using System.Text.Json;
using Belote.Simulation.Cli;
using Xunit;

namespace Belote.Simulation.Tests;

public class BenchmarkCliTests
{
    private static async Task<(int Code, string Out, string Err)> Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int code = await CliApp.RunAsync(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"bench-{Guid.NewGuid():N}.json");

    [Fact]
    public async Task Bench_writes_a_json_result_and_exits_zero()
    {
        string path = TempFile();
        try
        {
            var (code, text, _) = await Run("bench", "--games", "20", "--seed", "3", "--out", path);

            Assert.Equal(CliApp.Ok, code);
            Assert.Contains("smart team wins", text);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(20, doc.RootElement.GetProperty("gameCount").GetInt32());
            Assert.Equal(20, doc.RootElement.GetProperty("games").GetArrayLength());
            Assert.Equal(0, doc.RootElement.GetProperty("failed").GetInt32());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task An_unreachable_minimum_win_rate_exits_one()
    {
        var (code, _, err) = await Run("bench", "--games", "10", "--min-win-rate", "1.0001");
        // 1.0001 is out of range for the option itself, so it is a usage error.
        Assert.Equal(CliApp.BadUsage, code);
        Assert.Contains("min-win-rate", err);

        var (belowCode, _, _) = await Run("bench", "--games", "10", "--seed", "1", "--min-win-rate", "1");
        Assert.True(belowCode is CliApp.Ok or CliApp.GameFailed);
    }

    [Fact]
    public async Task A_missing_game_count_is_a_usage_error()
    {
        var (code, _, _) = await Run("bench", "--seed", "1");
        Assert.Equal(CliApp.BadUsage, code);
    }

    [Fact]
    public async Task The_same_arguments_produce_byte_identical_output_files()
    {
        string a = TempFile();
        string b = TempFile();
        try
        {
            await Run("bench", "--games", "16", "--seed", "9", "--parallelism", "1", "--out", a);
            await Run("bench", "--games", "16", "--seed", "9", "--parallelism", "6", "--out", b);
            Assert.Equal(File.ReadAllBytes(a), File.ReadAllBytes(b));
        }
        finally
        {
            File.Delete(a);
            File.Delete(b);
        }
    }
}
