using Belote.Core;
using Belote.Simulation;
using Belote.Table;

namespace Belote.Table.Tests;

public class ReplayAndSimulationTests
{
    [Fact]
    public async Task FR024_ReplayingTheRecordedLogReproducesTheFinalState()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            await using var table = Harness.NewTable(seed);
            var recorder = new Recorder();
            using var _ = table.Subscribe(recorder);
            var outcome = await Harness.PlayToEndAsync(table, recorder, Policies.SeededSouth(seed));

            var log = outcome.Finished!.Log;
            var audit = GameAuditor.Audit(log);
            Assert.True(audit.Ok, $"seed {seed}: {audit.First}");

            var (state, events) = Replay.Run(log.RuleSet, log.Seed, log.Events);
            Assert.Equal(Lines.Of(log.Events), Lines.Of(events));
            Assert.Equal(Phase.Finished, state.Phase);
            Assert.Equal(table.Snapshot.State!.MatchScore.ToArray(), state.MatchScore.ToArray());
            Assert.Equal(table.Snapshot.State.Winner, state.Winner);

            // The persisted JSON Lines form round-trips and audits clean too.
            var again = GameAuditor.AuditJsonLines(log.ToJsonLines());
            Assert.True(again.Ok, again.First?.ToString());
        }
    }

    [Fact]
    public async Task ClarificationQ1_LiveGameEqualsThe001SimulationMoveForMove()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            await using var table = Harness.NewTable(seed);
            var recorder = new Recorder();
            using var _ = table.Subscribe(recorder);
            var live = await Harness.PlayToEndAsync(table, recorder, Policies.SeededSouth(seed));

            var simulated = await GameRunner.RunAsync(
                RuleSets.Bulgarian, seed, SeatPresets.Create(SeatPresets.SmartTable, seed));

            Assert.True(simulated.Finished, simulated.FailureDetail);
            Assert.Equal(Lines.Of(simulated.Log.Events), Lines.Of(live.Finished!.Log.Events));
            Assert.Equal(simulated.NorthSouth, live.Finished.NorthSouth);
            Assert.Equal(simulated.EastWest, live.Finished.EastWest);
            Assert.Equal(simulated.Winner, live.Finished.Winner);
        }
    }

    [Fact]
    public async Task FR013_FR023_LogCarriesSeedRuleSetAndSeatsAndNeverRejections()
    {
        await using var table = Harness.NewTable(33);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);

        // Junk intents interleaved with the game must leave no trace in the record.
        var outcome = await Harness.PlayToEndAsync(table, recorder, update =>
        {
            Assert.IsType<IntentRejected>(table.SubmitAsync(Harness.Client, new IntentDto("nonsense", "south")).AsTask().Result);
            Assert.IsType<IntentRejected>(table.SubmitAsync(Harness.Client, new IntentDto("pass", "north")).AsTask().Result);
            return update.View.LegalActions[0];
        });

        var log = outcome.Finished!.Log;
        Assert.Equal(33UL, log.Seed);
        Assert.Equal(RuleSets.Bulgarian.Name, log.RuleSet.Name);
        Assert.Equal("RemoteClient", log.Seats[Seat.South]);
        Assert.All(new[] { Seat.North, Seat.East, Seat.West }, s => Assert.Equal("SmartBot", log.Seats[s]));
        Assert.Empty(log.Rejections);
        Assert.DoesNotContain("ActionRejected", log.ToJsonLinesWithRejections());
        Assert.StartsWith("{\"type\":\"header\"", log.ToJsonLines());
    }
}
