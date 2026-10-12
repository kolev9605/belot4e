using System.Security.Cryptography;
using System.Text;
using Belote.Core;
using Belote.Simulation;

namespace Belote.Simulation.Tests;

internal static class Sim
{
    public static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Belote.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Belote.sln not found above the test directory.");
    }

    public static async Task<GameRunResult> RunGameAsync(
        ulong seed,
        string preset = SeatPresets.AllBot,
        RuleSet? rules = null,
        GameRunOptions? options = null) =>
        await GameRunner.RunAsync(
            rules ?? RuleSets.Bulgarian,
            seed,
            SeatPresets.Create(preset, seed),
            options);

    public static Task<SimulationReport> RunBatchAsync(
        int games,
        string preset = SeatPresets.AllBot,
        ulong baseSeed = 1,
        KeepLogs keepLogs = KeepLogs.FailuresOnly,
        RuleSet? rules = null,
        int parallelism = 4) =>
        SimulationRunner.RunAsync(new RunOptions
        {
            BaseSeed = baseSeed,
            GameCount = games,
            SeatPreset = preset,
            KeepLogs = keepLogs,
            RuleSet = rules ?? RuleSets.Bulgarian,
            Parallelism = parallelism,
        });

    /// <summary>Hash of the event lines only (the header names the seat controllers, which may differ).</summary>
    public static string EventsHash(GameLog log)
    {
        var lines = log.ToJsonLines().Split('\n').Skip(1);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    public static IReadOnlyDictionary<Seat, SeatController> Bots(ulong seed) =>
        SeatPresets.Create(SeatPresets.AllBot, seed);
}
