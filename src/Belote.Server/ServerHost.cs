using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Belote.Server;

/// <summary>Builds the live-table web application; used by <c>Program</c> and by the end-to-end tests.</summary>
public static class ServerHost
{
    public static WebApplication Build(string[] args, Action<TableServerOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        var options = new TableServerOptions();
        builder.Configuration.GetSection(TableServerOptions.SectionName).Bind(options);
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<TableRegistry>();
        builder.Services.AddSignalR(hub =>
        {
            // Let bursts of intents reach the table, which serialises them.
            hub.MaximumParallelInvocationsPerClient = 16;
        });

        var app = builder.Build();
        app.MapHub<TableHub>(TableHub.Path);
        return app;
    }
}
