using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Belote.Core;
using Belote.Simulation;

namespace Belote.Table;

/// <summary>Receives one update per state change, in version order. Must not block for long.</summary>
public interface ITableObserver
{
    ValueTask OnUpdateAsync(TableUpdateDto update);
}

public sealed record TakeSeatOutcome(bool Ok, int Version, Rejection? Rejection);

public sealed record ViewOutcome(TableUpdateDto? Update, Rejection? Rejection);

/// <summary>A finished game together with its omniscient log. Server-side only.</summary>
public sealed record FinishedGame(string TableId, GameLog Log, Team Winner, int NorthSouth, int EastWest);

/// <summary>
/// One live Belote table. A single-reader queue (the actor loop) applies every intent, seating and
/// pause change in arrival order against the immutable state the previous one left, so concurrent
/// callers can never corrupt state. Each applied change publishes a new <see cref="TableSnapshot"/>.
/// </summary>
public sealed class LiveTable : IAsyncDisposable
{
    /// <summary>The seat the connected human occupies.</summary>
    public const Seat ClientSeat = Seat.South;

    private static readonly Seat[] BotSeats = { Seat.North, Seat.East, Seat.West };

    private readonly Channel<Work> _queue = Channel.CreateUnbounded<Work>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly CancellationTokenSource _stop = new();
    private readonly List<ITableObserver> _observers = new();
    private readonly object _observerLock = new();
    private readonly TaskCompletionSource<FinishedGame?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Task _loop;
    private readonly Task _botLoop;
    private readonly Principal _creator;
    private readonly ImmutableHashSet<Principal> _known;

    private TableSnapshot _snapshot;
    private TaskCompletionSource _changed = NewSignal();
    private ImmutableDictionary<Seat, Principal> _owners;

    private readonly Action<FinishedGame>? _onFinished;

    private LiveTable(
        TableOptions options,
        Principal creator,
        Func<Seat, IPlayer>? botFactory,
        Action<FinishedGame>? onFinished)
    {
        Options = options;
        _onFinished = onFinished;
        _creator = creator;
        Id = Guid.NewGuid().ToString("N");

        var bots = BotSeats.ToDictionary(s => s, s => new BotPrincipal(s));
        _owners = bots.ToImmutableDictionary(kv => kv.Key, kv => (Principal)kv.Value);
        _known = bots.Values.Cast<Principal>().Append(creator).ToImmutableHashSet();

        _snapshot = new TableSnapshot(0, null, null, TableLifecycle.AwaitingSeat, false, ImmutableList<GameEvent>.Empty);

        _loop = Task.Run(RunAsync);
        var driver = new BotDriver(this, options, botFactory ?? DefaultBot(options.Seed), _stop.Token);
        _botLoop = Task.Run(driver.RunAsync);
    }

    public string Id { get; }

    public TableOptions Options { get; }

    /// <summary>The latest published state; reading it never changes anything.</summary>
    public TableSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Completes with the finished game when the match ends (null if the table is disposed first).</summary>
    public Task<FinishedGame?> Completion => _completion.Task;

    /// <summary>Completes the next time the snapshot changes. Read it before reading <see cref="Snapshot"/>.</summary>
    internal Task ChangedTask => Volatile.Read(ref _changed).Task;

    /// <param name="onFinished">
    /// Called inside the actor with the finished game before the final update is pushed, so a client that has
    /// seen the end of the game can rely on the record already being stored.
    /// </param>
    public static LiveTable Create(
        TableOptions options,
        Principal creator,
        Func<Seat, IPlayer>? botFactory = null,
        Action<FinishedGame>? onFinished = null) =>
        new(options, creator, botFactory, onFinished);

    public static Func<Seat, IPlayer> DefaultBot(ulong seed) =>
        seat => new Belote.Bots.RandomLegalBot(Seeds.ForSeat(seed, seat));

    public IDisposable Subscribe(ITableObserver observer)
    {
        lock (_observerLock)
        {
            _observers.Add(observer);
        }

        return new Subscription(this, observer);
    }

    public Task<TakeSeatOutcome> TakeSeatAsync(Principal client)
    {
        var done = NewResult<TakeSeatOutcome>();
        Enqueue(new SeatWork(client, done));
        return done.Task;
    }

    public ValueTask<IntentOutcome> SubmitAsync(Intent intent)
    {
        var done = NewResult<IntentOutcome>();
        Enqueue(new SubmitWork(intent, done));
        return new ValueTask<IntentOutcome>(done.Task);
    }

    /// <summary>Parses a wire intent and submits it; a malformed intent never reaches the queue.</summary>
    public ValueTask<IntentOutcome> SubmitAsync(Principal sender, IntentDto dto)
    {
        if (!IntentParser.TryParse(dto, out var seat, out var action, out var rejection))
        {
            return new ValueTask<IntentOutcome>(new IntentRejected(rejection!));
        }

        return SubmitAsync(new Intent(seat, action, sender));
    }

