using Belote.Core;
using Belote.Table;

namespace Belote.Server.Tests;

public class RejectionCatalogue
{
    private static async Task SettleAsync() => await Task.Delay(150);

    /// <summary>A seed whose first player to act is a bot, so a long bot delay keeps it that bot's turn.</summary>
    private static ulong SeedWithBotFirst()
    {
        for (ulong seed = 1; seed < 100; seed++)
        {
            if (GameEngine.Start(RuleSets.Bulgarian, seed).State.ToAct != Seat.South)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("No seed found.");
    }

    [Fact]
    public async Task SC003_BeforeSeatingAndOutOfTurnAndNotOwner_AreRefusedAndStateIsIdentical()
    {
        await using var server = await LiveServer.StartAsync();
        await using var client = await TableClient.ConnectAsync(server.HubUrl);

        // No table yet.
        var none = await client.SubmitAsync(new IntentDto("pass", "south"));
        Assert.False(none.Accepted);
        Assert.Equal("notSeated", none.Rejection!.Category);
        Assert.Equal("notSeated", (await client.GetViewAsync()).Rejection!.Category);

        // Table created, not seated.
        ulong seed = SeedWithBotFirst();
        Assert.True((await client.CreateTableAsync(seed, botDelayMs: 5000)).Ok);
        var early = await client.SubmitAsync(new IntentDto("pass", "south"));
        Assert.Equal("gameNotInProgress", early.Rejection!.Category);
        Assert.Equal("GameNotStarted", early.Rejection.Code);
        Assert.Equal("gameNotInProgress", (await client.GetViewAsync()).Rejection!.Category);

        var wrongSeat = await client.TakeSeatAsync("north");
        Assert.False(wrongSeat.Ok);
        Assert.Equal("notOwner", wrongSeat.Rejection!.Category);
        var badSeat = await client.TakeSeatAsync("nowhere");
        Assert.Equal("malformed", badSeat.Rejection!.Category);

        var second = await client.CreateTableAsync(seed + 1, botDelayMs: 0);
        Assert.False(second.Ok);
        Assert.Equal("tableExists", second.Rejection!.Category);

        Assert.True((await client.TakeSeatAsync("south")).Ok);
        var again = await client.TakeSeatAsync("south");
        Assert.Equal("tableExists", again.Rejection!.Category);
        Assert.Equal("SeatTaken", again.Rejection.Code);
        var third = await client.CreateTableAsync(seed + 1, botDelayMs: 0);
        Assert.Equal("tableExists", third.Rejection!.Category);

        // Mid-game, with a bot holding the turn.
        await SettleAsync();
        var snapshot = server.Registry.Current!.Snapshot;
        int updates = client.AllUpdates.Count;
        Assert.Equal(1, updates);

        var cases = new (IntentDto Intent, string Category, string Code)[]
        {
            (new IntentDto("pass", "south"), "outOfTurn", "NotYourTurn"),
            (new IntentDto("pass", "north"), "notOwner", "NotOwner"),
            (new IntentDto("pass", "east"), "notOwner", "NotOwner"),
            (new IntentDto("play", "west", Card: "AS"), "notOwner", "NotOwner"),
            (new IntentDto("teleport", "south"), "malformed", "Malformed"),
            (new IntentDto("play", "south", Card: "ZZ"), "malformed", "Malformed"),
            (new IntentDto("pass", null), "malformed", "Malformed"),
        };
        foreach (var (intent, category, code) in cases)
        {
            var result = await client.SubmitAsync(intent);

            Assert.False(result.Accepted);
            Assert.Equal(category, result.Rejection!.Category);
            Assert.Equal(code, result.Rejection.Code);
            Assert.False(string.IsNullOrWhiteSpace(result.Rejection.Detail));
            Assert.Same(snapshot, server.Registry.Current!.Snapshot);
        }

        await SettleAsync();
        Assert.Equal(updates, client.AllUpdates.Count);
        var view = await client.GetViewAsync();
        Assert.Equal(0, view.Update!.Version);
    }

    [Fact]
    public async Task SC003_IllegalAndFinishedIntents_AreRefusedAndStateIsIdentical()
    {
        await using var server = await LiveServer.StartAsync();
        await using var client = await TableClient.ConnectAsync(server.HubUrl);
        Assert.True((await client.CreateTableAsync(7, botDelayMs: 0)).Ok);
        Assert.True((await client.TakeSeatAsync("south")).Ok);

        // Wait for South's first turn (bidding).
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        TableUpdateDto turn;
        do
        {
            turn = await client.Updates.ReadAsync(cts.Token);
        }
        while (turn.View.LegalActions.Count == 0);

        await SettleAsync();
        var snapshot = server.Registry.Current!.Snapshot;
        int updates = client.AllUpdates.Count;

        var illegal = new[]
        {
            (new IntentDto("announce", "south", Announcement: new AnnouncementSpecDto("sequence", "hearts", "Q", 3)), "WrongPhase"),
            (new IntentDto("play", "south", Card: turn.View.OwnHand[0]), "WrongPhase"),
            (new IntentDto("recontra", "south"), "RecontraNotAllowed"),
        };
        foreach (var (intent, code) in illegal)
        {
            var result = await client.SubmitAsync(intent);

            Assert.False(result.Accepted);
            Assert.Equal("illegal", result.Rejection!.Category);
            Assert.Equal(code, result.Rejection.Code);
            Assert.Same(snapshot, server.Registry.Current!.Snapshot);
        }

        await SettleAsync();
        Assert.Equal(updates, client.AllUpdates.Count);

        // A legal intent still works afterwards, and the game can be finished normally.
        var ok = await client.SubmitAsync(turn.View.LegalActions[0]);
        Assert.True(ok.Accepted);
        await client.PlayToEndAsync(ClientPolicies.First, TimeSpan.FromSeconds(30), firstVersion: turn.Version + 1);
    }
}
