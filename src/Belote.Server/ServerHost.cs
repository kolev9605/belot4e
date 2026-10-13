using System;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Belote.Table.Rooms;

namespace Belote.Server;

/// <summary>Builds the live-table web application; used by <c>Program</c> and by the end-to-end tests.</summary>
public static class ServerHost
{
    public const string DefaultUrl = "http://localhost:5080";

    public static WebApplication Build(string[] args, Action<TableServerOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Without an explicit address the server listens where the web client's dev proxy expects it
        // (Kestrel's own default, 5000, would silently leave the client unable to connect).
        if (string.IsNullOrWhiteSpace(builder.Configuration["urls"])
            && string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"])
            && !builder.Configuration.GetSection("Kestrel:Endpoints").Exists())
        {
            builder.WebHost.UseUrls(DefaultUrl);
        }

        var options = new TableServerOptions();
        builder.Configuration.GetSection(TableServerOptions.SectionName).Bind(options);
        builder.Configuration.GetSection(RoomOptions.SectionName).Bind(options.Rooms);
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<RoomRegistry>();
        builder.Services.AddSignalR(hub =>
        {
            // Let bursts of intents reach the table, which serialises them.
            hub.MaximumParallelInvocationsPerClient = 16;

            // Notice a lost connection quickly: a silent network loss pauses the table within ~15 s.
            hub.KeepAliveInterval = TimeSpan.FromSeconds(5);
            hub.ClientTimeoutInterval = TimeSpan.FromSeconds(15);
        });

        var app = builder.Build();
        app.MapHub<TableHub>(TableHub.Path);

        // Optional same-origin hosting of the built web client (feature 003).
        var webRoot = app.Configuration["Web:RootPath"];
        if (!string.IsNullOrWhiteSpace(webRoot))
        {
            var fullPath = Path.GetFullPath(webRoot);
            if (Directory.Exists(fullPath))
            {
                var files = new PhysicalFileProvider(fullPath);
                app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
                app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
                app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files });
            }
            else
            {
                app.Logger.LogWarning("Web:RootPath [{Path}] does not exist; the web client is not served.", fullPath);
            }
        }

        return app;
    }
}
