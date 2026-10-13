using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Belote.Server;

/// <summary>Server settings bound from the <c>Table</c> configuration section.</summary>
public sealed class TableServerOptions
{
    public const string SectionName = "Table";

    /// <summary>Default pause before each bot move, in milliseconds (clamped to 0..5000 per table).</summary>
    public int BotDelayMs { get; set; } = 600;

    /// <summary>Optional directory receiving one JSON Lines log per finished game.</summary>
    public string? LogDirectory { get; set; }

    /// <summary>Clock used for bot pacing and room timers; tests may replace it.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>Room timing and limits, bound from the <c>Room</c> configuration section.</summary>
    public RoomOptions Rooms { get; } = new();
}

/// <summary>
/// Holds every room, maps connections to the room and participant they act for, and keeps the
/// finished-game store. A connection is never an identity: the participant is found by the token the
/// client presents when it joins or rejoins.
/// </summary>
public sealed class RoomRegistry
{
    private readonly IHubContext<TableHub> _hub;
    private readonly TableServerOptions _options;
    private readonly ILogger<RoomRegistry> _log;
    private readonly ConcurrentDictionary<Guid, Room> _rooms = new();
    private readonly ConcurrentDictionary<string, Binding> _bindings = new();
    private readonly object _currentGate = new();
    private Room? _latest;

    public RoomRegistry(IHubContext<TableHub> hub, TableServerOptions options, ILogger<RoomRegistry> log)
    {
        _hub = hub;
        _options = options;
        _log = log;
        Finished = new FinishedGames(options.LogDirectory);
        Finished.WriteFailed += ex => _log.LogWarning(ex, "Could not write a finished game log");
    }

    /// <summary>Finished games and their omniscient logs. Server-side only.</summary>
    public FinishedGames Finished { get; }

    /// <summary>Number of rooms currently held (diagnostics and tests).</summary>
    public int RoomCount => _rooms.Count;

    /// <summary>The table of the most recently created room (diagnostics and tests; never on the hub).</summary>
    public LiveTable? Current
    {
        get
        {
            lock (_currentGate)
            {
                return _latest?.CurrentTable;
            }
        }
    }

    public Room? Find(Guid id) => _rooms.TryGetValue(id, out var room) ? room : null;

    public async Task<RoomResultDto> CreateAsync(string connectionId, CreateRoomRequest? request)
    {
        var token = request?.Token;
        if (string.IsNullOrEmpty(token) || token.Length is < 16 or > 128)
        {
            return Fail(RoomRejections.BadToken());
        }

        var rooms = _options.Rooms;
        rooms.TimeProvider = _options.TimeProvider;
        var seed = request?.Seed;
        var firstGame = true;
        var settings = new RoomSettings(
            () =>
            {
                if (firstGame && seed is { } fixedSeed)
                {
                    firstGame = false;
                    return fixedSeed;
                }

                firstGame = false;
                return RandomSeed();
            },
            TableOptions.Clamp(TimeSpan.FromMilliseconds(request?.BotDelayMs ?? _options.BotDelayMs)));

        var id = Guid.NewGuid();
        var room = Room.Create(
            id,
            rooms,
            settings,
            onFinished: game =>
            {
                Finished.Add(game);
                _log.LogInformation("Room {RoomId} finished a game", id);
            });
        _rooms[id] = room;
        lock (_currentGate)
        {
            _latest = room;
        }

        _ = room.Ended.ContinueWith(_ => Drop(room), TaskScheduler.Default);
        _log.LogInformation("Room {RoomId} created", id);

        var result = await AttachAsync(connectionId, room, token, (sink) => room.JoinAsync(token, request?.Name, connectionId, sink))
            .ConfigureAwait(false);
        if (!result.Ok)
        {
            // Nobody got in (e.g. an invalid name): do not keep an empty room around.
            _rooms.TryRemove(id, out _);
            await room.DisposeAsync().ConfigureAwait(false);
        }

        return result;
    }

    public async Task<RoomResultDto> JoinAsync(string connectionId, JoinRoomRequest? request)
    {
        if (!TryParseRoomId(request?.RoomId, out var id) || !_rooms.TryGetValue(id, out var room))
        {
            return Fail(RoomRejections.NotFound());
        }

        var token = request!.Token;
        return await AttachAsync(connectionId, room, token, sink => room.JoinAsync(token, request.Name, connectionId, sink))
            .ConfigureAwait(false);
    }

