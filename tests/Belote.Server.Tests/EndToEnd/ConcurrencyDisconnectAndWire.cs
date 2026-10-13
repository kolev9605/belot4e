using System.Reflection;
using Belote.Core;
using Belote.Table;
using Belote.Table.Tests;

namespace Belote.Server.Tests;

public class ConcurrencyDisconnectAndWire
{
    [Fact]
    public async Task SC005_SimultaneousIdenticalPlaysOverTheHub_ExactlyOneIsAccepted()
    {
        await using var server = await LiveServer.StartAsync();
        await using var client = await TableClient.ConnectAsync(server.HubUrl);
        Assert.True((await client.CreateTableAsync(12, botDelayMs: 0)).Ok);
        Assert.True((await client.TakeSeatAsync("south")).Ok);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        TableUpdateDto turn;
        while (true)
        {
            turn = await client.Updates.ReadAsync(cts.Token);
            if (turn.View.LegalActions.Count == 0)
            {
                continue;
            }

            if (turn.View.Phase == "playing" && turn.View.OwnHand.Count >= 6)
            {
                break;
            }

            Assert.True((await client.SubmitAsync(turn.View.LegalActions[0])).Accepted);
        }

        var card = turn.View.LegalActions.First(a => a.Kind == "play");
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => client.SubmitAsync(card)));

        Assert.Single(results, r => r.Accepted);
        Assert.Equal(15, results.Count(r => !r.Accepted));
        Assert.All(results.Where(r => !r.Accepted), r =>
            Assert.Contains(r.Rejection!.Code, new[] { "NotYourTurn", "CardNotInHand" }));

        // The table is still healthy: its log replays to the same state.
        await Task.Delay(100);
        var snapshot = server.Registry.Current!.Snapshot;
        Assert.NotEqual(TableLifecycle.Faulted, snapshot.Lifecycle);
        var (state, replayed) = Replay.Run(RuleSets.Bulgarian, 12, snapshot.Events);
        Assert.Equal(Lines.Of(snapshot.Events), Lines.Of(replayed));
        Assert.Equal(snapshot.State!.ToAct, state.ToAct);
    }

    [Fact]
    public async Task FR026_DroppedConnectionPausesTheTableAndTheHostKeepsServing()
    {
        await using var server = await LiveServer.StartAsync();
        var client = await TableClient.ConnectAsync(server.HubUrl);
        Assert.True((await client.CreateTableAsync(5, botDelayMs: 40)).Ok);
        Assert.True((await client.TakeSeatAsync("south")).Ok);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await client.Updates.ReadAsync(cts.Token);
        await client.Updates.ReadAsync(cts.Token);

        await client.DisposeAsync();

        var table = server.Registry.Current!;
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!table.Snapshot.Paused)
        {
            Assert.True(DateTime.UtcNow < until, "The table never paused.");
            await Task.Delay(10);
        }

        var frozen = table.Snapshot;
        await Task.Delay(400);
        Assert.Same(frozen, table.Snapshot);

        // The host is unaffected: a new connection is not blocked by the abandoned game (feature 003, FR-023).
        await using var other = await TableClient.ConnectAsync(server.HubUrl);
        var view = await other.GetViewAsync();
        Assert.Equal("notSeated", view.Rejection!.Category);
        var created = await other.CreateTableAsync(9, botDelayMs: 0);
        Assert.True(created.Ok, created.Rejection?.Code);
    }

    [Fact]
    public void FR025_NoWireTypeCanCarryTheLogTheSeedOrFullState()
    {
        var forbidden = new[] { typeof(GameEvent), typeof(GameState), typeof(HandState), typeof(Belote.Simulation.GameLog) };
        var wireTypes = typeof(TableUpdateDto).Assembly.GetTypes()
            .Where(t => t.Namespace == "Belote.Table" && (t.Name.EndsWith("Dto") || t.Name.EndsWith("Request")))
            .ToArray();
        Assert.NotEmpty(wireTypes);

        foreach (var type in wireTypes)
        {
            foreach (var property in type.GetProperties())
            {
                var propertyType = property.PropertyType;
                var inner = propertyType.IsGenericType ? propertyType.GetGenericArguments() : new[] { propertyType };
                Assert.DoesNotContain(inner, t => forbidden.Contains(t));

                // Only the creation request may mention a seed (it is input, never output).
                if (property.Name.Contains("seed", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Contains(type, new[] { typeof(CreateTableRequest), typeof(CreateRoomRequest) });
                }
            }
        }

        // Every hub method answers with a DTO, never with engine or log types.
        foreach (var method in typeof(TableHub).GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance))
        {
            var returned = method.ReturnType;
            if (returned.IsGenericType)
            {
                returned = returned.GetGenericArguments()[0];
            }

            Assert.True(
                returned == typeof(Task) || returned == typeof(void) || returned.Name.EndsWith("Dto"),
                $"{method.Name} returns {returned.Name}");
        }
    }
}
