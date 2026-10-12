using System.Threading.Channels;
using Belote.Core;
using Belote.Table;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Belote.Server.Tests;

/// <summary>The automated "player": a SignalR client that records every pushed update in order.</summary>
internal sealed class TableClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly Channel<TableUpdateDto> _updates = Channel.CreateUnbounded<TableUpdateDto>();
    private readonly List<TableUpdateDto> _all = new();
    private readonly object _lock = new();

    private TableClient(HubConnection connection)
    {
        _connection = connection;
        _connection.On<TableUpdateDto>("Update", update =>
        {
            lock (_lock)
            {
                _all.Add(update);
            }

            _updates.Writer.TryWrite(update);
        });
    }

    public ChannelReader<TableUpdateDto> Updates => _updates.Reader;

    public IReadOnlyList<TableUpdateDto> AllUpdates
    {
        get
        {
            lock (_lock)
            {
                return _all.ToArray();
            }
        }
    }

    public static async Task<TableClient> ConnectAsync(string hubUrl)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, HttpTransportType.WebSockets)
            .Build();
        var client = new TableClient(connection);
        await connection.StartAsync();
        return client;
    }

    public Task<CreateTableResultDto> CreateTableAsync(ulong? seed = null, int botDelayMs = 0) =>
        _connection.InvokeAsync<CreateTableResultDto>("CreateTable", new CreateTableRequest(seed, botDelayMs));

    public Task<CreateTableResultDto> CreateTableWithoutDelayAsync(ulong seed) =>
        _connection.InvokeAsync<CreateTableResultDto>("CreateTable", new CreateTableRequest(seed, null));

    public Task<TakeSeatResultDto> TakeSeatAsync(string seat = "south") =>
        _connection.InvokeAsync<TakeSeatResultDto>("TakeSeat", new TakeSeatRequest(seat));

    public Task<IntentResultDto> SubmitAsync(IntentDto intent) =>
        _connection.InvokeAsync<IntentResultDto>("SubmitIntent", intent);

    public Task<GetViewResultDto> GetViewAsync() =>
        _connection.InvokeAsync<GetViewResultDto>("GetView");

    public async Task<IReadOnlyList<TableUpdateDto>> ReadUpdatesAsync(TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
        var list = new List<TableUpdateDto>();
        while (_updates.Reader.TryRead(out var u))
        {
            list.Add(u);
        }

        return list;
    }

    /// <summary>
    /// Plays South until a finished update arrives. Asserts every update has the next version, no
    /// legal intent is rejected, and returns the updates received for this game.
    /// </summary>
    public async Task<IReadOnlyList<TableUpdateDto>> PlayToEndAsync(
        Func<TableUpdateDto, IntentDto> choose,
        TimeSpan? timeout = null,
        int firstVersion = 0)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(60));
        var received = new List<TableUpdateDto>();
        int expected = firstVersion;
        await foreach (var update in _updates.Reader.ReadAllAsync(cts.Token))
        {
            Assert.Equal(expected++, update.Version);
            received.Add(update);
            if (update.Finished)
            {
                break;
            }

            if (update.View.LegalActions.Count == 0)
            {
                continue;
            }

            var intent = choose(update);
            var result = await _connection.InvokeAsync<IntentResultDto>("SubmitIntent", intent, cts.Token);
            Assert.True(
                result.Accepted,
                $"Legal intent rejected at version {update.Version}: {result.Rejection?.Code}: {result.Rejection?.Detail}");
        }

        return received;
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    public Task StopAsync() => _connection.StopAsync();
}

internal static class ClientPolicies
{
    public static Func<TableUpdateDto, IntentDto> SeededSouth(ulong seed)
    {
        var random = new SeededRandom(Seeds.ForSeat(seed, Seat.South));
        return update => update.View.LegalActions[random.NextInt(update.View.LegalActions.Count)];
    }

    public static Func<TableUpdateDto, IntentDto> First { get; } = u => u.View.LegalActions[0];
}
