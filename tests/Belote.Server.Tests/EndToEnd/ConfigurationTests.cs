using Belote.Table;

namespace Belote.Server.Tests;

public class ConfigurationTests
{
    [Fact]
    public async Task ServerDefaultDelayIsUsedAndPerTableOverrideIsClamped()
    {
        await using var server = await LiveServer.StartAsync(o => o.BotDelayMs = 250);
        await using var client = await TableClient.ConnectAsync(server.HubUrl);

        // No override in the request: the server default applies.
        Assert.True((await client.CreateTableAsync(1, botDelayMs: 0)).Ok);
        Assert.Equal(TimeSpan.Zero, server.Registry.Current!.Options.BotDelay);
        Assert.True((await client.TakeSeatAsync()).Ok);
        await client.PlayToEndAsync(ClientPolicies.First);

        // An absurd override is clamped to the 0..5 s range.
        Assert.True((await client.CreateTableAsync(2, botDelayMs: 999_999)).Ok);
        Assert.Equal(TimeSpan.FromSeconds(5), server.Registry.Current!.Options.BotDelay);
    }

    [Fact]
    public async Task ServerDefaultDelayAppliesWhenTheRequestHasNone()
    {
        await using var server = await LiveServer.StartAsync(o => o.BotDelayMs = 250);
        await using var connection = await TableClient.ConnectAsync(server.HubUrl);

        // A request without a delay uses the configured default.
        var result = await connection.CreateTableWithoutDelayAsync(3);

        Assert.True(result.Ok);
        Assert.Equal(TimeSpan.FromMilliseconds(250), server.Registry.Current!.Options.BotDelay);
    }

    [Fact]
    public async Task FinishedGameLogIsWrittenToTheConfiguredDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "belote-logs-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var server = await LiveServer.StartAsync(o => o.LogDirectory = directory);
            await using var client = await TableClient.ConnectAsync(server.HubUrl);
            Assert.True((await client.CreateTableAsync(77, botDelayMs: 0)).Ok);
            Assert.True((await client.TakeSeatAsync()).Ok);
            await client.PlayToEndAsync(ClientPolicies.First);

            var file = Assert.Single(Directory.GetFiles(directory, "*.jsonl"));
            var audit = Belote.Simulation.GameAuditor.AuditJsonLines(await File.ReadAllTextAsync(file));
            Assert.True(audit.Ok, audit.First?.ToString());
            Assert.StartsWith("77-", Path.GetFileName(file));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
