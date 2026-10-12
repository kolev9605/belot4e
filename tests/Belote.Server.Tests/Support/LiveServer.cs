using Belote.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Belote.Server.Tests;

/// <summary>The real server on loopback Kestrel with an ephemeral port; one per test.</summary>
internal sealed class LiveServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private LiveServer(WebApplication app, string hubUrl)
    {
        _app = app;
        HubUrl = hubUrl;
    }

    public string HubUrl { get; }

    public TableRegistry Registry => _app.Services.GetRequiredService<TableRegistry>();

    public static async Task<LiveServer> StartAsync(Action<TableServerOptions>? configure = null)
    {
        var app = ServerHost.Build(
            new[] { "--urls", "http://127.0.0.1:0", "--Logging:LogLevel:Default=Warning" },
            configure);
        await app.StartAsync();
        var address = app.Urls.First();
        return new LiveServer(app, address.TrimEnd('/') + TableHub.Path);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