    public ViewOutcome GetView(Principal client)
    {
        var snap = Snapshot;
        if (!_known.Contains(client))
        {
            return new ViewOutcome(null, Rejection.NotSeated("This connection is not seated at the table."));
        }

        if (snap.State is null || !_owners.TryGetValue(ClientSeat, out var owner) || owner != client)
        {
            return new ViewOutcome(
                null,
                new Rejection(RejectionCategory.GameNotInProgress, "GameNotStarted", "The game has not started."));
        }

        return new ViewOutcome(BuildUpdate(snap), null);
    }

    /// <summary>The client connection went away: no state change happens until <see cref="AttachAsync"/>.</summary>
    public Task DetachAsync(Principal client) => EnqueuePause(client, paused: true);

    public Task AttachAsync(Principal client) => EnqueuePause(client, paused: false);

    internal Task ReportFaultAsync(string detail)
    {
        var done = NewResult<bool>();
        Enqueue(new FaultWork(detail, done));
        return done.Task;
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _stop.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(_loop, _botLoop).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _completion.TrySetResult(null);
        _stop.Dispose();
    }

    private Task EnqueuePause(Principal client, bool paused)
    {
        var done = NewResult<bool>();
        Enqueue(new PauseWork(client, paused, done));
        return done.Task;
    }

    private void Enqueue(Work work)
    {
        if (!_queue.Writer.TryWrite(work))
        {
            throw new ObjectDisposedException(nameof(LiveTable));
        }
    }

