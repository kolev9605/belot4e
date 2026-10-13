using Belote.Table;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Belote.Server.Tests;

/// <summary>A browser tab stand-in: one SignalR connection plus the identity token it presents.</summary>
internal sealed class RoomClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly object _lock = new();
    private readonly List<RoomStateDto> _rooms = new();
    private readonly List<TableUpdateDto> _updates = new();
    private readonly List<TableClosedDto> _closed = new();
    private int _superseded;

    private RoomClient(HubConnection connection, string token, string name)
    {
        _connection = connection;
        Token = token;
        Name = name;
        _connection.On<RoomStateDto>("Room", state =>
        {
            lock (_lock)
            {
                _rooms.Add(state);
            }
        });
        _connection.On<TableUpdateDto>("Update", update =>
        {
            lock (_lock)
            {
                _updates.Add(update);
            }
        });
        _connection.On<SupersededDto>("Superseded", _ => Interlocked.Increment(ref _superseded));
        _connection.On<TableClosedDto>("TableClosed", closed =>
        {
            lock (_lock)
            {
                _closed.Add(closed);
            }
        });
    }

    public string Token { get; }

    public string Name { get; }

    public string? RoomId { get; private set; }

    public int SupersededCount => Volatile.Read(ref _superseded);

    public RoomStateDto? LastRoom
    {
        get
        {
            lock (_lock)
            {
                return _rooms.LastOrDefault();
            }
        }
    }

    public TableUpdateDto? LastUpdate
    {
        get
        {
            lock (_lock)
            {
                return _updates.LastOrDefault();
            }
        }
    }

    public IReadOnlyList<TableUpdateDto> Updates
    {
        get
        {
            lock (_lock)
            {
                return _updates.ToArray();
            }
        }
    }

    public IReadOnlyList<RoomStateDto> Rooms
    {
        get
        {
            lock (_lock)
            {
                return _rooms.ToArray();
            }
        }
    }

    public IReadOnlyList<TableClosedDto> Closed
    {
        get
        {
            lock (_lock)
            {
                return _closed.ToArray();
            }
        }
    }

    public static async Task<RoomClient> ConnectAsync(string hubUrl, string name, string? token = null)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, HttpTransportType.WebSockets)
            .Build();
        var client = new RoomClient(connection, token ?? Guid.NewGuid().ToString("N"), name);
        await connection.StartAsync();
        return client;
    }

    /// <summary>A second tab for the same person (same token, new connection).</summary>
    public Task<RoomClient> OpenAnotherTabAsync(string hubUrl) => ConnectAsync(hubUrl, Name, Token);

    public async Task<RoomResultDto> CreateRoomAsync(ulong? seed = 1, int botDelayMs = 0)
    {
        var result = await _connection.InvokeAsync<RoomResultDto>(
            "CreateRoom", new CreateRoomRequest(Token, Name, seed, botDelayMs));
        RoomId = result.RoomId;
        return result;
    }

    public async Task<RoomResultDto> JoinRoomAsync(string roomId)
    {
        var result = await _connection.InvokeAsync<RoomResultDto>(
            "JoinRoom", new JoinRoomRequest(roomId, Token, Name));
        if (result.Ok)
        {
            RoomId = result.RoomId;
        }

        return result;
    }

    public Task<RoomResultDto> RejoinAsync(string? roomId = null) =>
        _connection.InvokeAsync<RoomResultDto>("Rejoin", new RejoinRequest(roomId ?? RoomId, Token));

    public Task<RoomResultDto> ClaimSeatAsync(string seat) =>
        _connection.InvokeAsync<RoomResultDto>("ClaimSeat", new ClaimSeatRequest(seat));

    public Task<RoomResultDto> SetNameAsync(string name) =>
        _connection.InvokeAsync<RoomResultDto>("SetName", new SetNameRequest(name));

    public Task<RoomResultDto> StartGameAsync() => _connection.InvokeAsync<RoomResultDto>("StartGame");

    public Task<RoomResultDto> SkipWaitAsync() =>
        _connection.InvokeAsync<RoomResultDto>("SkipWait", new SkipWaitRequest());

    public Task<RoomResultDto> LeaveRoomAsync() => _connection.InvokeAsync<RoomResultDto>("LeaveRoom");

    public Task<RoomResultDto> PlayAgainAsync() => _connection.InvokeAsync<RoomResultDto>("PlayAgain");

    public Task<RoomResultDto> TakeBackAsync() => _connection.InvokeAsync<RoomResultDto>("TakeBack");

    public Task<IntentResultDto> SubmitAsync(IntentDto intent) =>
        _connection.InvokeAsync<IntentResultDto>("SubmitIntent", intent);

    public Task<GetViewResultDto> GetViewAsync() => _connection.InvokeAsync<GetViewResultDto>("GetView");

    public Task StopAsync() => _connection.StopAsync();

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>Waits until the condition holds on this client's observations.</summary>
    public static async Task WaitForAsync(Func<bool> condition, string what, int timeoutMs = 10_000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, $"Timed out waiting for: {what}");
            await Task.Delay(5);
        }
    }

    /// <summary>Plays the first legal action whenever this client has one, until its game is finished.</summary>
    public static async Task PlayToEndAsync(IReadOnlyList<RoomClient> players, int timeoutSeconds = 60)
    {
        var until = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var submitted = players.ToDictionary(p => p, _ => -1);
        while (players.Any(p => p.LastUpdate is null || !p.LastUpdate.Finished))
        {
            Assert.True(DateTime.UtcNow < until, "The game did not finish in time.");
            bool acted = false;
            foreach (var player in players)
            {
                var update = player.LastUpdate;
                if (update is null || update.Finished || update.View.LegalActions.Count == 0
                    || submitted[player] == update.Version)
                {
                    continue;
                }

                submitted[player] = update.Version;
                var result = await player.SubmitAsync(update.View.LegalActions[0]);
                if (!result.Accepted)
                {
                    Assert.True(
                        result.Rejection?.Category is "paused" or "notOwner",
                        $"Legal intent rejected: {result.Rejection?.Code}");
                    submitted[player] = -1;
                }

                acted = true;
            }

            if (!acted)
            {
                await Task.Delay(3);
            }
        }
    }
}
