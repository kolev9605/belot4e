using Belote.Core;
using Belote.Simulation;
using Xunit;

namespace Belote.Simulation.Tests;

/// <summary>
/// Golden logs pin the exact event stream of a few seeds. If a rule or the RNG changes on purpose,
/// regenerate with BELOTE_UPDATE_GOLDEN=1 and review the diff: every changed line is a behaviour change.
/// </summary>
public class GoldenLogTests
{
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public async Task A_fixed_seed_reproduces_its_committed_golden_log(ulong seed)
    {
        var run = await Sim.RunGameAsync(seed);
        Assert.True(run.Finished, run.FailureDetail);
        string actual = run.Log.ToJsonLines();

        string path = Path.Combine(Sim.FindRepositoryRoot(), "tests", "Belote.Simulation.Tests", "Golden", $"seed-{seed}.jsonl");
        if (Environment.GetEnvironmentVariable("BELOTE_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
        }

        Assert.True(File.Exists(path), $"Golden file missing: {path}. Run with BELOTE_UPDATE_GOLDEN=1 to create it.");
        string expected = File.ReadAllText(path).ReplaceLineEndings("\n");
        Assert.Equal(expected, actual);
        Assert.True(GameAuditor.AuditJsonLines(expected).Ok);
    }
}

/// <summary>
/// Large seeded batches (SC-001, SC-003, SC-004, SC-007). Size comes from BELOTE_BATCH_GAMES
/// (default 1,000; the nightly CI job uses 10,000).
/// </summary>
[Trait("Category", "Batch")]
public class LargeBatchTests
{
    private static int Games =>
        int.TryParse(Environment.GetEnvironmentVariable("BELOTE_BATCH_GAMES"), out int n) && n > 0 ? n : 1000;

    [Fact]
    public async Task SC_001_All_bot_batch_finishes_every_game_correctly()
    {
        var report = await Sim.RunBatchAsync(Games, baseSeed: 20260101, keepLogs: KeepLogs.None, parallelism: Environment.ProcessorCount);
        var failures = report.Games.Where(g => !g.FinishedCorrectly).Take(5)
            .Select(g => $"#{g.Index} seed {g.Seed}: {g.Failure}: {g.FailureDetail}");
        Assert.True(report.Failed == 0, string.Join(Environment.NewLine, failures));
        Assert.Equal(Games, report.Passed);
    }

    [Theory]
    [InlineData("mixed-1")]
    [InlineData("mixed-2")]
    [InlineData("mixed-3")]
    [InlineData("mixed-4")]
    [InlineData("scripted-illegal")]
    public async Task SC_004_Mixed_seat_batches_are_as_correct_as_all_bot_batches(string preset)
    {
        int games = Math.Min(Games, 1000);
        var report = await Sim.RunBatchAsync(games, preset, baseSeed: 77, keepLogs: KeepLogs.None);
        Assert.Equal(games, report.Passed);
    }

    [Fact]
    public async Task SC_007_One_thousand_games_run_well_inside_five_minutes()
    {
        var report = await Sim.RunBatchAsync(1000, baseSeed: 5, keepLogs: KeepLogs.None, parallelism: 1);
        Assert.Equal(1000, report.Passed);
        Assert.True(report.Elapsed < TimeSpan.FromMinutes(5), $"Took {report.Elapsed}.");
    }
}
