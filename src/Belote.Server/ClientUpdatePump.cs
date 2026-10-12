using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Belote.Table;
using Microsoft.AspNetCore.SignalR;

namespace Belote.Server;

/// <summary>
/// Delivers a table's updates to one connection in the order they were produced. The table hands an
/// update over without waiting for the network; a single drain task sends them one at a time.
/// </summary>
internal sealed class ClientUpdatePump : ITableObserver, IAsyncDisposable
{
    public const string UpdateMethod = "Update";

    private readonly Channel<TableUpdateDto> _channel = Channel.CreateUnbounded<TableUpdateDto>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    private readonly Task _drain;

    public ClientUpdatePump(IHubContext<TableHub> hub, string connectionId)
    {
        _drain = Task.Run(async () =>
        {
            try
            {
                await foreach (var update in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
                {
                    await hub.Clients.Client(connectionId).SendAsync(UpdateMethod, update).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // The connection is gone; the table pauses via the hub's disconnect handler.
            }
        });
    }

    public ValueTask OnUpdateAsync(TableUpdateDto update)
    {
        _channel.Writer.TryWrite(update);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _drain.ConfigureAwait(false);
    }
}