    public async Task<RoomResultDto> RejoinAsync(string connectionId, RejoinRequest? request)
    {
        if (!TryParseRoomId(request?.RoomId, out var id) || !_rooms.TryGetValue(id, out var room))
        {
            return Fail(RoomRejections.NotFound());
        }

        var token = request!.Token;
        return await AttachAsync(connectionId, room, token, sink => room.RejoinAsync(token, connectionId, sink))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The room and token the connection acts for, or null when it has not joined or was taken over by a
    /// newer connection of the same person.
    /// </summary>
    public (Room Room, string Token)? BindingFor(string connectionId) =>
        _bindings.TryGetValue(connectionId, out var b) && !b.Sink.IsSuperseded ? (b.Room, b.Token) : null;

    /// <summary>The binding even when superseded; used to take control back.</summary>
    public (Room Room, string Token)? AnyBindingFor(string connectionId) =>
        _bindings.TryGetValue(connectionId, out var b) ? (b.Room, b.Token) : null;

    public bool IsSuperseded(string connectionId) =>
        _bindings.TryGetValue(connectionId, out var b) && b.Sink.IsSuperseded;

    /// <summary>Forgets the connection's binding (it left the room on purpose).</summary>
    public async Task UnbindAsync(string connectionId)
    {
        if (_bindings.TryRemove(connectionId, out var binding))
        {
            await binding.Sink.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task DisconnectedAsync(string connectionId)
    {
        if (!_bindings.TryRemove(connectionId, out var binding))
        {
            return;
        }

        try
        {
            await binding.Room.DisconnectedAsync(connectionId).ConfigureAwait(false);
            _log.LogInformation("Room {RoomId}: a connection went away", binding.Room.Id);
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            await binding.Sink.DisposeAsync().ConfigureAwait(false);
        }
    }

    public static RoomResultDto ToDto(RoomResult result, Guid? roomId = null) => new(
        result.Ok,
        roomId?.ToString("D"),
        result.State,
        result.Rejection?.ToDto(),
        result.Update,
        result.AwayMoves,
        result.PendingNextHand);

    public static bool TryParseRoomId(string? text, out Guid id)
    {
        id = Guid.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return Guid.TryParse(text.Trim().Trim('{', '}'), out id);
    }

    private async Task<RoomResultDto> AttachAsync(
        string connectionId, Room room, string? token, Func<IRoomSink, Task<RoomResult>> join)
    {
        // A connection acts for one room at a time: moving to another counts as leaving the first.
        if (_bindings.TryGetValue(connectionId, out var existing) && existing.Room != room)
        {
            await DisconnectedAsync(connectionId).ConfigureAwait(false);
        }

        var sink = new ConnectionSink(_hub, connectionId);
        RoomResult result;
        try
        {
            result = await join(sink).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            await sink.DisposeAsync().ConfigureAwait(false);
            return Fail(RoomRejections.Ended());
        }

        if (!result.Ok)
        {
            await sink.DisposeAsync().ConfigureAwait(false);
            return ToDto(result, room.Id);
        }

        var binding = new Binding(room, token!, sink);
        _bindings[connectionId] = binding;

        // A re-join from the same connection replaces an older sink for it.
        if (existing is not null && !ReferenceEquals(existing.Sink, sink))
        {
            await existing.Sink.DisposeAsync().ConfigureAwait(false);
        }

        return ToDto(result, room.Id);
    }

    private void Drop(Room room)
    {
        _rooms.TryRemove(room.Id, out _);
        foreach (var (connectionId, binding) in _bindings.ToArray())
        {
            if (binding.Room == room && _bindings.TryRemove(connectionId, out var removed))
            {
                _ = removed.Sink.DisposeAsync().AsTask();
            }
        }

        _log.LogInformation("Room {RoomId} ended", room.Id);
        _ = room.DisposeAsync().AsTask();
    }

    private static RoomResultDto Fail(Rejection rejection) => new(false, null, null, rejection.ToDto());

    private static ulong RandomSeed() => BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));

    private sealed record Binding(Room Room, string Token, ConnectionSink Sink);
}
