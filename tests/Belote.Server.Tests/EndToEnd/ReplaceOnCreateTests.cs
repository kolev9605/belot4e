using System.Threading.Channels;
using Belote.Table;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Belote.Server.Tests;

public class ReplaceOnCreateTests
{
    private static async Task<HubConnection> ConnectWithTableClosedAsync(
        string hubUrl, Channel<TableClosedDto> closed)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, HttpTransportType.WebSockets)
            .Build();
        connection.On<TableClosedDto>("TableClosed", m => closed.Writer.TryWrite(m));
        await connection.StartAsync();
        return connection;
    }

    [Fact]
    public async Task AnotherConnectionReplacesAnUnfinishedGameAndTheDisplacedOneIsToldSo()
    {
        await using var server = await LiveServer.StartAsync();
        var closed = Channel.CreateUnbounded<TableClosedDto>();
        await using var first = await ConnectWithTableClosedAsync(server.HubUrl, closed);

        var created = await first.InvokeAsync<CreateTableResultDto>("CreateTable", new CreateTableRequest(5, 40));
        Assert.True(created.Ok);
        var seated = await first.InvokeAsync<TakeSeatResultDto>("TakeSeat", new TakeSeatRequest("south"));
        Assert.True(seated.Ok);

        // The owning connection still gets tableExists (002 behaviour).
        var same = await first.InvokeAsync<CreateTableResultDto>("CreateTable", new CreateTableRequest(6, 0));
        Assert.Equal("tableExists", same.Rejection!.Category);

        await using var second = await TableClient.ConnectAsync(server.HubUrl);
        var replaced = await second.CreateTableAsync(7, botDelayMs: 0);
        Assert.True(replaced.Ok, replaced.Rejection?.Code);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var message = await closed.Reader.ReadAsync(cts.Token);
        Assert.Equal("replaced", message.Reason);
        Assert.Equal(1, message.ProtocolVersion);

        var intent = await first.InvokeAsync<IntentResultDto>("SubmitIntent", new IntentDto("pass", "south"));
        Assert.Equal("notSeated", intent.Rejection!.Category);
        var view = await first.InvokeAsync<GetViewResultDto>("GetView");
        Assert.Equal("notSeated", view.Rejection!.Category);

        // The new table plays normally.
        Assert.True((await second.TakeSeatAsync("south")).Ok);
        var updates = await second.PlayToEndAsync(ClientPolicies.First);
        Assert.True(updates[^1].Finished);
    }

    [Fact]
    public async Task AGameLeftByADisconnectedPlayerDoesNotBlockANewOne()
    {
        await using var server = await LiveServer.StartAsync();
        var client = await TableClient.ConnectAsync(server.HubUrl);
        Assert.True((await client.CreateTableAsync(5, botDelayMs: 40)).Ok);
        Assert.True((await client.TakeSeatAsync("south")).Ok);
        await client.DisposeAsync();

        await using var other = await TableClient.ConnectAsync(server.HubUrl);
        var created = await other.CreateTableAsync(9, botDelayMs: 0);
        Assert.True(created.Ok, created.Rejection?.Code);
        Assert.True((await other.TakeSeatAsync("south")).Ok);
        var updates = await other.PlayToEndAsync(ClientPolicies.First);
        Assert.True(updates[^1].Finished);
    }
}
