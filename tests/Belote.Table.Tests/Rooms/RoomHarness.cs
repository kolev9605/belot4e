using Microsoft.Extensions.Time.Testing;
using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;

namespace Belote.Table.Tests;

/// <summary>Records everything a room pushes to one connection.</summary>
internal sealed class TestSink : IRoomSink
{
    private readonly object _lock = new();
    private readonly List<RoomStateDto> _rooms = new();
    private readonly List<TableUpdateDto> _updates = new();

    public int SupersededCount { get; private set; }

    public string? ClosedReason { get; private set; }

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

    public void PushRoom(RoomStateDto state)
    {
        lock (_lock)
        {
            _rooms.Add(state);
        }
    }

    public void PushUpdate(TableUpdateDto update)
    {
        lock (_lock)
        {
            _updates.Add(update);
        }
    }

    public void PushSuperseded() => SupersededCount++;

    public void PushClosed(string reason) => ClosedReason = reason;
}

/// <summary>A person in a room test: a token, a name and the connection that is currently attached.</summary>
internal sealed class Person
{
    private int _connections;

    public Person(string name, char tokenChar)
    {
        Name = name;
        Token = new string(tokenChar, 32);
    }

    public string Name { get; }

    public string Token { get; }

    public TestSink Sink { get; private set; } = new();

    public string ConnectionId { get; private set; } = string.Empty;

    public string NewConnection()
    {
        Sink = new TestSink();
        ConnectionId = $"{Name}-{++_connections}";
        return ConnectionId;
    }
}

internal sealed class RoomRig : IAsyncDisposable
{
    public RoomRig(TimeSpan? botDelay = null, ulong seed = 7, Action<RoomOptions>? configure = null)
    {
        Time = new FakeTimeProvider();
        Options = new RoomOptions { TimeProvider = Time };
        configure?.Invoke(Options);
        Room = Room.Create(Guid.NewGuid(), Options, new RoomSettings(() => seed, botDelay ?? TimeSpan.FromHours(1)));
    }

    public FakeTimeProvider Time { get; }

    public RoomOptions Options { get; }

    public Room Room { get; }

    public Task<RoomResult> JoinAsync(Person p) => Room.JoinAsync(p.Token, p.Name, p.NewConnection(), p.Sink);

    public Task<RoomResult> RejoinAsync(Person p) => Room.RejoinAsync(p.Token, p.NewConnection(), p.Sink);

    public async Task DisconnectAsync(Person p)
    {
        await Room.DisconnectedAsync(p.ConnectionId);
        await Room.SyncAsync();
    }

    public async Task AdvanceAsync(TimeSpan by)
    {
        Time.Advance(by);
        await Room.SyncAsync();
        await Room.SyncAsync();
    }

    public async Task<RoomResult> ClaimAsync(Person p, Seat seat)
    {
        var result = await Room.ClaimSeatAsync(p.Token, seat);
        Assert.True(result.Ok, result.Rejection?.Code);
        return result;
    }

    /// <summary>Joins each person; the first becomes host at South, the rest take the given seats.</summary>
    public async Task SeatAsync(params (Person Person, Seat Seat)[] people)
    {
        bool first = true;
        foreach (var (person, seat) in people)
        {
            var joined = await JoinAsync(person);
            Assert.True(joined.Ok, joined.Rejection?.Code);
            if (first)
            {
                first = false;
                if (seat != Seat.South)
                {
                    await ClaimAsync(person, seat);
                }
            }
            else
            {
                await ClaimAsync(person, seat);
            }
        }
    }

    /// <summary>Plays the first legal action for every person whose turn it is, until the game finishes.</summary>
    public async Task PlayOutAsync(IReadOnlyList<Person> humans, TimeSpan? timeout = null)
    {
        var until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(60));
        var submitted = humans.ToDictionary(h => h, _ => -1);
        while (true)
        {
            Assert.True(DateTime.UtcNow < until, "The game did not finish in time.");
            var info = await Room.InfoAsync();
            if (info.Lifecycle != RoomLifecycle.InGame)
            {
                return;
            }

            bool acted = false;
            foreach (var human in humans)
            {
                var update = human.Sink.LastUpdate;
                if (update is null || update.Finished || update.View.LegalActions.Count == 0
                    || submitted[human] == update.Version)
                {
                    continue;
                }

                submitted[human] = update.Version;
                var outcome = await Room.SubmitIntentAsync(human.Token, update.View.LegalActions[0]);
                if (outcome is IntentRejected rejected)
                {
                    if (rejected.Rejection.Category is not (RejectionCategory.Paused or RejectionCategory.NotOwner
                        or RejectionCategory.NotSeated or RejectionCategory.GameNotInProgress
                        or RejectionCategory.State))
                    {
                        throw new InvalidOperationException(
                            $"Legal intent rejected: {rejected.Rejection.Code}: {rejected.Rejection.Detail}");
                    }

                    submitted[human] = -1;
                }

                acted = true;
            }

            if (!acted)
            {
                await Task.Delay(2);
            }
        }
    }

    public async ValueTask DisposeAsync() => await Room.DisposeAsync();
}
