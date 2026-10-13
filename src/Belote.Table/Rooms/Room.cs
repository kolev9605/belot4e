using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Belote.Core;
using Belote.Simulation;

namespace Belote.Table.Rooms;

/// <summary>
/// One room: lobby, four seats, a host, humans and bots, and the connection lifecycle around a
/// <see cref="LiveTable"/>. Like the table it is an actor: one reader applies every operation, timer
/// and table notification in arrival order, so no lock guards its state and every timing rule is
/// driven only by the injected <see cref="TimeProvider"/>.
/// </summary>
public sealed class Room : IAsyncDisposable
{
    private const int MaxParticipants = 8;

    private static readonly Seat[] SeatOrder = { Seat.South, Seat.West, Seat.North, Seat.East };

    private readonly RoomOptions _options;
    private readonly RoomSettings _settings;
    private readonly TimeProvider _time;
    private readonly Action<FinishedGame>? _onFinished;
    private readonly Func<Seat, IPlayer>? _botFactory;
    private readonly Channel<Func<Task>> _queue = Channel.CreateUnbounded<Func<Task>>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Task _loop;
    private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // ---- actor-owned state -------------------------------------------------------------------
    private readonly List<Member> _members = new();
    private readonly Dictionary<Seat, SeatSlot> _slots = new();
    private readonly Dictionary<Seat, PendingClaim> _pending = new();
    private RoomLifecycle _lifecycle = RoomLifecycle.Lobby;
    private Guid? _hostId;
    private int _revision;
    private RoomEventDto? _lastEvent;
    private LiveTable? _table;
    private IDisposable? _tableSubscription;
    private TableSnapshot? _previous;
    private ITimer? _idleTimer;
    private int _timerGeneration;
    private bool _disposed;

    private Room(
        Guid id,
        RoomOptions options,
        RoomSettings settings,
        Action<FinishedGame>? onFinished,
        Func<Seat, IPlayer>? botFactory)
    {
        Id = id;
        _options = options;
        _settings = settings;
        _time = options.TimeProvider;
        _onFinished = onFinished;
        _botFactory = botFactory;
        foreach (var seat in Seats.All)
        {
            _slots[seat] = SeatSlot.Free;
        }

        _loop = Task.Run(RunAsync);
    }

    public Guid Id { get; }

    /// <summary>The running table, for diagnostics and tests only; never exposed on the hub.</summary>
    public LiveTable? CurrentTable => Volatile.Read(ref _table);

    /// <summary>Completes when the room has ended (idle expiry or disposal).</summary>
    public Task Ended => _ended.Task;

    public static Room Create(
        Guid id,
        RoomOptions options,
        RoomSettings settings,
        Action<FinishedGame>? onFinished = null,
        Func<Seat, IPlayer>? botFactory = null) =>
        new(id, options, settings, onFinished, botFactory);

    // =========================================================================================
    // Public operations. Each one runs inside the actor and returns the caller's view of the room.
    // =========================================================================================

    /// <summary>
    /// Adds a person (the first one becomes host and is seated at South), or re-attaches a known one.
    /// A known token never creates a second participant.
    /// </summary>
    public Task<RoomResult> JoinAsync(string? token, string? name, string connectionId, IRoomSink sink) =>
        Do(() => JoinCore(token, name, connectionId, sink, mustExist: false));

    /// <summary>Re-attaches a known participant to a new connection (reload, reconnect, take back).</summary>
    public Task<RoomResult> RejoinAsync(string? token, string connectionId, IRoomSink sink) =>
        Do(() => JoinCore(token, null, connectionId, sink, mustExist: true));

    public Task<RoomResult> ClaimSeatAsync(string? token, Seat seat) => Do(() => ClaimSeatCore(token, seat));

    public Task<RoomResult> ReleaseSeatAsync(string? token) => Do(() => ReleaseSeatCore(token));

    public Task<RoomResult> SetNameAsync(string? token, string? name) => Do(() => SetNameCore(token, name));

    public Task<RoomResult> StartGameAsync(string? token) => Do(() => StartGameCore(token));

    public Task<RoomResult> SkipWaitAsync(string? token, Guid? target) => Do(() => SkipWaitCore(token, target));

    public Task<RoomResult> LeaveAsync(string? token) => Do(() => LeaveCore(token));

    public Task<RoomResult> PlayAgainAsync(string? token) => Do(() => PlayAgainCore(token));

    public Task<IntentOutcome> SubmitIntentAsync(string? token, IntentDto? dto) => Do(() => SubmitCore(token, dto));

    public Task<RoomResult> GetViewAsync(string? token) => Do(() => GetViewCore(token));

    /// <summary>A connection went away. Ignored when the participant has already moved to a newer one.</summary>
    public Task DisconnectedAsync(string connectionId) => Do(async () =>
    {
        await DisconnectedCore(connectionId).ConfigureAwait(false);
        return true;
    });

    /// <summary>Completes once every operation and timer callback queued so far has been applied.</summary>
    public Task SyncAsync() => Do(() => Task.FromResult(true));

