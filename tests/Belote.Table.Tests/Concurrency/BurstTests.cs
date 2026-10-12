using Belote.Core;
using Belote.Simulation;
using Belote.Table;

namespace Belote.Table.Tests;

public class BurstTests
{
    private static async Task<TableUpdateDto> ReachSouthPlayingTurnAsync(LiveTable table, Recorder recorder)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await table.TakeSeatAsync(Harness.Client);
        await foreach (var update in recorder.Reader.ReadAllAsync(cts.Token))
        {
            if (update.View.LegalActions.Count == 0)
            {
                continue;
            }

            if (update.View.Phase == "playing" && update.View.OwnHand.Count >= 6)
            {
                return update;
            }

            var outcome = await table.SubmitAsync(Harness.Client, update.View.LegalActions[0]);
            Assert.IsType<IntentAccepted>(outcome);
        }

        throw new InvalidOperationException("Never reached a playing turn.");
    }

    [Fact]
    public async Task FR014_DuplicateCardBurst_ExactlyOneIsAccepted()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            await using var table = Harness.NewTable(seed);
            var recorder = new Recorder();
            using var _ = table.Subscribe(recorder);
            var update = await ReachSouthPlayingTurnAsync(table, recorder);
            var card = update.View.LegalActions.First(a => a.Kind == "play");

            var burst = Enumerable.Range(0, 16)
                .Select(_ => Task.Run(async () => await table.SubmitAsync(Harness.Client, card)))
                .ToArray();
            var outcomes = await Task.WhenAll(burst);

            Assert.Single(outcomes.OfType<IntentAccepted>());
            Assert.Equal(15, outcomes.OfType<IntentRejected>().Count());
            // Evaluated against the state the accepted play left: not South's turn, or the card is gone.
            Assert.All(outcomes.OfType<IntentRejected>(), r =>
                Assert.Contains(r.Rejection.Code, new[] { "NotYourTurn", "CardNotInHand" }));
        }
    }

    [Fact]
    public async Task FR014_MixedBurstEachGetsOneOutcomeAndStateMatchesASequentialReplay()
    {
        await using var table = Harness.NewTable(12);
        var recorder = new Recorder();
        recorder.Attach(table);
        using var _ = table.Subscribe(recorder);
        var update = await ReachSouthPlayingTurnAsync(table, recorder);
        var legal = update.View.LegalActions.Where(a => a.Kind == "play").ToArray();

        var intents = new List<IntentDto>();
        intents.AddRange(legal);
        intents.AddRange(legal);
        intents.Add(new IntentDto("pass", "north"));
        intents.Add(new IntentDto("play", "east", Card: "AS"));
        intents.Add(new IntentDto("nonsense", "south"));
        intents.Add(new IntentDto("play", "south", Card: "7C"));

        var tasks = intents.Select(i => Task.Run(async () => (i, await table.SubmitAsync(Harness.Client, i)))).ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(intents.Count, results.Length);
        var accepted = results.Where(r => r.Item2 is IntentAccepted).ToArray();
        Assert.NotEmpty(accepted);

        // Wait for bots to settle, then the table state must equal a sequential replay of its own log.
        await Task.Delay(100);
        var snap = table.Snapshot;
        Assert.NotEqual(TableLifecycle.Faulted, snap.Lifecycle);
        var (state, events) = Replay.Run(RuleSets.Bulgarian, 12, snap.Events);
        Assert.Equal(Lines.Of(snap.Events), Lines.Of(events));
        Assert.Equal(snap.State!.MatchScore.ToArray(), state.MatchScore.ToArray());
        Assert.Equal(snap.State.ToAct, state.ToAct);
        Assert.Equal(snap.Version, recorder.Updates[^1].Version);
    }

    [Fact]
    public async Task ClientIntentsRacingWithBots_NeverCorruptTheGame()
    {
        var games = Enumerable.Range(1, 8).Select(i => (ulong)(100 + i)).Select(async seed =>
        {
            await using var table = Harness.NewTable(seed);
            var recorder = new Recorder();
            using var sub = table.Subscribe(recorder);
            using var stop = new CancellationTokenSource();

            // A spammer that can never be legal: another seat's intents and malformed ones.
            var spam = Task.Run(async () =>
            {
                var junk = new[]
                {
                    new IntentDto("pass", "north"),
                    new IntentDto("play", "west", Card: "AS"),
                    new IntentDto("bid", "east", "hearts"),
                    new IntentDto("bogus", "south"),
                };
                int n = 0;
                while (!stop.IsCancellationRequested)
                {
                    var outcome = await table.SubmitAsync(Harness.Client, junk[n++ % junk.Length]);
                    Assert.IsType<IntentRejected>(outcome);
                    await Task.Yield();
                }
            });

            var played = await Harness.PlayToEndAsync(table, recorder, Policies.SeededSouth(seed));
            stop.Cancel();
            await spam;

            Assert.NotEqual(TableLifecycle.Faulted, table.Snapshot.Lifecycle);
            var versions = played.Updates.Select(u => u.Version).ToArray();
            Assert.Equal(Enumerable.Range(0, versions.Length), versions);
            var audit = GameAuditor.Audit(played.Finished!.Log);
            Assert.True(audit.Ok, audit.First?.ToString());
        });

        await Task.WhenAll(games);
    }
}
