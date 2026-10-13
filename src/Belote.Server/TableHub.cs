using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace Belote.Server;

/// <summary>
/// The persistent-connection surface of rooms. It parses and forwards requests only; every decision
/// (membership, ownership, turn, legality) is made by the <see cref="Room"/>, its <see cref="LiveTable"/>
/// and the rules engine. A connection is never an identity: the client's token is.
/// </summary>
public sealed class TableHub : Hub
{
    public const string Path = "/table";

    private readonly RoomRegistry _registry;

    public TableHub(RoomRegistry registry)
    {
        _registry = registry;
    }

    // ---- rooms ----------------------------------------------------------------------------

    public Task<RoomResultDto> CreateRoom(CreateRoomRequest? request) =>
        _registry.CreateAsync(Context.ConnectionId, request);

    public Task<RoomResultDto> JoinRoom(JoinRoomRequest? request) =>
        _registry.JoinAsync(Context.ConnectionId, request);

    public Task<RoomResultDto> Rejoin(RejoinRequest? request) =>
        _registry.RejoinAsync(Context.ConnectionId, request);

    /// <summary>Takes control back for this tab after a <c>Superseded</c> push.</summary>
    public Task<RoomResultDto> TakeBack()
    {
        if (_registry.AnyBindingFor(Context.ConnectionId) is not { } b)
        {
            return Task.FromResult(NotInRoom());
        }

        return _registry.RejoinAsync(
            Context.ConnectionId, new RejoinRequest(b.Room.Id.ToString("D"), b.Token));
    }

    public Task<RoomResultDto> SetName(SetNameRequest? request) =>
        Run(b => b.Room.SetNameAsync(b.Token, request?.Name));

    public Task<RoomResultDto> ClaimSeat(ClaimSeatRequest? request)
    {
        if (!IntentParser.TryParseSeat(request?.Seat, out var seat))
        {
            return Task.FromResult(new RoomResultDto(
                false, null, null, Rejection.Malformed($"Unknown or missing seat [{request?.Seat}].").ToDto()));
        }

        return Run(b => b.Room.ClaimSeatAsync(b.Token, seat));
    }

    public Task<RoomResultDto> ReleaseSeat() => Run(b => b.Room.ReleaseSeatAsync(b.Token));

    public Task<RoomResultDto> StartGame() => Run(b => b.Room.StartGameAsync(b.Token));

    public Task<RoomResultDto> PlayAgain() => Run(b => b.Room.PlayAgainAsync(b.Token));

    public Task<RoomResultDto> SkipWait(SkipWaitRequest? request)
    {
        Guid? target = Guid.TryParse(request?.ParticipantId, out var id) ? id : null;
        return Run(b => b.Room.SkipWaitAsync(b.Token, target));
    }

    public async Task<RoomResultDto> LeaveRoom()
    {
        if (_registry.BindingFor(Context.ConnectionId) is not { } b)
        {
            return new RoomResultDto(true, null, null, null);
        }

        var result = await b.Room.LeaveAsync(b.Token).ConfigureAwait(false);
        await _registry.UnbindAsync(Context.ConnectionId).ConfigureAwait(false);
        return RoomRegistry.ToDto(result, b.Room.Id);
    }

    // ---- play -----------------------------------------------------------------------------

    public async Task<IntentResultDto> SubmitIntent(IntentDto? intent)
    {
        if (_registry.BindingFor(Context.ConnectionId) is not { } b)
        {
            return new IntentResultDto(false, null, NoTable().ToDto());
        }

        var outcome = await b.Room.SubmitIntentAsync(b.Token, intent).ConfigureAwait(false);
        return outcome switch
        {
            IntentAccepted a => new IntentResultDto(true, a.Version, null),
            IntentRejected r => new IntentResultDto(false, null, r.Rejection.ToDto()),
            _ => throw new InvalidOperationException("Unknown outcome."),
        };
    }

    public async Task<GetViewResultDto> GetView()
    {
        if (_registry.BindingFor(Context.ConnectionId) is not { } b)
        {
            return new GetViewResultDto(null, NoTable().ToDto());
        }

        var result = await b.Room.GetViewAsync(b.Token).ConfigureAwait(false);
        return new GetViewResultDto(result.Update, result.Rejection?.ToDto());
    }

    // ---- single-player shim (feature 002) ---------------------------------------------------

    /// <summary>Legacy: creates a room with this connection seated at South; the game starts on <c>TakeSeat</c>.</summary>
    public async Task<CreateTableResultDto> CreateTable(CreateTableRequest? request)
    {
        if (_registry.BindingFor(Context.ConnectionId) is { } existing)
        {
            var info = await existing.Room.InfoAsync().ConfigureAwait(false);
            if (info.Lifecycle is RoomLifecycle.Lobby or RoomLifecycle.InGame)
            {
                var rejection = new Rejection(
                    RejectionCategory.TableExists, "TableExists", "A game is already in progress for this connection.");
                return new CreateTableResultDto(false, null, rejection.ToDto());
            }
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var result = await _registry.CreateAsync(
            Context.ConnectionId,
            new CreateRoomRequest(token, "Player", request?.Seed, request?.BotDelayMs)).ConfigureAwait(false);
        return result.Ok
            ? new CreateTableResultDto(true, result.RoomId, null)
            : new CreateTableResultDto(false, null, result.Rejection);
    }

    /// <summary>Legacy: the connection's seat is South; taking it starts the game.</summary>
    public async Task<TakeSeatResultDto> TakeSeat(TakeSeatRequest? request)
    {
        if (_registry.BindingFor(Context.ConnectionId) is not { } b)
        {
            return new TakeSeatResultDto(false, null, NoTable().ToDto());
        }

        if (!IntentParser.TryParseSeat(request?.Seat, out var seat))
        {
            return new TakeSeatResultDto(
                false, null, Rejection.Malformed($"Unknown or missing seat [{request?.Seat}].").ToDto());
        }

        if (seat != Seat.South)
        {
            return new TakeSeatResultDto(false, null, Rejection.NotOwner(seat).ToDto());
        }

        var result = await b.Room.StartGameAsync(b.Token).ConfigureAwait(false);
        if (result.Ok)
        {
            return new TakeSeatResultDto(true, 0, null);
        }

        var rejection = result.Rejection!.Code == "GameInProgress"
            ? new Rejection(RejectionCategory.TableExists, "SeatTaken", "The seat is already taken.")
            : result.Rejection;
        return new TakeSeatResultDto(false, null, rejection.ToDto());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _registry.DisconnectedAsync(Context.ConnectionId).ConfigureAwait(false);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    private async Task<RoomResultDto> Run(Func<(Room Room, string Token), Task<RoomResult>> body)
    {
        if (_registry.BindingFor(Context.ConnectionId) is not { } b)
        {
            return NotInRoom();
        }

        try
        {
            var result = await body(b).ConfigureAwait(false);
            return RoomRegistry.ToDto(result, b.Room.Id);
        }
        catch (ObjectDisposedException)
        {
            return new RoomResultDto(false, null, null, RoomRejections.Ended().ToDto());
        }
    }

    private RoomResultDto NotInRoom() => new(
        false,
        null,
        null,
        (_registry.IsSuperseded(Context.ConnectionId) ? RoomRejections.Superseded() : RoomRejections.NotInRoom()).ToDto());

    private Rejection NoTable() => _registry.IsSuperseded(Context.ConnectionId)
        ? RoomRejections.Superseded()
        : Rejection.NotSeated("This connection has not created a table.");
}
