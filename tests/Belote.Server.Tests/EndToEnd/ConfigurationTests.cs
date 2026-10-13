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
        Assert.True((await client.TakeSeatAsync()).Ok);
        Assert.Equal(TimeSpan.Zero, server.Registry.Current!.Options.BotDelay);
        await client.PlayToEndAsync(ClientPolicies.First);

        // An absurd override is clamped to the 0..5 s range.
        Assert.True((await client.CreateTableAsync(2, botDelayMs: 999_999)).Ok);
        Assert.True((await client.TakeSeatAsync()).Ok);
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
        Assert.True((await connection.TakeSeatAsync()).Ok);
        Assert.Equal(TimeSpan.FromMilliseconds(250), server.Registry.Current!.Options.BotDelay);
    }

    [Fact]
    public async Task RoomLinksFallBackToTheWebClientSoADeepLinkOpensTheApp()
    {
        var web = Path.Combine(Path.GetTempPath(), "belote-web-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(web);
        await File.WriteAllTextAsync(Path.Combine(web, "index.html"), "<html><body>belote-app</body></html>");
        try
        {
            await using var server = await LiveServer.StartAsync(null, $"--Web:RootPath={web}");
            using var http = new HttpClient();

            var deepLink = await http.GetStringAsync($"{server.BaseUrl}/room/{Guid.NewGuid()}");

            Assert.Contains("belote-app", deepLink);
        }
        finally
        {
            Directory.Delete(web, recursive: true);
        }
    }

    [Fact]
    public async Task RoomTimingIsConfigurableAndDefaultsToTheSpecifiedValues()
    {
        await using var defaults = await LiveServer.StartAsync();
        var rooms = defaults.Registry;
        Assert.NotNull(rooms);

        await using var tuned = await LiveServer.StartAsync(o =>
        {
            o.Rooms.GraceSeconds = 5;
            o.Rooms.IdleExpiryMinutes = 2;
        });
        await using var a = await RoomClient.ConnectAsync(tuned.HubUrl, "Ivo");
        Assert.True((await a.CreateRoomAsync()).Ok);
        Assert.Equal(1, tuned.Registry.RoomCount);

        var fresh = new Belote.Table.Rooms.RoomOptions();
        Assert.Equal(60, fresh.GraceSeconds);
        Assert.Equal(60, fresh.IdleExpiryMinutes);
        Assert.Equal(60, fresh.LobbySeatReleaseSeconds);
        Assert.Equal(20, fresh.MaxNameLength);
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
