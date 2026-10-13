using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

public class ViewPrivacyTests
{
    [Fact]
    public async Task FR017_NoMessageContainsACardOutsideOwnHandOrPublicTable()
    {
        int updatesChecked = 0;
        for (ulong seed = 1; seed <= 25; seed++)
        {
            await using var table = Harness.NewTable(seed);
            var recorder = new Recorder();
            recorder.Attach(table);
            using var _ = table.Subscribe(recorder);

            await Harness.PlayToEndAsync(table, recorder, Policies.SeededSouth(seed));

            var events = table.Snapshot.Events;
            var updates = recorder.Updates;
            var counts = recorder.EventCounts;
            Assert.Equal(updates.Count, counts.Count);
            for (int i = 0; i < updates.Count; i++)
            {
                var problems = PrivacyOracle.Violations(updates[i], events.Take(counts[i]).ToList());
                Assert.True(problems.Count == 0, $"seed {seed}: {string.Join("; ", problems)}");
                updatesChecked++;
            }
        }

        Assert.True(updatesChecked > 1000, $"only {updatesChecked} updates were checked");
    }

    [Fact]
    public async Task LastHandIsIdenticalForEverySeatAndHoldsOnlyPlayedCards()
    {
        await using var table = Harness.NewTable(9);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);
        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        var state = table.Snapshot.State!;
        var played = table.Snapshot.Events.OfType<CardPlayed>().Select(e => (e.Deal, e.Card.ToString())).ToHashSet();
        var summary = recorder.Updates[^1].View.LastHand!;
        foreach (var seat in new[] { Seat.North, Seat.East, Seat.South, Seat.West })
        {
            var view = SeatViewMapper.Map(Projection.For(state, seat), null, table.Snapshot.LastHand);
            Assert.Equal(Harness.Json(summary), Harness.Json(view.LastHand));
        }

        foreach (var card in summary.Teams.NorthSouth.WonCards.Concat(summary.Teams.EastWest.WonCards))
        {
            Assert.Contains((summary.DealNumber, card), played);
        }
    }

    [Fact]
    public async Task OracleDetectsAForeignCard()
    {
        await using var table = Harness.NewTable(4);
        var recorder = new Recorder();
        recorder.Attach(table);
        using var _ = table.Subscribe(recorder);
        await table.TakeSeatAsync(Harness.Client);
        var first = await recorder.Reader.ReadAsync();
        var events = table.Snapshot.Events.Take(recorder.EventCounts[0]).ToList();
        Assert.Empty(PrivacyOracle.Violations(first, events));

        var dealt = events.OfType<HandDealt>().Single();
        var foreign = dealt.Hands[(int)Seat.East][0].ToString();
        var leaked = first with { View = first.View with { OwnHand = first.View.OwnHand.Append(foreign).ToArray() } };

        Assert.Contains(PrivacyOracle.Violations(leaked, events), p => p.Contains(foreign));
    }

    [Fact]
    public async Task BiddingShowsOnlyTheCardsDealtBeforeBidding()
    {
        await using var table = Harness.NewTable(4);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);
        await table.TakeSeatAsync(Harness.Client);
        var first = await recorder.Reader.ReadAsync();

        var start = GameEngine.Start(RuleSets.Bulgarian, 4);
        var dealt = start.Events.OfType<HandDealt>().Single();
        Assert.Equal("bidding", first.View.Phase);
        Assert.Equal(dealt.DealtBeforeBidding, first.View.OwnHand.Count);
        Assert.True(dealt.DealtBeforeBidding < 8);
    }
}
