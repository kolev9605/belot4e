using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

/// <summary>Feature 006 contract: lastHand summarises the most recently scored hand (contracts/hand-summary.md).</summary>
public class HandSummaryTests
{
    private static async Task<IReadOnlyList<TableUpdateDto>> PlayAsync(ulong seed)
    {
        await using var table = Harness.NewTable(seed);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);
        await Harness.PlayToEndAsync(table, recorder, Policies.SeededSouth(seed));
        return recorder.Updates;
    }

    private static IEnumerable<HandSummaryDto> DistinctSummaries(IEnumerable<TableUpdateDto> updates)
    {
        HandSummaryDto? previous = null;
        foreach (var update in updates)
        {
            var current = update.View.LastHand;
            if (current is not null && (previous is null || current.DealNumber != previous.DealNumber))
            {
                yield return current;
            }

            previous = current ?? previous;
        }
    }

    [Fact]
    public async Task LastHandIsNullBeforeTheFirstHandIsScored()
    {
        var updates = await PlayAsync(3);
        Assert.Null(updates[0].View.LastHand);
        var firstScored = updates.First(u => u.View.LastHand is not null);
        Assert.True(firstScored.View.DealNumber >= 1);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(7UL)]
    public async Task WonCardsOfBothTeamsAreDisjointAndCoverTheWholeHand(ulong seed)
    {
        var updates = await PlayAsync(seed);
        var summaries = DistinctSummaries(updates).ToList();
        Assert.NotEmpty(summaries);

        foreach (var s in summaries)
        {
            var ns = s.Teams.NorthSouth.WonCards;
            var ew = s.Teams.EastWest.WonCards;
            Assert.Equal(32, ns.Count + ew.Count);
            Assert.Empty(ns.Intersect(ew));
            Assert.Equal(32, ns.Concat(ew).Distinct().Count());
            Assert.Equal(0, ns.Count % 4);
            Assert.Equal(0, ew.Count % 4);
        }
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(7UL)]
    public async Task BreakdownPartsAddUpToTheRawTotal(ulong seed)
    {
        var updates = await PlayAsync(seed);
        foreach (var s in DistinctSummaries(updates))
        {
            foreach (var team in new[] { s.Teams.NorthSouth, s.Teams.EastWest })
            {
                Assert.Equal(
                    team.CardPoints + team.LastTrickBonus + team.AnnouncementPoints + team.BelotePoints + team.AllTricksBonus,
                    team.RawTotal);
            }
        }
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public async Task MatchScoreIsThePreviousScorePlusTheHandGamePoints(ulong seed)
    {
        var updates = await PlayAsync(seed);
        int ns = 0;
        int ew = 0;
        foreach (var s in DistinctSummaries(updates))
        {
            ns += s.Teams.NorthSouth.GamePoints;
            ew += s.Teams.EastWest.GamePoints;
            Assert.Equal(ns, s.MatchScore.NorthSouth);
            Assert.Equal(ew, s.MatchScore.EastWest);
        }

        var last = updates[^1].View;
        Assert.Equal(last.LastHand!.MatchScore, last.MatchScore);
        Assert.NotNull(last.LastHand.Winner);
        Assert.Equal(last.Winner, last.LastHand.Winner);
    }

    [Fact]
    public async Task LastHandStaysUntilTheNextHandIsScoredAndOnlyContainsPlayedCards()
    {
        var updates = await PlayAsync(5);
        HandSummaryDto? shown = null;
        foreach (var update in updates)
        {
            var current = update.View.LastHand;
            if (shown is not null)
            {
                Assert.NotNull(current);
                if (current!.DealNumber == shown.DealNumber)
                {
                    Assert.Equal(Harness.Json(shown), Harness.Json(current));
                }
            }

            shown = current ?? shown;
        }
    }

    [Fact]
    public async Task EveryCardInASummaryWasPlayedInThatDeal()
    {
        await using var table = Harness.NewTable(11);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);
        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        var events = table.Snapshot.Events;
        foreach (var s in DistinctSummaries(recorder.Updates))
        {
            var played = events.OfType<CardPlayed>()
                .Where(e => e.Deal == s.DealNumber)
                .Select(e => e.Card.ToString())
                .OrderBy(c => c)
                .ToArray();
            var shown = s.Teams.NorthSouth.WonCards.Concat(s.Teams.EastWest.WonCards).OrderBy(c => c).ToArray();
            Assert.Equal(played, shown);
        }
    }
}
