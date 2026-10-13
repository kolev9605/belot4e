using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Belote.Core;

namespace Belote.Simulation;

public sealed record BenchmarkOptions
{
    public ulong BaseSeed { get; init; } = 1;

    public int GameCount { get; init; } = 100;

    public RuleSet RuleSet { get; init; } = RuleSets.Bulgarian;

    public int Parallelism { get; init; } = Math.Max(1, Environment.ProcessorCount);
}

public sealed record BenchmarkGame(
    int Index,
    ulong Seed,
    Team SmartTeam,
    Team? Winner,
    int NorthSouth,
    int EastWest,
    bool FinishedCorrectly);

/// <summary>Outcome of a seeded smart-bot versus random-bot batch (see contracts/benchmark-cli.md).</summary>
public sealed record BenchmarkResult(
    ulong BaseSeed,
    int GameCount,
    int SmartWins,
    int RandomWins,
    double SmartWinRate,
    double MeanScoreMargin,
    int Failed,
    IReadOnlyList<BenchmarkGame> Games)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);
}

/// <summary>
/// Plays N seeded, audited games of smart bots against random-legal bots. Even game indexes seat the
/// smart team North/South, odd indexes East/West, so neither team benefits from seating.
/// </summary>
public static class Benchmark
{
    public static async Task<BenchmarkResult> RunAsync(BenchmarkOptions options, CancellationToken cancellationToken = default)
    {
        var report = await SimulationRunner.RunAsync(
            new RunOptions
            {
                BaseSeed = options.BaseSeed,
                GameCount = options.GameCount,
                RuleSet = options.RuleSet,
                Parallelism = options.Parallelism,
                KeepLogs = KeepLogs.None,
                Audit = true,
                SeatFactory = (index, seed) =>
                    SeatPresets.Create(index % 2 == 0 ? SeatPresets.SmartNs : SeatPresets.SmartEw, seed),
            },
            cancellationToken).ConfigureAwait(false);

        var games = report.Games
            .OrderBy(g => g.Index)
            .Select(g => new BenchmarkGame(
                g.Index,
                g.Seed,
                g.Index % 2 == 0 ? Team.NorthSouth : Team.EastWest,
                g.Winner,
                g.NorthSouth,
                g.EastWest,
                g.FinishedCorrectly))
            .ToList();

        var finished = games.Where(g => g.FinishedCorrectly).ToList();
        int smartWins = finished.Count(g => g.Winner == g.SmartTeam);
        int randomWins = finished.Count(g => g.Winner is { } w && w != g.SmartTeam);
        double margin = finished.Count == 0
            ? 0
            : finished.Average(g => g.SmartTeam == Team.NorthSouth ? g.NorthSouth - g.EastWest : g.EastWest - g.NorthSouth);

        return new BenchmarkResult(
            options.BaseSeed,
            options.GameCount,
            smartWins,
            randomWins,
            options.GameCount == 0 ? 0 : (double)smartWins / options.GameCount,
            margin,
            games.Count - finished.Count,
            games);
    }
}
