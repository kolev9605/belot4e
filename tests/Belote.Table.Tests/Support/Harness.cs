using System.Threading.Channels;
using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

/// <summary>Records every update and lets a driver await them without blocking the table.</summary>
internal sealed class Recorder : ITableObserver
{
    private readonly Channel<TableUpdateDto> _channel = Channel.CreateUnbounded<TableUpdateDto>();
    private readonly List<TableUpdateDto> _all = new();
    private readonly List<int> _eventCounts = new();
    private LiveTable? _table;
    private readonly object _lock = new();
    private readonly TimeSpan _delay;

    public Recorder(TimeSpan? callbackDelay = null) => _delay = callbackDelay ?? TimeSpan.Zero;

    public ChannelReader<TableUpdateDto> Reader => _channel.Reader;

    /// <summary>When attached, records how many log events existed when each update was published.</summary>
    public void Attach(LiveTable table) => _table = table;

    public IReadOnlyList<int> EventCounts
    {
        get
        {
            lock (_lock)
            {
                return _eventCounts.ToArray();
            }
        }
    }

    public IReadOnlyList<TableUpdateDto> Updates
    {
        get
        {
            lock (_lock)
            {
                return _all.ToArray();
            }
        }
    }

    public async ValueTask OnUpdateAsync(TableUpdateDto update)
    {
        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay);
        }

        lock (_lock)
        {
            _all.Add(update);
            _eventCounts.Add(_table?.Snapshot.Events.Count ?? -1);
        }

        _channel.Writer.TryWrite(update);
    }
}

internal static class Policies
{
    /// <summary>Always the first legal action the server offers.</summary>
    public static Func<TableUpdateDto, IntentDto> First { get; } = update => update.View.LegalActions[0];

    /// <summary>The 001 baseline policy for South: uniform choice from the seat seeded stream.</summary>
    public static Func<TableUpdateDto, IntentDto> SeededSouth(ulong seed)
    {
        var random = new SeededRandom(Seeds.ForSeat(seed, Seat.South));
        return update => update.View.LegalActions[random.NextInt(update.View.LegalActions.Count)];
    }
}

internal sealed record GameOutcome(
    IReadOnlyList<TableUpdateDto> Updates,
    IReadOnlyList<(IntentDto Intent, Rejection Rejection)> Rejections,
    FinishedGame? Finished);

internal static class Harness
{
    /// <summary>Wire JSON of any DTO; DTOs hold arrays, so tests compare this rather than record equality.</summary>
    public static string Json(object? value) => System.Text.Json.JsonSerializer.Serialize(value, WireJson.Options);

    public static readonly Principal Client = new ConnectionPrincipal("test-client");

    public static LiveTable NewTable(
        ulong seed,
        TimeSpan? delay = null,
        TimeProvider? time = null,
        Func<Seat, IPlayer>? bots = null,
        RuleSet? rules = null)
    {
        var options = new TableOptions
        {
            Seed = seed,
            BotDelay = delay ?? TimeSpan.Zero,
            TimeProvider = time ?? TimeProvider.System,
            RuleSet = rules ?? RuleSets.Bulgarian,
        };
        return LiveTable.Create(options, Client, bots);
    }

    /// <summary>Seats South and plays every South turn with <paramref name="choose"/> until the game ends.</summary>
    public static async Task<GameOutcome> PlayToEndAsync(
        LiveTable table,
        Recorder recorder,
        Func<TableUpdateDto, IntentDto> choose,
        TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(60));
        var seated = await table.TakeSeatAsync(Client);
        Assert.True(seated.Ok, seated.Rejection?.Detail);

        var rejections = new List<(IntentDto, Rejection)>();
        await foreach (var update in recorder.Reader.ReadAllAsync(cts.Token))
        {
            if (update.Finished)
            {
                break;
            }

            if (update.View.LegalActions.Count == 0)
            {
                continue;
            }

            var intent = choose(update);
            var outcome = await table.SubmitAsync(Client, intent);
            if (outcome is IntentRejected r)
            {
                rejections.Add((intent, r.Rejection));
                throw new InvalidOperationException(
                    $"Legal intent rejected at version {update.Version}: {r.Rejection.Code}: {r.Rejection.Detail}");
            }
        }

        var finished = await table.Completion.WaitAsync(cts.Token);
        return new GameOutcome(recorder.Updates, rejections, finished);
    }
}