    private async Task RunAsync()
    {
        await foreach (var work in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                switch (work)
                {
                    case SeatWork w:
                        w.Done.SetResult(await TakeSeatCoreAsync(w.Client).ConfigureAwait(false));
                        break;
                    case SubmitWork w:
                        w.Done.SetResult(await SubmitCoreAsync(w.Intent).ConfigureAwait(false));
                        break;
                    case PauseWork w:
                        PauseCore(w);
                        w.Done.SetResult(true);
                        break;
                    case FaultWork w:
                        FaultCore(w);
                        w.Done.SetResult(true);
                        break;
                }
            }
            catch (Exception ex)
            {
                // An unexpected failure makes the table unusable; surface it to the caller and stop play.
                var snap = _snapshot;
                if (snap.Lifecycle != TableLifecycle.Finished)
                {
                    Publish(snap with { Lifecycle = TableLifecycle.Faulted });
                }

                work.Fail(ex);
            }
        }
    }

    private async ValueTask<TakeSeatOutcome> TakeSeatCoreAsync(Principal client)
    {
        var snap = _snapshot;
        if (client != _creator)
        {
            return new TakeSeatOutcome(false, snap.Version, Rejection.NotOwner(ClientSeat));
        }

        if (snap.Lifecycle != TableLifecycle.AwaitingSeat)
        {
            return new TakeSeatOutcome(
                false,
                snap.Version,
                new Rejection(RejectionCategory.TableExists, "SeatTaken", "The seat is already taken."));
        }

        var start = GameEngine.Start(Options.RuleSet, Options.Seed);
        _owners = _owners.SetItem(ClientSeat, client);
        var next = new TableSnapshot(
            0, start.State, null, TableLifecycle.InProgress, false, ImmutableList.CreateRange(start.Events));
        Publish(next);
        await NotifyAsync(next).ConfigureAwait(false);
        return new TakeSeatOutcome(true, 0, null);
    }

    private async ValueTask<IntentOutcome> SubmitCoreAsync(Intent intent)
    {
        var snap = _snapshot;
        if (!_known.Contains(intent.Sender))
        {
            return new IntentRejected(Rejection.NotSeated("This connection is not seated at the table."));
        }

        if (snap.Lifecycle != TableLifecycle.InProgress)
        {
            return new IntentRejected(NotInProgress(snap.Lifecycle));
        }

        if (snap.Paused)
        {
            return new IntentRejected(Rejection.Paused());
        }

        if (!_owners.TryGetValue(intent.Seat, out var owner) || owner != intent.Sender)
        {
            return new IntentRejected(Rejection.NotOwner(intent.Seat));
        }

        var state = snap.State!;
        var result = GameEngine.Apply(state, intent.Seat, intent.Action);
        if (result is Belote.Core.Rejected engineRejection)
        {
            return new IntentRejected(Rejection.FromEngine(engineRejection.Reason, engineRejection.Detail));
        }

        var accepted = (Belote.Core.Accepted)result;
        var last = NextLastTrick(snap, state, intent, accepted);
        bool finished = accepted.State.Phase == Phase.Finished;
        var next = new TableSnapshot(
            snap.Version + 1,
            accepted.State,
            last,
            finished ? TableLifecycle.Finished : TableLifecycle.InProgress,
            false,
            snap.Events.AddRange(accepted.Events));

        Publish(next);
        FinishedGame? game = null;
        if (finished)
        {
            game = BuildFinished(next);
            _onFinished?.Invoke(game);
        }

        await NotifyAsync(next).ConfigureAwait(false);
        if (game is not null)
        {
            _completion.TrySetResult(game);
        }

        return new IntentAccepted(next.Version);
    }

    private void PauseCore(PauseWork work)
    {
        var snap = _snapshot;
        if (work.Client != _creator || snap.Lifecycle != TableLifecycle.InProgress || snap.Paused == work.Paused)
        {
            return;
        }

        Publish(snap with { Paused = work.Paused });
    }

    private void FaultCore(FaultWork work)
    {
        var snap = _snapshot;
        if (snap.Lifecycle == TableLifecycle.InProgress)
        {
            Publish(snap with { Lifecycle = TableLifecycle.Faulted });
        }
    }

    private static Rejection NotInProgress(TableLifecycle lifecycle) => lifecycle switch
    {
        TableLifecycle.AwaitingSeat => new Rejection(
            RejectionCategory.GameNotInProgress, "GameNotStarted", "The game has not started."),
        TableLifecycle.Finished => new Rejection(
            RejectionCategory.GameNotInProgress, "GameFinished", "The game is finished."),
        _ => new Rejection(RejectionCategory.GameNotInProgress, "TableFaulted", "The table has failed."),
    };

    /// <summary>Keeps the last completed trick until the next card is played (FR-027).</summary>
    private static CompletedTrick? NextLastTrick(TableSnapshot snap, GameState before, Intent intent, Belote.Core.Accepted accepted)
    {
        if (intent.Action is not PlayCardAction play)
        {
            return snap.LastCompletedTrick;
        }

        var won = accepted.Events.OfType<TrickWon>().FirstOrDefault();
        if (won is null)
        {
            return null;
        }

        var plays = before.Hand.CurrentPlays.Add(new PlayedCard(intent.Seat, play.Card));
        return new CompletedTrick(before.DealNumber, won.TrickNumber, before.Hand.Leader, plays, won.Winner);
    }

    private FinishedGame BuildFinished(TableSnapshot snap)
    {
        var state = snap.State!;
        var seats = Seats.All.ToDictionary(
            s => s,
            s => s == ClientSeat ? "RemoteClient" : nameof(Belote.Bots.RandomLegalBot));
        var log = new GameLog(state.Seed, state.RuleSet, seats, snap.Events);
        return new FinishedGame(Id, log, state.Winner!.Value, state.MatchScore[0], state.MatchScore[1]);
    }

    private TableUpdateDto BuildUpdate(TableSnapshot snap)
    {
        var view = Projection.For(snap.State!, ClientSeat);
        return SeatViewMapper.ToUpdate(snap.Version, view, snap.LastCompletedTrick);
    }

    private void Publish(TableSnapshot snapshot)
    {
        Volatile.Write(ref _snapshot, snapshot);
        var old = Interlocked.Exchange(ref _changed, NewSignal());
        old.TrySetResult();
    }

    private async ValueTask NotifyAsync(TableSnapshot snap)
    {
        ITableObserver[] targets;
        lock (_observerLock)
        {
            if (_observers.Count == 0)
            {
                return;
            }

            targets = _observers.ToArray();
        }

        var update = BuildUpdate(snap);
        foreach (var observer in targets)
        {
            try
            {
                await observer.OnUpdateAsync(update).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A faulty observer must never stop the table.
            }
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource<T> NewResult<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private abstract record Work
    {
        public abstract void Fail(Exception exception);
    }

    private sealed record SeatWork(Principal Client, TaskCompletionSource<TakeSeatOutcome> Done) : Work
    {
        public override void Fail(Exception exception) => Done.TrySetException(exception);
    }

    private sealed record SubmitWork(Intent Intent, TaskCompletionSource<IntentOutcome> Done) : Work
    {
        public override void Fail(Exception exception) => Done.TrySetException(exception);
    }

    private sealed record PauseWork(Principal Client, bool Paused, TaskCompletionSource<bool> Done) : Work
    {
        public override void Fail(Exception exception) => Done.TrySetException(exception);
    }

    private sealed record FaultWork(string Detail, TaskCompletionSource<bool> Done) : Work
    {
        public override void Fail(Exception exception) => Done.TrySetException(exception);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly LiveTable _table;
        private readonly ITableObserver _observer;

        public Subscription(LiveTable table, ITableObserver observer)
        {
            _table = table;
            _observer = observer;
        }

        public void Dispose()
        {
            lock (_table._observerLock)
            {
                _table._observers.Remove(_observer);
            }
        }
    }
}
