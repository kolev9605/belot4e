using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Belote.Core;

namespace Belote.Simulation.Cli;

/// <summary>
/// `belote-sim` command implementation. Exit codes: 0 all games finished correctly, 1 at least one game
/// failed a check, 2 invalid arguments or rule set.
/// </summary>
public static class CliApp
{
    public const int Ok = 0;
    public const int GameFailed = 1;
    public const int BadUsage = 2;

    private const string Usage = """
        Usage:
          belote-sim run    --games <N> [--seed <U64>] [--rules <path|bulgarian>] [--seats <preset>]
                            [--parallelism <N>] [--keep-logs none|failures|all] [--out <dir>]
                            [--max-illegal <N>] [--decision-timeout-ms <N>]
          belote-sim replay --seed <U64> [--rules <path|bulgarian>] [--seats <preset>] [--out <file>]
          belote-sim audit  --log <file>
        Seat presets: all-bot, mixed-1..mixed-4, scripted-illegal
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Length == 0)
            {
                error.WriteLine(Usage);
                return BadUsage;
            }

            var options = ParseOptions(args.Skip(1).ToArray());
            return args[0] switch
            {
                "run" => await RunBatchAsync(options, output, error).ConfigureAwait(false),
                "replay" => await ReplayAsync(options, output, error).ConfigureAwait(false),
                "audit" => Audit(options, output, error),
                _ => Usage_(error, $"Unknown command '{args[0]}'."),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or FileNotFoundException
            or System.Text.Json.JsonException or KeyNotFoundException)
        {
            error.WriteLine($"error: {ex.Message}");
            return BadUsage;
        }
    }

    private static int Usage_(TextWriter error, string message)
    {
        error.WriteLine(message);
        error.WriteLine(Usage);
        return BadUsage;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                throw new ArgumentException($"Unexpected argument '{args[i]}'.");
            }

            options[args[i][2..]] = args[++i];
        }

        return options;
    }

    private static RuleSet LoadRules(Dictionary<string, string> options)
    {
        string name = options.GetValueOrDefault("rules", "bulgarian");
        var rules = name == "bulgarian" ? RuleSets.Bulgarian : RuleSetJson.Parse(File.ReadAllText(name));
        RuleSetValidator.ThrowIfInvalid(rules);
        return rules;
    }

    private static string Seats_(Dictionary<string, string> options)
    {
        string preset = options.GetValueOrDefault("seats", SeatPresets.AllBot);
        if (!SeatPresets.IsValid(preset))
        {
            throw new ArgumentException($"Unknown seat preset '{preset}'.");
        }

        return preset;
    }

    private static async Task<int> RunBatchAsync(Dictionary<string, string> options, TextWriter output, TextWriter error)
    {
        if (!options.TryGetValue("games", out var gamesText) || !int.TryParse(gamesText, out int games) || games < 1)
        {
            throw new ArgumentException("--games <N> is required and must be a positive integer.");
        }

        var runOptions = new RunOptions
        {
            BaseSeed = ulong.Parse(options.GetValueOrDefault("seed", "1")),
            GameCount = games,
            RuleSet = LoadRules(options),
            SeatPreset = Seats_(options),
            Parallelism = int.Parse(options.GetValueOrDefault("parallelism", Environment.ProcessorCount.ToString())),
            KeepLogs = options.GetValueOrDefault("keep-logs", "failures") switch
            {
                "none" => KeepLogs.None,
                "failures" => KeepLogs.FailuresOnly,
                "all" => KeepLogs.All,
                var other => throw new ArgumentException($"Unknown --keep-logs value '{other}'."),
            },
            Game = new GameRunOptions
            {
                MaxIllegalAttempts = int.Parse(options.GetValueOrDefault("max-illegal", "3")),
                DecisionTimeout = TimeSpan.FromMilliseconds(
                    int.Parse(options.GetValueOrDefault("decision-timeout-ms", "5000"))),
            },
        };

        var report = await SimulationRunner.RunAsync(runOptions).ConfigureAwait(false);

        string outDir = options.GetValueOrDefault("out", "sim-out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "report.json"), ReportJson.ToJson(report));
        foreach (var game in report.Games.Where(g => g.Log is not null))
        {
            File.WriteAllText(
                Path.Combine(outDir, $"game-{game.Index}-{game.Seed}.jsonl"),
                game.Log!.ToJsonLinesWithRejections());
        }

        output.WriteLine($"games: {report.Games.Count}  finished correctly: {report.Passed}  failed: {report.Failed}");
        foreach (var kv in report.ByFailure.OrderBy(k => k.Key.ToString(), StringComparer.Ordinal))
        {
            output.WriteLine($"  {kv.Key}: {kv.Value}");
        }

        foreach (var failed in report.Games.Where(g => !g.FinishedCorrectly).Take(10))
        {
            error.WriteLine($"FAILED game {failed.Index} seed {failed.Seed}: {failed.Failure}: {failed.FailureDetail}");
        }

        output.WriteLine($"elapsed: {report.Elapsed.TotalSeconds:0.00}s  report: {Path.Combine(outDir, "report.json")}");
        return report.Failed == 0 ? Ok : GameFailed;
    }

    private static async Task<int> ReplayAsync(Dictionary<string, string> options, TextWriter output, TextWriter error)
    {
        if (!options.TryGetValue("seed", out var seedText) || !ulong.TryParse(seedText, out ulong seed))
        {
            throw new ArgumentException("--seed <U64> is required.");
        }

        var rules = LoadRules(options);
        var seats = SeatPresets.Create(Seats_(options), seed);
        var run = await GameRunner.RunAsync(rules, seed, seats).ConfigureAwait(false);
        string text = run.Log.ToJsonLinesWithRejections();

        if (options.TryGetValue("out", out var path))
        {
            File.WriteAllText(path, text);
            output.WriteLine($"log written to {path}");
        }
        else
        {
            output.Write(text);
        }

        if (!run.Finished)
        {
            error.WriteLine($"game did not finish: {run.Failure}: {run.FailureDetail}");
            return GameFailed;
        }

        return Ok;
    }

    private static int Audit(Dictionary<string, string> options, TextWriter output, TextWriter error)
    {
        if (!options.TryGetValue("log", out var path))
        {
            throw new ArgumentException("--log <file> is required.");
        }

        var report = GameAuditor.AuditJsonLines(File.ReadAllText(path));
        if (report.Ok)
        {
            output.WriteLine("audit passed");
            return Ok;
        }

        error.WriteLine($"audit failed: first offending event: {report.First}");
        foreach (var issue in report.Issues.Skip(1).Take(9))
        {
            error.WriteLine($"  also: {issue}");
        }

        return GameFailed;
    }
}
