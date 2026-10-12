using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Belote.Core;

namespace Belote.Simulation;

public enum KeepLogs
{
    None,
    FailuresOnly,
    All,
}

public sealed record RunOptions
{
    public ulong BaseSeed { get; init; } = 1;

    public int GameCount { get; init; } = 1;

    public RuleSet RuleSet { get; init; } = RuleSets.Bulgarian;

    public string SeatPreset { get; init; } = SeatPresets.AllBot;

    /// <summary>Overrides <see cref="SeatPreset"/> when set: builds the seat controllers of one game.</summary>
    public Func<int, ulong, IReadOnlyDictionary<Seat, SeatController>>? SeatFactory { get; init; }

    public int Parallelism { get; init; } = Math.Max(1, Environment.ProcessorCount);

    public KeepLogs KeepLogs { get; init; } = KeepLogs.FailuresOnly;

    public GameRunOptions Game { get; init; } = new();

    /// <summary>Run the independent auditor on every game (recommended).</summary>
    public bool Audit { get; init; } = true;
}

public sealed record GameReport(
    int Index,
    ulong Seed,
    bool FinishedCorrectly,
    Team? Winner,
    int NorthSouth,
    int EastWest,
    int Deals,
    int HandsScored,
    string? LogHash,
    FailureKind? Failure,
    string? FailureDetail,
    GameLog? Log);

public sealed record SimulationReport(
    RunOptions Options,
    IReadOnlyList<GameReport> Games,
    TimeSpan Elapsed)
{
    public int Passed => Games.Count(g => g.FinishedCorrectly);

    public int Failed => Games.Count - Passed;

    public IReadOnlyDictionary<FailureKind, int> ByFailure => Games
        .Where(g => g.Failure is not null)
        .GroupBy(g => g.Failure!.Value)
        .ToDictionary(g => g.Key, g => g.Count());
}

/// <summary>Plays N seeded games, audits each, and reports per-game outcomes.</summary>
public static class SimulationRunner
{
    public static async Task<SimulationReport> RunAsync(RunOptions options, CancellationToken cancellationToken = default)
    {
        RuleSetValidator.ThrowIfInvalid(options.RuleSet);
        if (options.SeatFactory is null && !SeatPresets.IsValid(options.SeatPreset))
        {
            throw new ArgumentException($"Unknown seat preset '{options.SeatPreset}'.", nameof(options));
        }

        var stopwatch = Stopwatch.StartNew();
        var results = new GameReport[options.GameCount];
        var parallel = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.Parallelism),
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(
            Enumerable.Range(0, options.GameCount),
            parallel,
            async (index, ct) => results[index] = await RunOneAsync(options, index, ct).ConfigureAwait(false))
            .ConfigureAwait(false);

        stopwatch.Stop();
        return new SimulationReport(options, results, stopwatch.Elapsed);
    }

    /// <summary>Plays and audits a single game of a batch (used to re-run one seed in isolation).</summary>
    public static async Task<GameReport> RunOneAsync(RunOptions options, int index, CancellationToken ct = default)
    {
        ulong seed = Seeds.ForGame(options.BaseSeed, index);
        var seats = options.SeatFactory is { } factory
            ? factory(index, seed)
            : SeatPresets.Create(options.SeatPreset, seed);

        var run = await GameRunner.RunAsync(options.RuleSet, seed, seats, options.Game, ct).ConfigureAwait(false);

        FailureKind? failure = run.Failure;
        string? detail = run.FailureDetail;
        if (failure is null && options.Audit)
        {
            var audit = GameAuditor.Audit(run.Log);
            if (!audit.Ok)
            {
                var first = audit.First!;
                failure = first.Check == "ReplayMismatch" ? FailureKind.ReplayMismatch
                    : first.Check == "NoWinner" ? FailureKind.NoWinner
                    : first.Check == "AuditError" ? FailureKind.AuditMismatch
                    : FailureKind.RuleViolationDetected;
                detail = first.ToString();
            }
        }

        bool ok = failure is null;
        bool keep = options.KeepLogs == KeepLogs.All || (options.KeepLogs == KeepLogs.FailuresOnly && !ok);
        return new GameReport(
            index,
            seed,
            ok,
            run.Winner,
            run.NorthSouth,
            run.EastWest,
            run.Deals,
            run.HandsScored,
            run.Log.Hash,
            failure,
            detail,
            keep ? run.Log : null);
    }
}

public static class ReportJson
{
    public static string ToJson(SimulationReport report, bool indented = true)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
        {
            w.WriteStartObject();
            w.WriteNumber("reportVersion", 1);

            w.WriteStartObject("options");
            w.WriteNumber("baseSeed", report.Options.BaseSeed);
            w.WriteNumber("games", report.Options.GameCount);
            w.WriteString("ruleSet", report.Options.RuleSet.Name);
            w.WriteNumber("ruleSetVersion", report.Options.RuleSet.Version);
            w.WriteString("seats", report.Options.SeatFactory is null ? report.Options.SeatPreset : "custom");
            w.WriteEndObject();

            w.WriteStartObject("summary");
            w.WriteNumber("games", report.Games.Count);
            w.WriteNumber("finishedCorrectly", report.Passed);
            w.WriteNumber("failed", report.Failed);
            w.WriteStartObject("byFailureKind");
            foreach (var kv in report.ByFailure.OrderBy(k => k.Key.ToString(), StringComparer.Ordinal))
            {
                w.WriteNumber(kv.Key.ToString(), kv.Value);
            }

            w.WriteEndObject();
            w.WriteEndObject();

            w.WriteStartArray("games");
            foreach (var g in report.Games)
            {
                w.WriteStartObject();
                w.WriteNumber("index", g.Index);
                w.WriteNumber("seed", g.Seed);
                w.WriteBoolean("finished", g.FinishedCorrectly);
                if (g.Winner is { } winner)
                {
                    w.WriteString("winner", JsonNames.Of(winner));
                }
                else
                {
                    w.WriteNull("winner");
                }

                w.WriteStartObject("finalScore");
                w.WriteNumber("northSouth", g.NorthSouth);
                w.WriteNumber("eastWest", g.EastWest);
                w.WriteEndObject();
                w.WriteNumber("deals", g.Deals);
                w.WriteNumber("hands", g.HandsScored);
                w.WriteString("logHash", g.LogHash);
                w.WriteString("failureKind", g.Failure?.ToString());
                w.WriteString("failureDetail", g.FailureDetail);
                w.WriteEndObject();
            }

            w.WriteEndArray();

            w.WriteStartObject("runner");
            w.WriteNumber("elapsedMs", (long)report.Elapsed.TotalMilliseconds);
            w.WriteNumber("parallelism", report.Options.Parallelism);
            w.WriteEndObject();

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
