using Microsoft.Extensions.Time.Testing;
using Belote.Core;
using Belote.Simulation;
using Belote.Table;

namespace Belote.Table.Tests;

/// <summary>One test per row of the rejection catalogue; every case also proves state is unchanged.</summary>
public class RejectionTests
{
    /// <summary>Plays the first legal action for South until <paramref name="stop"/> holds on a South turn.</summary>
    private static async Task<TableUpdateDto> PlayUntilAsync(
        LiveTable table,
        Recorder recorder,
        Func<TableUpdateDto, bool> stop)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Assert.True((await table.TakeSeatAsync(Harness.Client)).Ok);
        await foreach (var update in recorder.Reader.ReadAllAsync(cts.Token))
        {
            if (update.View.LegalActions.Count == 0)
            {
                Assert.False(update.Finished, "The game finished before the scenario was reached.");
                continue;
            }

            if (stop(update))
            {
                return update;
            }

            var outcome = await table.SubmitAsync(Harness.Client, update.View.LegalActions[0]);
            Assert.IsType<IntentAccepted>(outcome);
        }

        throw new InvalidOperationException("Scenario never reached.");
    }

    private static async Task<Rejection> AssertRejectedUnchangedAsync(
        LiveTable table,
        Recorder recorder,
        Principal sender,
        IntentDto intent)
    {
        var before = table.Snapshot;
        int updates = recorder.Updates.Count;

        var outcome = await table.SubmitAsync(sender, intent);

        var rejected = Assert.IsType<IntentRejected>(outcome);
        var after = table.Snapshot;
        Assert.Same(before, after);
        Assert.Same(before.State, after.State);
        Assert.Same(before.Events, after.Events);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(updates, recorder.Updates.Count);
        Assert.False(string.IsNullOrWhiteSpace(rejected.Rejection.Detail));
        return rejected.Rejection;
    }

    private static (LiveTable Table, Recorder Recorder, IDisposable Sub) Create(ulong seed, TimeProvider? time = null, TimeSpan? delay = null)
    {
        var table = Harness.NewTable(seed, delay, time);
        var recorder = new Recorder();
        return (table, recorder, table.Subscribe(recorder));
    }

    [Fact]
    public async Task Malformed_IntentIsRejected()
    {
        var (table, recorder, sub) = Create(1);
        await using var _ = table;
        using var __ = sub;
        await PlayUntilAsync(table, recorder, u => true);

        var r = await AssertRejectedUnchangedAsync(table, recorder, Harness.Client, new IntentDto("teleport", "south"));

        Assert.Equal(RejectionCategory.Malformed, r.Category);
        Assert.Equal("Malformed", r.Code);
    }

    [Fact]
    public async Task NotSeated_StrangerIsRejected()
    {
        var (table, recorder, sub) = Create(1);
        await using var _ = table;
        using var __ = sub;
        await PlayUntilAsync(table, recorder, u => true);

        var r = await AssertRejectedUnchangedAsync(
            table, recorder, new ConnectionPrincipal("stranger"), new IntentDto("pass", "south"));

        Assert.Equal(RejectionCategory.NotSeated, r.Category);
        Assert.Equal("NotSeated", r.Code);
    }

    [Fact]
    public async Task GameNotStarted_BeforeSeating()
    {
        var (table, recorder, sub) = Create(1);
        await using var _ = table;
        using var __ = sub;

        var r = await AssertRejectedUnchangedAsync(table, recorder, Harness.Client, new IntentDto("pass", "south"));

        Assert.Equal(RejectionCategory.GameNotInProgress, r.Category);
        Assert.Equal("GameNotStarted", r.Code);
        Assert.Equal(RejectionCategory.GameNotInProgress, table.GetView(Harness.Client).Rejection!.Category);
    }

    [Fact]
    public async Task BidNotHigher_IsIllegal()
    {
        var (table, recorder, sub) = Create(1);
        await using var _ = table;
        using var __ = sub;
        var update = await PlayUntilAsync(
            table, recorder, u => u.View.Phase == "bidding" && u.View.StandingBid is not null && !u.View.Contra);

        var r = await AssertRejectedUnchangedAsync(
            table, recorder, Harness.Client, new IntentDto("bid", "south", update.View.StandingBid));

        Assert.Equal(RejectionCategory.Illegal, r.Category);
        Assert.Equal("BidNotHigher", r.Code);
    }

    [Fact]
    public async Task WrongPhase_AnnouncementWhileBidding()
    {
        var (table, recorder, sub) = Create(1);
        await using var _ = table;
        using var __ = sub;
        await PlayUntilAsync(table, recorder, u => u.View.Phase == "bidding");

        var announce = new IntentDto("announce", "south", Announcement: new AnnouncementSpecDto("sequence", "hearts", "Q", 3));
        var r = await AssertRejectedUnchangedAsync(table, recorder, Harness.Client, announce);

        Assert.Equal(RejectionCategory.Illegal, r.Category);
        Assert.Equal("WrongPhase", r.Code);
    }

    [Fact]
    public async Task CardNotInHand_IsIllegal()
    {
        var (table, recorder, sub) = Create(1);
        await using var _ = table;
        using var __ = sub;
        var update = await PlayUntilAsync(table, recorder, u => u.View.Phase == "playing");

        var foreign = Deck.Standard32.Select(c => c.ToString()).First(c => !update.View.OwnHand.Contains(c));
        var r = await AssertRejectedUnchangedAsync(table, recorder, Harness.Client, new IntentDto("play", "south", Card: foreign));

        Assert.Equal(RejectionCategory.Illegal, r.Category);
        Assert.Equal("CardNotInHand", r.Code);
    }

    [Fact]
    public async Task MustFollowSuit_IsIllegal()
    {
        var (table, recorder, sub) = Create(1);
        await using var _ = table;
        using var __ = sub;
        var update = await PlayUntilAsync(table, recorder, u => OffSuitCard(u) is not null);

        var r = await AssertRejectedUnchangedAsync(
            table, recorder, Harness.Client, new IntentDto("play", "south", Card: OffSuitCard(update)));

        Assert.Equal(RejectionCategory.Illegal, r.Category);
        Assert.Equal("MustFollowSuit", r.Code);
    }

    /// <summary>A card of another suit while holding the led suit, in a plain suit-following situation.</summary>
    private static string? OffSuitCard(TableUpdateDto u)
    {
        if (u.View.Phase != "playing" || u.View.CurrentTrick.Count == 0 || u.View.Contract is null)
        {
            return null;
        }

        var led = Card.Parse(u.View.CurrentTrick[0].Card).Suit;
        var hand = u.View.OwnHand.Select(Card.Parse).ToArray();
        if (!hand.Any(c => c.Suit == led))
        {
            return null;
        }

        var off = hand.Where(c => c.Suit != led).Select(c => c.ToString()).FirstOrDefault();
        return off;
    }

    [Fact]
    public async Task OutOfTurn_AndNotOwner_WhileABotIsToAct()
    {
        // A fake clock holds the bots mid-delay so the table stays on a bot's turn.
        for (ulong seed = 1; seed < 40; seed++)
        {
            var time = new FakeTimeProvider();
            var (table, recorder, sub) = Create(seed, time, TimeSpan.FromMilliseconds(500));
            await using var _ = table;
            using var __ = sub;
            await table.TakeSeatAsync(Harness.Client);
            var toAct = table.Snapshot.State!.ToAct;
            if (toAct == Seat.South)
            {
                continue;
            }

            var own = await AssertRejectedUnchangedAsync(table, recorder, Harness.Client, new IntentDto("pass", "south"));
            Assert.Equal(RejectionCategory.OutOfTurn, own.Category);
            Assert.Equal("NotYourTurn", own.Code);

            foreach (var seat in new[] { "north", "east", "west" })
            {
                var r = await AssertRejectedUnchangedAsync(table, recorder, Harness.Client, new IntentDto("pass", seat));
                Assert.Equal(RejectionCategory.NotOwner, r.Category);
                Assert.Equal("NotOwner", r.Code);
            }

            return;
        }

        Assert.Fail("No seed started with a bot to act.");
    }

    [Fact]
    public async Task NotOwner_OnSouthsOwnTurn()
    {
        var (table, recorder, sub) = Create(1);
        await using var _ = table;
        using var __ = sub;
        await PlayUntilAsync(table, recorder, u => true);

        var r = await AssertRejectedUnchangedAsync(table, recorder, Harness.Client, new IntentDto("pass", "north"));

        Assert.Equal(RejectionCategory.NotOwner, r.Category);
    }

    [Fact]
    public async Task RejectedIntentDoesNotBlockTheNextLegalOne()
    {
        var (table, recorder, sub) = Create(1);
        await using var _ = table;
        using var __ = sub;
        var update = await PlayUntilAsync(table, recorder, u => true);

        await AssertRejectedUnchangedAsync(table, recorder, Harness.Client, new IntentDto("nonsense", "south"));
        var outcome = await table.SubmitAsync(Harness.Client, update.View.LegalActions[0]);

        var accepted = Assert.IsType<IntentAccepted>(outcome);
        Assert.Equal(update.Version + 1, accepted.Version);
    }

    [Fact]
    public async Task GameFinished_IsRejectedAndUnchanged()
    {
        var (table, recorder, sub) = Create(2);
        await using var _ = table;
        using var __ = sub;
        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        var r = await AssertRejectedUnchangedAsync(table, recorder, Harness.Client, new IntentDto("pass", "south"));

        Assert.Equal(RejectionCategory.GameNotInProgress, r.Category);
        Assert.Equal("GameFinished", r.Code);
        Assert.Contains(Rejection.CategoryName(r.Category), new[] { "gameNotInProgress" });
        Assert.Equal(JsonNames.Of(Seat.South), recorder.Updates[^1].View.Seat);
    }
}
