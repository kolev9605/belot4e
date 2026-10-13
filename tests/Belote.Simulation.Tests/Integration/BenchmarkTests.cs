using Belote.Core;
using Belote.Simulation;
using Xunit;

namespace Belote.Simulation.Tests;

public class BenchmarkTests
{
    [Fact]
    public async Task The_smart_team_beats_the_random_team_in_at_least_eighty_percent_of_matches()
    {
        var result = await Benchmark.RunAsync(new BenchmarkOptions { BaseSeed = 1, GameCount = 400 });

        Assert.Equal(0, result.Failed);
        Assert.Equal(400, result.SmartWins + result.RandomWins);
        Assert.True(result.SmartWinRate >= 0.80, $"win rate {result.SmartWinRate:P1}");
        Assert.True(result.MeanScoreMargin > 0);
    }

    [Fact]
    public async Task The_result_is_identical_across_runs_and_parallelism()
    {
        var a = await Benchmark.RunAsync(new BenchmarkOptions { BaseSeed = 5, GameCount = 60, Parallelism = 1 });
        var b = await Benchmark.RunAsync(new BenchmarkOptions { BaseSeed = 5, GameCount = 60, Parallelism = 8 });
        Assert.Equal(a.ToJson(), b.ToJson());
    }

    [Fact]
    public async Task Seats_are_swapped_so_each_team_plays_half_the_games_as_the_smart_team()
    {
        var result = await Benchmark.RunAsync(new BenchmarkOptions { BaseSeed = 2, GameCount = 41 });
        Assert.Equal(21, result.Games.Count(g => g.SmartTeam == Team.NorthSouth));
        Assert.Equal(20, result.Games.Count(g => g.SmartTeam == Team.EastWest));
        Assert.Equal(41, result.Games.Select(g => g.Seed).Distinct().Count());
    }

    [Fact]
    public async Task The_smart_team_also_wins_clearly_from_each_side_of_the_table()
    {
        var result = await Benchmark.RunAsync(new BenchmarkOptions { BaseSeed = 3, GameCount = 200 });
        foreach (var team in new[] { Team.NorthSouth, Team.EastWest })
        {
            var games = result.Games.Where(g => g.SmartTeam == team).ToList();
            double rate = (double)games.Count(g => g.Winner == team) / games.Count;
            Assert.True(rate >= 0.80, $"{team}: {rate:P1}");
        }
    }
}