    /// <summary>A read-only description of the room for diagnostics and tests.</summary>
    public Task<RoomInfo> InfoAsync() => Do(() => Task.FromResult(new RoomInfo(
        _lifecycle,
        _revision,
        _hostId,
        _members.Count,
        _members.Count(m => m.Status == ParticipantStatus.Connected),
        _table?.Snapshot.Paused ?? false,
        _table?.Snapshot.Version ?? -1,
        _table?.Snapshot.State?.DealNumber ?? 0)));

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.Writer.TryComplete();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        _idleTimer?.Dispose();
        foreach (var member in _members)
        {
            member.Timer?.Dispose();
        }

        _tableSubscription?.Dispose();
        if (_table is not null)
        {
            await _table.DisposeAsync().ConfigureAwait(false);
        }

        _ended.TrySetResult();
    }

    // =========================================================================================
    // Actor plumbing
    // =========================================================================================

    private Task<T> Do<T>(Func<Task<T>> body)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task> work = async () =>
        {
            try
            {
                done.SetResult(await body().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                done.TrySetException(ex);
            }
        };

        if (!_queue.Writer.TryWrite(work))
        {
            done.TrySetException(new ObjectDisposedException(nameof(Room)));
        }

        return done.Task;
    }

    private void Post(Func<Task> work) => _queue.Writer.TryWrite(work);

    private async Task RunAsync()
    {
        await foreach (var work in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await work().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Each work item reports its own failure to its caller; the room keeps running.
            }
        }
    }

    // =========================================================================================
    // Joining and attaching
    // =========================================================================================

    private async Task<RoomResult> JoinCore(
        string? token, string? name, string connectionId, IRoomSink sink, bool mustExist)
    {
        if (_lifecycle == RoomLifecycle.Ended)
        {
            return RoomResult.Fail(RoomRejections.Ended());
        }

        if (!ValidToken(token))
        {
            return RoomResult.Fail(RoomRejections.BadToken());
        }

        var member = Find(token!);
        if (member is null)
        {
            if (mustExist)
            {
                return RoomResult.Fail(RoomRejections.NotInRoom());
            }

            var admitted = Admit(token!, name);
            if (admitted.Rejection is not null)
            {
                return RoomResult.Fail(admitted.Rejection);
            }

            member = admitted.Member!;
            _members.Add(member);
            bool first = _members.Count == 1;
            if (first)
            {
                _hostId = member.Id;
                _slots[Seat.South] = new SeatSlot.Human(member.Id);
                member.Seat = Seat.South;
            }

            Attach(member, connectionId, sink);
            CancelIdle();
            return await FinishJoinAsync(member, new RoomEventDto("joined", SeatName(member), member.Name, null, 0))
                .ConfigureAwait(false);
        }

        var reclaimed = await AttachKnownAsync(member, connectionId, sink).ConfigureAwait(false);
        CancelIdle();
        return reclaimed;
    }

    private (Member? Member, Rejection? Rejection) Admit(string token, string? name)
    {
        if (_members.Count >= MaxParticipants || !HasRoomForNewcomer())
        {
            return (null, RoomRejections.Full());
        }

        var check = NameRules.Validate(name, _members.Select(m => m.Name), _options.MaxNameLength);
        if (!check.Ok)
        {
            return (null, check.Code == NameRules.TakenCode
                ? RoomRejections.NameTaken()
                : RoomRejections.NameInvalid());
        }

        return (new Member(Guid.NewGuid(), Hash(token), check.Name), null);
    }

    /// <summary>A newcomer needs something they could eventually sit on.</summary>
    private bool HasRoomForNewcomer()
    {
        if (_members.Count == 0)
        {
            return true;
        }

        return _lifecycle switch
        {
            RoomLifecycle.Lobby => _slots.Values.Any(s => s is not SeatSlot.Human),
            RoomLifecycle.Finished => _slots.Values.Any(s => s is not SeatSlot.Human),
            RoomLifecycle.InGame => _slots.Any(kv => IsOpenToNewcomer(kv.Key)),
            _ => false,
        };
    }

    private bool IsOpenToNewcomer(Seat seat) =>
        _slots[seat] is SeatSlot.Bot { Origin: BotOrigin.Default or BotOrigin.Left }
        && !_pending.ContainsKey(seat);

    private void Attach(Member member, string connectionId, IRoomSink sink)
    {
        var previous = member.Sink;
        var previousConnection = member.ConnectionId;
        member.ConnectionId = connectionId;
        member.Sink = sink;

        // Another connection of the same person is told it was taken over; the same connection
        // joining again is not "another tab".
        if (previous is not null && !ReferenceEquals(previous, sink) && previousConnection != connectionId)
        {
            previous.PushSuperseded();
        }
    }

    private async Task<RoomResult> AttachKnownAsync(Member member, string connectionId, IRoomSink sink)
    {
        Attach(member, connectionId, sink);
        member.Timer?.Dispose();
        member.Timer = null;
        member.TimerGeneration++;

        int? awayMoves = null;
        RoomEventDto? ev = null;
        var wasReconnecting = member.Status == ParticipantStatus.Reconnecting;

        if (member.Status == ParticipantStatus.Away
            && _lifecycle == RoomLifecycle.InGame
            && member.Seat is { } seat
            && _slots[seat] is SeatSlot.Bot { Origin: BotOrigin.Replaced } bot
            && bot.ReplacedParticipantId == member.Id)
        {
            _slots[seat] = new SeatSlot.Human(member.Id);
            await _table!.SetControllerAsync(seat, new HumanPrincipal(member.Id)).ConfigureAwait(false);
            awayMoves = member.AwayMoves;
            member.AwayMoves = 0;
            ev = new RoomEventDto("reclaimed", SeatName(member), member.Name, null, 0);
        }

        if (member.Seat is { } held
            && !(_slots[held] is SeatSlot.Human mine && mine.ParticipantId == member.Id))
        {
            member.Seat = null;
        }

        member.Status = ParticipantStatus.Connected;
        if (wasReconnecting)
        {
            _missing.Remove(member.Id);
        }

        // With nobody connected nobody was replaced; whoever returns first resumes, and anyone else whose
        // minute has already run out is replaced now.
        var now = _time.GetUtcNow();
        foreach (var other in _members.Where(m =>
                     m.Id != member.Id
                     && m.Status == ParticipantStatus.Reconnecting
                     && _missing.TryGetValue(m.Id, out var entry)
                     && entry.DeadlineUtc <= now).ToList())
        {
            await ReplaceWithBotAsync(other, "replaced", null).ConfigureAwait(false);
        }

        await UpdatePauseAsync().ConfigureAwait(false);

        // A lobby or between-games participant who dropped and came back keeps their seat.
        return await FinishJoinAsync(member, ev, awayMoves).ConfigureAwait(false);
    }

    private readonly Dictionary<Guid, PauseEntry> _missing = new();

    private async Task<RoomResult> FinishJoinAsync(Member member, RoomEventDto? ev, int? awayMoves = null)
    {
        PublishState(ev);
        TableUpdateDto? update = null;
        if (_table is not null && member.Seat is { } seat && _slots[seat] is SeatSlot.Human h && h.ParticipantId == member.Id)
        {
            update = _table.BuildUpdateFor(seat);
            if (update is not null)
            {
                member.Sink?.PushUpdate(update);
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return new RoomResult(true, null, BuildState(member), update, awayMoves);
    }

    // =========================================================================================
    // Seats and names
    // =========================================================================================

    private async Task<RoomResult> ClaimSeatCore(string? token, Seat seat)
    {
        var member = Authorize(token, out var fail);
        if (member is null)
        {
            return fail!;
        }

        bool betweenGames = _lifecycle is RoomLifecycle.Lobby or RoomLifecycle.Finished;
        var slot = _slots[seat];

        if (slot is SeatSlot.Human h && h.ParticipantId == member.Id)
        {
            return Ok(member);
        }

        if (betweenGames)
        {
            if (slot is SeatSlot.Human)
            {
                return RoomResult.Fail(RoomRejections.SeatTaken());
            }

            if (member.Seat is { } old)
            {
                _slots[old] = _lifecycle == RoomLifecycle.Lobby
                    ? SeatSlot.Free
                    : new SeatSlot.Bot(BotOrigin.Left);
            }

            _slots[seat] = new SeatSlot.Human(member.Id);
            member.Seat = seat;
            PublishState(new RoomEventDto("seated", SeatName(seat), member.Name, null, 0));
            return Ok(member);
        }

        if (_lifecycle != RoomLifecycle.InGame)
        {
            return RoomResult.Fail(RoomRejections.GameNotStarted());
        }

        // In a running game.
        if (slot is SeatSlot.Bot { Origin: BotOrigin.Replaced } replaced)
        {
            if (replaced.ReplacedParticipantId == member.Id && member.Status != ParticipantStatus.Away)
            {
                return Ok(member);
            }

            return RoomResult.Fail(RoomRejections.SeatTaken());
        }

        if (slot is SeatSlot.Human)
        {
            return RoomResult.Fail(RoomRejections.SeatTaken());
        }

        if (member.Seat is not null)
        {
            return RoomResult.Fail(RoomRejections.AlreadySeated());
        }

        if (_pending.ContainsKey(seat) || _pending.Values.Any(p => p.MemberId == member.Id))
        {
            return RoomResult.Fail(RoomRejections.SeatTaken());
        }

        int deal = _previous?.State?.DealNumber ?? _table?.Snapshot.State?.DealNumber ?? 0;
        _pending[seat] = new PendingClaim(member.Id, deal);
        PublishState(new RoomEventDto("joiningNextHand", SeatName(seat), member.Name, null, 0));
        return new RoomResult(true, null, BuildState(member), PendingNextHand: true);
    }

    private Task<RoomResult> ReleaseSeatCore(string? token)
    {
        var member = Authorize(token, out var fail);
        if (member is null)
        {
            return Task.FromResult(fail!);
        }

        if (_lifecycle == RoomLifecycle.InGame)
        {
            return Task.FromResult(RoomResult.Fail(RoomRejections.GameInProgress()));
        }

        if (member.Seat is { } seat)
        {
            _slots[seat] = _lifecycle == RoomLifecycle.Lobby ? SeatSlot.Free : new SeatSlot.Bot(BotOrigin.Left);
            member.Seat = null;
            PublishState(new RoomEventDto("stoodUp", SeatName(seat), member.Name, null, 0));
        }

        return Task.FromResult(Ok(member));
    }

    private Task<RoomResult> SetNameCore(string? token, string? name)
    {
        var member = Authorize(token, out var fail);
        if (member is null)
        {
            return Task.FromResult(fail!);
        }

        var others = _members.Where(m => m.Id != member.Id).Select(m => m.Name);
        var check = NameRules.Validate(name, others, _options.MaxNameLength);
        if (!check.Ok)
        {
            return Task.FromResult(RoomResult.Fail(
                check.Code == NameRules.TakenCode ? RoomRejections.NameTaken() : RoomRejections.NameInvalid()));
        }

        if (!string.Equals(member.Name, check.Name, StringComparison.Ordinal))
        {
            member.Name = check.Name;
            PublishState(new RoomEventDto("nameChanged", member.Seat is { } s ? JsonNames.Of(s) : null, member.Name, null, 0));
        }

        return Task.FromResult(Ok(member));
    }

    // =========================================================================================
    // Starting, playing, finishing
    // =========================================================================================

    private async Task<RoomResult> StartGameCore(string? token)
    {
        var member = Authorize(token, out var fail);
        if (member is null)
        {
            return fail!;
        }

        if (_hostId != member.Id)
        {
            return RoomResult.Fail(RoomRejections.NotHost());
        }

        if (_lifecycle == RoomLifecycle.InGame)
        {
            return RoomResult.Fail(RoomRejections.GameInProgress());
        }

        if (_lifecycle != RoomLifecycle.Lobby || !_slots.Values.Any(s => s is SeatSlot.Human))
        {
            return RoomResult.Fail(RoomRejections.GameNotStarted());
        }

        await BeginGameAsync("gameStarted").ConfigureAwait(false);
        return Ok(member);
    }

    private async Task<RoomResult> PlayAgainCore(string? token)
    {
        var member = Authorize(token, out var fail);
        if (member is null)
        {
            return fail!;
        }

        if (_hostId != member.Id)
        {
            return RoomResult.Fail(RoomRejections.NotHost());
        }

        if (_lifecycle != RoomLifecycle.Finished)
        {
            return RoomResult.Fail(_lifecycle == RoomLifecycle.InGame
                ? RoomRejections.GameInProgress()
                : RoomRejections.GameNotStarted());
        }

        await BeginGameAsync("gameStarted").ConfigureAwait(false);
        return Ok(member);
    }

    private async Task BeginGameAsync(string eventKind)
    {
        _tableSubscription?.Dispose();
        _tableSubscription = null;
        if (_table is not null)
        {
            await _table.DisposeAsync().ConfigureAwait(false);
            _table = null;
        }

        _missing.Clear();
        _pending.Clear();
        foreach (var seat in Seats.All)
        {
            if (_slots[seat] is not SeatSlot.Human)
            {
                _slots[seat] = new SeatSlot.Bot(BotOrigin.Default);
            }
        }

        // Members whose seat became a bot (e.g. a replaced player who never came back) are unseated.
        foreach (var m in _members)
        {
            if (m.Seat is { } s && !(_slots[s] is SeatSlot.Human h && h.ParticipantId == m.Id))
            {
                m.Seat = null;
            }

            m.AwayMoves = 0;
            if (m.Status == ParticipantStatus.Away)
            {
                m.Status = ParticipantStatus.Offline;
            }
        }

        var controllers = new Dictionary<Seat, Principal>();
        foreach (var seat in Seats.All)
        {
            controllers[seat] = _slots[seat] is SeatSlot.Human human
                ? new HumanPrincipal(human.ParticipantId)
                : new BotPrincipal(seat);
        }

        var options = new TableOptions
        {
            Seed = _settings.SeedSource(),
            BotDelay = _settings.BotDelay,
            TimeProvider = _time,
        };
        var table = LiveTable.CreateStarted(options, controllers, _botFactory, _onFinished);
        _table = table;
        _previous = table.Snapshot;
        _lifecycle = RoomLifecycle.InGame;
        _tableSubscription = table.SubscribeSnapshots(new RoomObserver(this));

        PublishState(new RoomEventDto(eventKind, null, null, null, 0));
        PushUpdates(table.Snapshot);

        // Members who are not connected right now hold a human seat: the game starts paused for them.
        foreach (var m in _members.Where(m => m.Seat is not null && m.Sink is null).ToList())
        {
            MarkMissing(m);
        }

        await UpdatePauseAsync().ConfigureAwait(false);
        SyncHostAfterChange();
    }

    private async Task<IntentOutcome> SubmitCore(string? token, IntentDto? dto)
    {
        var member = Authorize(token, out var fail);
        if (member is null)
        {
            return new IntentRejected(fail!.Rejection!);
        }

        if (_table is null || _lifecycle != RoomLifecycle.InGame)
        {
            return new IntentRejected(RoomRejections.GameNotStarted());
        }

        if (member.Seat is not { } seat || _slots[seat] is not SeatSlot.Human h || h.ParticipantId != member.Id)
        {
            return new IntentRejected(Rejection.NotSeated("You do not hold a seat in this game."));
        }

        return await _table.SubmitAsync(new HumanPrincipal(member.Id), dto!).ConfigureAwait(false);
    }

    private Task<RoomResult> GetViewCore(string? token)
    {
        var member = Authorize(token, out var fail);
        if (member is null)
        {
            return Task.FromResult(fail!);
        }

        if (_table is null || member.Seat is not { } seat)
        {
            return Task.FromResult(RoomResult.Fail(RoomRejections.GameNotStarted()));
        }

        return Task.FromResult(new RoomResult(true, null, BuildState(member), _table.BuildUpdateFor(seat)));
    }

    /// <summary>Called by the table observer (inside the table actor); only enqueues.</summary>
    private void OnSnapshot(TableSnapshot snapshot) => Post(() => ApplySnapshotAsync(snapshot));

    private async Task ApplySnapshotAsync(TableSnapshot snapshot)
    {
        if (_table is null || _lifecycle == RoomLifecycle.Ended)
        {
            return;
        }

        var previous = _previous;
        _previous = snapshot;

        // Count the moves a bot makes for a replaced human, for the "what you missed" note.
        if (previous?.State is { } before && snapshot.Version == previous.Version + 1)
        {
            if (_slots[before.ToAct] is SeatSlot.Bot { Origin: BotOrigin.Replaced } bot
                && bot.ReplacedParticipantId is { } replacedId
                && _members.FirstOrDefault(m => m.Id == replacedId) is { } away)
            {
                away.AwayMoves++;
            }
        }

        // Late joiners take over at the start of the next hand.
        bool changed = false;
        if (snapshot.State is { } state)
        {
            foreach (var (seat, claim) in _pending.ToList())
            {
                if (state.DealNumber <= claim.DealAtClaim)
                {
                    continue;
                }

                _pending.Remove(seat);
                var claimant = _members.FirstOrDefault(m => m.Id == claim.MemberId);
                if (claimant is null || claimant.Seat is not null || !IsBotSeatOpen(seat))
                {
                    continue;
                }

                _slots[seat] = new SeatSlot.Human(claimant.Id);
                claimant.Seat = seat;
                await _table.SetControllerAsync(seat, new HumanPrincipal(claimant.Id)).ConfigureAwait(false);
                PublishState(new RoomEventDto("joined", JsonNames.Of(seat), claimant.Name, null, 0));
                if (_table.BuildUpdateFor(seat, snapshot) is { } own)
                {
                    claimant.Sink?.PushUpdate(own);
                }

                changed = true;
            }
        }

        PushUpdates(snapshot);

        if (snapshot.Lifecycle == TableLifecycle.Finished && _lifecycle == RoomLifecycle.InGame)
        {
            _lifecycle = RoomLifecycle.Finished;
            _missing.Clear();
            _pending.Clear();
            foreach (var seat in Seats.All)
            {
                if (_slots[seat] is SeatSlot.Bot)
                {
                    _slots[seat] = new SeatSlot.Bot(BotOrigin.Left);
                }
            }

            foreach (var m in _members)
            {
                m.Timer?.Dispose();
                m.Timer = null;
                if (m.Status is ParticipantStatus.Reconnecting or ParticipantStatus.Away)
                {
                    m.Status = ParticipantStatus.Offline;
                    m.Seat = null;
                    ScheduleRelease(m);
                }
            }

            PublishState(new RoomEventDto("gameFinished", null, null, null, 0));
            return;
        }

        if (changed)
        {
            SyncHostAfterChange();
        }
    }

    private bool IsBotSeatOpen(Seat seat) =>
        _slots[seat] is SeatSlot.Bot { Origin: BotOrigin.Default or BotOrigin.Left };

    private void PushUpdates(TableSnapshot snapshot)
    {
        if (_table is null)
        {
            return;
        }

        foreach (var member in _members)
        {
            if (member.Sink is null || member.Seat is not { } seat)
            {
                continue;
            }

            if (_slots[seat] is SeatSlot.Human h && h.ParticipantId == member.Id
                && _table.BuildUpdateFor(seat, snapshot) is { } update)
            {
                member.Sink.PushUpdate(update);
            }
        }
    }

    // =========================================================================================
    // Disconnects, the pause, replacement and skipping
    // =========================================================================================

    private async Task DisconnectedCore(string connectionId)
    {
        var member = _members.FirstOrDefault(m => m.ConnectionId == connectionId);
        if (member is null)
        {
            return;
        }

        member.ConnectionId = null;
        member.Sink = null;
        if (member.Status is ParticipantStatus.Away or ParticipantStatus.Reconnecting or ParticipantStatus.Offline)
        {
            return;
        }

        bool holdsHumanSeat = member.Seat is { } s && _slots[s] is SeatSlot.Human h && h.ParticipantId == member.Id;
        if (_lifecycle == RoomLifecycle.InGame && holdsHumanSeat)
        {
            MarkMissing(member);
            await UpdatePauseAsync().ConfigureAwait(false);
            PublishState(null);
        }
        else
        {
            member.Status = ParticipantStatus.Offline;
            ScheduleRelease(member);
            PublishState(null);
        }

        StartIdleIfEmpty();
    }

    private void ScheduleRelease(Member member)
    {
        member.Timer?.Dispose();
        int generation = ++member.TimerGeneration;
        var id = member.Id;
        member.Timer = _time.CreateTimer(
            _ => Post(() => ReleaseOfflineAsync(id, generation)),
            null,
            _options.LobbySeatRelease,
            Timeout.InfiniteTimeSpan);
    }

    private void MarkMissing(Member member)
    {
        var seat = member.Seat!.Value;
        member.Status = ParticipantStatus.Reconnecting;
        member.Timer?.Dispose();
        int generation = ++member.TimerGeneration;
        var deadline = _time.GetUtcNow() + _options.Grace;
        _missing[member.Id] = new PauseEntry(member.Id, seat, deadline);
        var id = member.Id;
        member.Timer = _time.CreateTimer(
            _ => Post(() => DeadlineAsync(id, generation)),
            null,
            _options.Grace,
            Timeout.InfiniteTimeSpan);
    }

    private async Task DeadlineAsync(Guid memberId, int generation)
    {
        var member = _members.FirstOrDefault(m => m.Id == memberId);
        if (member is null || member.TimerGeneration != generation || member.Status != ParticipantStatus.Reconnecting)
        {
            return;
        }

        // Nobody is here to play with: keep the table paused for whoever returns first.
        if (!_members.Any(m => m.Sink is not null))
        {
            return;
        }

        await ReplaceWithBotAsync(member, "replaced", null).ConfigureAwait(false);
        PublishState(null);
    }

    private async Task ReleaseOfflineAsync(Guid memberId, int generation)
    {
        var member = _members.FirstOrDefault(m => m.Id == memberId);
        if (member is null || member.TimerGeneration != generation || member.Status != ParticipantStatus.Offline)
        {
            return;
        }

        if (_lifecycle == RoomLifecycle.InGame && member.Seat is not null)
        {
            return;
        }

        RemoveMember(member, "left");
        SyncHostAfterChange();
        await UpdatePauseAsync().ConfigureAwait(false);
        PublishState(null);
    }

    private async Task ReplaceWithBotAsync(Member member, string eventKind, string? byName)
    {
        member.Timer?.Dispose();
        member.Timer = null;
        member.TimerGeneration++;
        _missing.Remove(member.Id);
        var seat = member.Seat!.Value;
        member.Status = ParticipantStatus.Away;
        member.AwayMoves = 0;
        _slots[seat] = new SeatSlot.Bot(BotOrigin.Replaced, member.Id);
        if (_table is not null)
        {
            await _table.SetControllerAsync(seat, new BotPrincipal(seat)).ConfigureAwait(false);
        }

        _lastEvent = new RoomEventDto(eventKind, JsonNames.Of(seat), member.Name, byName, _revision + 1);
        if (_hostId == member.Id)
        {
            SyncHostAfterChange();
        }

        await UpdatePauseAsync().ConfigureAwait(false);
    }

    private async Task<RoomResult> SkipWaitCore(string? token, Guid? target)
    {
        var member = Authorize(token, out var fail);
        if (member is null)
        {
            return fail!;
        }

        if (_lifecycle != RoomLifecycle.InGame || _missing.Count == 0)
        {
            return RoomResult.Fail(RoomRejections.NotMissing());
        }

        if (_missing.ContainsKey(member.Id) || member.Status != ParticipantStatus.Connected)
        {
            return RoomResult.Fail(RoomRejections.NotMissing());
        }

        var victims = _members
            .Where(m => _missing.ContainsKey(m.Id) && (target is null || m.Id == target))
            .ToList();
        if (victims.Count == 0)
        {
            return RoomResult.Fail(RoomRejections.NotMissing());
        }

        foreach (var victim in victims)
        {
            await ReplaceWithBotAsync(victim, "skipped", member.Name).ConfigureAwait(false);
        }

        PublishState(null);
        return Ok(member);
    }

    private async Task UpdatePauseAsync()
    {
        if (_table is null || _lifecycle != RoomLifecycle.InGame)
        {
            return;
        }

        await _table.SetPausedAsync(_missing.Count > 0).ConfigureAwait(false);
    }

    // =========================================================================================
    // Leaving, host, idle expiry
    // =========================================================================================

    private async Task<RoomResult> LeaveCore(string? token)
    {
        var member = Authorize(token, out var fail);
        if (member is null)
        {
            return fail!;
        }

        var seat = member.Seat;
        bool wasMissing = _missing.ContainsKey(member.Id);
        var name = member.Name;
        var sink = member.Sink;

        if (seat is { } s && _lifecycle == RoomLifecycle.InGame
            && _slots[s] is SeatSlot.Human or SeatSlot.Bot { Origin: BotOrigin.Replaced })
        {
            _slots[s] = new SeatSlot.Bot(BotOrigin.Left);
            if (_table is not null)
            {
                await _table.SetControllerAsync(s, new BotPrincipal(s)).ConfigureAwait(false);
            }
        }

        RemoveMember(member, "left");
        sink?.PushClosed("left");
        SyncHostAfterChange();
        if (wasMissing || _lifecycle == RoomLifecycle.InGame)
        {
            await UpdatePauseAsync().ConfigureAwait(false);
        }

        PublishState(new RoomEventDto("left", seat is { } x ? JsonNames.Of(x) : null, name, null, 0));
        StartIdleIfEmpty();
        return new RoomResult(true);
    }

    private void RemoveMember(Member member, string eventKind)
    {
        member.Timer?.Dispose();
        member.Timer = null;
        member.TimerGeneration++;
        _missing.Remove(member.Id);
        foreach (var (seat, claim) in _pending.ToList())
        {
            if (claim.MemberId == member.Id)
            {
                _pending.Remove(seat);
            }
        }

        if (member.Seat is { } s && _slots[s] is SeatSlot.Human h && h.ParticipantId == member.Id)
        {
            _slots[s] = _lifecycle == RoomLifecycle.Lobby ? SeatSlot.Free : new SeatSlot.Bot(BotOrigin.Left);
        }
        else if (member.Seat is { } rs && _slots[rs] is SeatSlot.Bot { Origin: BotOrigin.Replaced } r
                 && r.ReplacedParticipantId == member.Id)
        {
            _slots[rs] = new SeatSlot.Bot(BotOrigin.Left);
        }

        _members.Remove(member);
        _lastEvent = new RoomEventDto(eventKind, member.Seat is { } ms ? JsonNames.Of(ms) : null, member.Name, null, _revision + 1);
    }

    /// <summary>Keeps <see cref="_hostId"/> on a person who can act: connected first, then in-grace.</summary>
    private void SyncHostAfterChange()
    {
        var host = _hostId is { } id ? _members.FirstOrDefault(m => m.Id == id) : null;
        bool hostUsable = host is not null && host.Status is ParticipantStatus.Connected or ParticipantStatus.Reconnecting;
        if (hostUsable)
        {
            return;
        }

        var next = _members.FirstOrDefault(m => m.Status == ParticipantStatus.Connected && m.Seat is not null)
                   ?? _members.FirstOrDefault(m => m.Status == ParticipantStatus.Connected)
                   ?? _members.FirstOrDefault(m => m.Status == ParticipantStatus.Reconnecting);
        if (next is not null && next.Id != _hostId)
        {
            _hostId = next.Id;
            _lastEvent = new RoomEventDto("hostChanged", next.Seat is { } s ? JsonNames.Of(s) : null, next.Name, null, _revision + 1);
        }
    }

    private void StartIdleIfEmpty()
    {
        bool anyConnected = _members.Any(m => m.Sink is not null);
        if (anyConnected || _idleTimer is not null || _lifecycle == RoomLifecycle.Ended)
        {
            return;
        }

        int generation = ++_timerGeneration;
        _idleTimer = _time.CreateTimer(
            _ => Post(() => ExpireAsync(generation)),
            null,
            _options.IdleExpiry,
            Timeout.InfiniteTimeSpan);
    }

    private void CancelIdle()
    {
        _idleTimer?.Dispose();
        _idleTimer = null;
        _timerGeneration++;
    }

    private async Task ExpireAsync(int generation)
    {
        if (generation != _timerGeneration || _lifecycle == RoomLifecycle.Ended)
        {
            return;
        }

        if (_members.Any(m => m.Sink is not null))
        {
            CancelIdle();
            return;
        }

        _lifecycle = RoomLifecycle.Ended;
        foreach (var member in _members)
        {
            member.Timer?.Dispose();
            member.Sink?.PushClosed("ended");
        }

        _tableSubscription?.Dispose();
        _tableSubscription = null;
        if (_table is not null)
        {
            await _table.DisposeAsync().ConfigureAwait(false);
            _table = null;
        }

        _ended.TrySetResult();
    }

    // =========================================================================================
    // State publication
    // =========================================================================================

    private void PublishState(RoomEventDto? ev)
    {
        _revision++;
        if (ev is not null)
        {
            _lastEvent = ev with { Revision = _revision };
        }
        else if (_lastEvent is not null && _lastEvent.Revision > _revision)
        {
            _lastEvent = _lastEvent with { Revision = _revision };
        }

        foreach (var member in _members)
        {
            member.Sink?.PushRoom(BuildState(member));
        }
    }

    private RoomStateDto BuildState(Member viewer)
    {
        var now = _time.GetUtcNow();
        var seats = new List<RoomSeatDto>();
        foreach (var seat in SeatOrder)
        {
            var name = JsonNames.Of(seat);
            switch (_slots[seat])
            {
                case SeatSlot.Human h:
                    var m = _members.First(x => x.Id == h.ParticipantId);
                    seats.Add(new RoomSeatDto(
                        name,
                        "human",
                        m.Id.ToString("D"),
                        m.Name,
                        ConnectionName(m),
                        _missing.TryGetValue(m.Id, out var p) ? p.DeadlineUtc : null,
                        null,
                        null));
                    break;
                case SeatSlot.Bot b:
                    var replaced = b.ReplacedParticipantId is { } rid
                        ? _members.FirstOrDefault(x => x.Id == rid)
                        : null;
                    seats.Add(new RoomSeatDto(
                        name,
                        "bot",
                        null,
                        "Bot",
                        null,
                        null,
                        replaced?.Name,
                        b.Origin.ToString().ToLowerInvariant()));
                    break;
                default:
                    seats.Add(new RoomSeatDto(name, "free", null, null, null, null, null, null));
                    break;
            }
        }

        var missing = _missing.Values
            .OrderBy(e => e.DeadlineUtc)
            .Select(e => new MissingDto(
                e.ParticipantId.ToString("D"),
                JsonNames.Of(e.Seat),
                _members.First(x => x.Id == e.ParticipantId).Name,
                e.DeadlineUtc))
            .ToArray();

        var pending = _pending
            .Select(kv => new PendingTakeoverDto(
                JsonNames.Of(kv.Key),
                _members.FirstOrDefault(x => x.Id == kv.Value.MemberId)?.Name ?? string.Empty))
            .ToArray();

        return new RoomStateDto(
            TableUpdateDto.CurrentProtocolVersion,
            Id.ToString("D"),
            _revision,
            now,
            _lifecycle.ToString().ToLowerInvariant(),
            (_hostId ?? Guid.Empty).ToString("D"),
            new RoomYouDto(
                viewer.Id.ToString("D"),
                viewer.Seat is { } vs ? JsonNames.Of(vs) : null,
                _hostId == viewer.Id),
            seats,
            missing,
            pending,
            _lastEvent);
    }

    private static string ConnectionName(Member m) => m.Status switch
    {
        ParticipantStatus.Connected => "connected",
        ParticipantStatus.Reconnecting => "reconnecting",
        ParticipantStatus.Away => "away",
        _ => "offline",
    };

    // =========================================================================================
    // Helpers
    // =========================================================================================

    private static bool ValidToken(string? token) =>
        !string.IsNullOrEmpty(token) && token.Length is >= 16 and <= 128;

    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private Member? Find(string token)
    {
        var hash = Hash(token);
        Member? found = null;
        foreach (var member in _members)
        {
            // Compare every member so timing does not reveal which one matched.
            if (CryptographicOperations.FixedTimeEquals(member.TokenHash, hash))
            {
                found = member;
            }
        }

        return found;
    }

    private Member? Authorize(string? token, out RoomResult? failure)
    {
        failure = null;
        if (_lifecycle == RoomLifecycle.Ended)
        {
            failure = RoomResult.Fail(RoomRejections.Ended());
            return null;
        }

        if (!ValidToken(token))
        {
            failure = RoomResult.Fail(RoomRejections.BadToken());
            return null;
        }

        var member = Find(token!);
        if (member is null)
        {
            failure = RoomResult.Fail(RoomRejections.NotInRoom());
        }

        return member;
    }

    private RoomResult Ok(Member member) => new(true, null, BuildState(member));

    private static string SeatName(Seat seat) => JsonNames.Of(seat);

    private static string? SeatName(Member member) => member.Seat is { } s ? JsonNames.Of(s) : null;

    private sealed class Member
    {
        public Member(Guid id, byte[] tokenHash, string name)
        {
            Id = id;
            TokenHash = tokenHash;
            Name = name;
        }

        public Guid Id { get; }

        public byte[] TokenHash { get; }

        public string Name { get; set; }

        public Seat? Seat { get; set; }

        public ParticipantStatus Status { get; set; } = ParticipantStatus.Connected;

        public string? ConnectionId { get; set; }

        public IRoomSink? Sink { get; set; }

        public ITimer? Timer { get; set; }

        public int TimerGeneration { get; set; }

        public int AwayMoves { get; set; }
    }

    private sealed record PendingClaim(Guid MemberId, int DealAtClaim);

    private sealed class RoomObserver : ISnapshotObserver
    {
        private readonly Room _room;

        public RoomObserver(Room room) => _room = room;

        public ValueTask OnSnapshotAsync(TableSnapshot snapshot)
        {
            _room.OnSnapshot(snapshot);
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>A small read-only description of a room.</summary>
public sealed record RoomInfo(
    RoomLifecycle Lifecycle,
    int Revision,
    Guid? HostId,
    int Members,
    int ConnectedMembers,
    bool TablePaused,
    int TableVersion,
    int DealNumber);
