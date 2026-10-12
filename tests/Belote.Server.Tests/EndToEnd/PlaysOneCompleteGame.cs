using Belote.Core;
using Belote.Table;

namespace Belote.Server.Tests;

public class PlaysOneCompleteGame
{
    [Fact]
    public async Task ClientPlaysFromFirstDealToAFinalWinner()
    {
        await using var server = await LiveServer.StartAsync();
        await using var client = await TableClient.ConnectAsync(server.HubUrl);

        var created = await client.CreateTableAsync(seed: 42, botDelayMs: 0);
        Assert.True(created.Ok);
        Assert.NotNull(created.TableId);
        var seated = await client.TakeSeatAsync("south");
        Assert.True(seated.Ok);
        Assert.Equal(0, seated.Version);

        var updates = await client.PlayToEndAsync(ClientPolicies.First);

        var final = updates[^1];
        Assert.True(final.Finished);
        Assert.Equal("finished", final.View.Phase);
        Assert.NotNull(final.View.Winner);
        Assert.Empty(final.View.LegalActions);
        int target = RuleSets.Bulgarian.Match.TargetScore;
        Assert.True(Math.Max(final.View.MatchScore.NorthSouth, final.View.MatchScore.EastWest) >= target);
        Assert.All(updates, u => Assert.Equal("south", u.View.Seat));

        var finished = Assert.Single(server.Registry.Finished.All);
        Assert.Equal(42UL, finished.Log.Seed);

        // Once finished, intents are refused and the client may start another table.
        var late = await client.SubmitAsync(new IntentDto("pass", "south"));
        Assert.False(late.Accepted);
        Assert.Equal("gameNotInProgress", late.Rejection!.Category);
        Assert.True((await client.CreateTableAsync(seed: 43, botDelayMs: 0)).Ok);
    }
}
