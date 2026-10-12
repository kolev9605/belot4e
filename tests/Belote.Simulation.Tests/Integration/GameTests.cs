using Belote.Core;
using Belote.Simulation;
using Xunit;

namespace Belote.Simulation.Tests;

public class AllBotGameTests
{
    [Fact]
    public async Task One_all_bot_game_finishes_with_one_winner_at_or_above_the_target()
    {
        var run = await Sim.RunGameAsync(42);

        Assert.True(run.Finished, run.FailureDetail);
        Assert.NotNull(run.Winner);
        int winnerScore = run.Winner == Team.NorthSouth ? run.NorthSouth : run.EastWest;
        int other = run.Winner == Team.NorthSouth ? run.EastWest : run.NorthSouth;
        Assert.True(winnerScore >= RuleSets.Bulgarian.Match.TargetScore);
        Assert.True(winnerScore >= other);
        Assert.True(run.HandsScored >= 1);
        Assert.IsType<GameFinished>(run.Log.Events[^1]);
        Assert.True(GameAuditor.Audit(run.Log).Ok);
    }

    [Fact]
    public async Task One_hundred_seeded_games_all_finish_and_pass_the_audit()
    {
        var report = await Sim.RunBatchAsync(100, keepLogs: KeepLogs.None);
        Assert.Equal(100, report.Passed);
        Assert.All(report.Games, g =>
        {
            Assert.True(g.FinishedCorrectly, g.FailureDetail);
            Assert.NotNull(g.Winner);
        });
    }

    [Fact]
    public async Task Games_exercise_failed_contracts_ties_sweeps_announcements_and_all_pass_hands()
    {
        var kinds = new HashSet<string>();
        for (ulong seed = 1; seed <= 300 && kinds.Count < 8; seed++)
        {
            var run = await Sim.RunGameAsync(seed);
            foreach (var e in run.Log.Events)
            {
                switch (e)
                {
                    case HandVoided: kinds.Add("allPass"); break;
                    case PointsHeldOver: kinds.Add("tie"); break;
                    case HeldOverAwarded: kinds.Add("heldAwarded"); break;
                    case AnnouncementsCompared: kinds.Add("announcements"); break;
                    case BeloteAwarded: kinds.Add("belote"); break;
                    case HandScored h when h.Result.Outcome == HandOutcome.Failed: kinds.Add("failed"); break;
                    case HandScored h when h.Result.AllTricksTeam is not null: kinds.Add("sweep"); break;
                    case HandScored h when h.Result.Multiplier > 1: kinds.Add("doubled"); break;
                }
            }
        }

        Assert.Contains("allPass", kinds);
        Assert.Contains("failed", kinds);
        Assert.Contains("announcements", kinds);
        Assert.Contains("belote", kinds);
        Assert.Contains("doubled", kinds);
    }
}

public class DeterminismTests
{
    [Fact]
    public async Task SC_002_The_same_seed_reproduces_the_identical_log_and_result()
    {
        for (ulong seed = 1; seed <= 1000; seed++)
        {
            var a = await Sim.RunGameAsync(seed);
            var b = await Sim.RunGameAsync(seed);
            Assert.Equal(a.Log.Hash, b.Log.Hash);
            Assert.Equal(a.Log.ToJsonLines(), b.Log.ToJsonLines());
            Assert.Equal(a.Winner, b.Winner);
            Assert.Equal((a.NorthSouth, a.EastWest), (b.NorthSouth, b.EastWest));
        }
    }

    [Fact]
    public async Task Different_seeds_give_different_games()
    {
        var hashes = new HashSet<string>();
        for (ulong seed = 1; seed <= 50; seed++)
        {
            hashes.Add((await Sim.RunGameAsync(seed)).Log.Hash);
        }

        Assert.Equal(50, hashes.Count);
    }

    [Fact]
    public async Task Parallelism_does_not_change_any_games_result()
    {
        var serial = await Sim.RunBatchAsync(60, parallelism: 1, keepLogs: KeepLogs.None);
        var parallel = await Sim.RunBatchAsync(60, parallelism: 8, keepLogs: KeepLogs.None);
        Assert.Equal(
            serial.Games.Select(g => (g.Index, g.Seed, g.LogHash, g.Winner, g.NorthSouth, g.EastWest)),
            parallel.Games.Select(g => (g.Index, g.Seed, g.LogHash, g.Winner, g.NorthSouth, g.EastWest)));
    }

    [Fact]
    public async Task A_different_rule_set_changes_the_game_but_stays_reproducible()
    {
        var variant = RuleSets.Bulgarian with { Match = RuleSets.Bulgarian.Match with { TargetScore = 61 } };
        var plain = await Sim.RunGameAsync(7);
        var first = await Sim.RunGameAsync(7, rules: variant);
        var second = await Sim.RunGameAsync(7, rules: variant);
        Assert.NotEqual(plain.Log.Hash, first.Log.Hash);
        Assert.Equal(first.Log.Hash, second.Log.Hash);
    }
}
