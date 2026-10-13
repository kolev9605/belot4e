using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Belote.Table;
using Belote.Table.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace Belote.Server;

/// <summary>
/// Delivers a room's pushes to one connection in the order they were produced. The room hands a message
/// over without waiting for the network; a single drain task sends them one at a time.
/// </summary>
internal sealed class ConnectionSink : IRoomSink, IAsyncDisposable
{
    public const string UpdateMethod = "Update";
    public const string RoomMethod = "Room";
    public const string SupersededMethod = "Superseded";
    public const string ClosedMethod = "TableClosed";

    private readonly Channel<(string Method, object Payload)> _channel =
        Channel.CreateUnbounded<(string, object)>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly Task _drain;
    private volatile bool _superseded;

    public ConnectionSink(IHubContext<TableHub> hub, string connectionId)
    {
        _drain = Task.Run(async () =>
        {
            try
            {
                await foreach (var (method, payload) in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
                {
                    await hub.Clients.Client(connectionId).SendAsync(method, payload).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // The connection is gone; the room learns about it from the hub's disconnect handler.
            }
        });
    }

    public void PushRoom(RoomStateDto state) => _channel.Writer.TryWrite((RoomMethod, state));

    public void PushUpdate(TableUpdateDto update) => _channel.Writer.TryWrite((UpdateMethod, update));

    /// <summary>True once a newer connection of the same person took over; the hub then refuses its calls.</summary>
    public bool IsSuperseded => _superseded;

    public void PushSuperseded()
    {
        _superseded = true;
        _channel.Writer.TryWrite((SupersededMethod, new SupersededDto(TableUpdateDto.CurrentProtocolVersion)));
    }

    public void PushClosed(string reason) =>
        _channel.Writer.TryWrite((ClosedMethod, new TableClosedDto(TableUpdateDto.CurrentProtocolVersion, reason)));

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _drain.ConfigureAwait(false);
    }
}
