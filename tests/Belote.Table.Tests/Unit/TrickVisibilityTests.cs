using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

public class TrickVisibilityTests
{
    [Fact]
    public async Task FR027_FourthCardUpdateShowsAllFourCardsAndTheWinner()
    {
        await using var table = Harness.NewTable(8);
        var recorder = new Recorder();
        recorder.Attach(table);
        using var _ = table.Subscribe(recorder);
        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        var events = table.Snapshot.Events;
        var updates = recorder.Updates;
        var counts = recorder.EventCounts;
        int tricksChecked = 0;
        for (int i = 1; i < updates.Count; i++)
        {
            var added = events.Skip(counts[i - 1]).Take(counts[i] - counts[i - 1]).ToList();
            var won = added.OfType<TrickWon>().FirstOrDefault();
            if (won is null)
            {
                continue;
            }

            var played = added.OfType<CardPlayed>().Single();
            var last = updates[i].View.LastCompletedTrick;
            Assert.NotNull(last);
            Assert.Equal(4, last!.Plays.Count);
            Assert.Equal(played.Card.ToString(), last.Plays[^1].Card);
            Assert.Equal(played.Seat.ToString().ToLowerInvariant(), last.Plays[^1].Seat);
            Assert.Equal(won.Winner.ToString().ToLowerInvariant(), last.Winner);
            Assert.Equal(won.TrickNumber, last.TrickNumber);
            Assert.Empty(updates[i].View.CurrentTrick);
            tricksChecked++;
        }

        Assert.True(tricksChecked >= 8);
    }

    [Fact]
    public async Task FR027_CompletedTrickPersistsUntilTheNextCardIsPlayed()
    {
        await using var table = Harness.NewTable(8);
        var recorder = new Recorder();
        recorder.Attach(table);
        using var _ = table.Subscribe(recorder);
        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        var events = table.Snapshot.Events;
        var updates = recorder.Updates;
        var counts = recorder.EventCounts;
        CompletedTrickDto? expected = null;
        for (int i = 0; i < updates.Count; i++)
        {
            var added = events.Skip(i == 0 ? 0 : counts[i - 1]).Take(counts[i] - (i == 0 ? 0 : counts[i - 1])).ToList();
            if (added.OfType<CardPlayed>().Any())
            {
                expected = added.OfType<TrickWon>().Any() ? updates[i].View.LastCompletedTrick : null;
            }

            Assert.Equal(Harness.Json(expected), Harness.Json(updates[i].View.LastCompletedTrick));
        }
    }

    [Fact]
    public async Task FR027_TheEighthTrickSurvivesTheHandRollover()
    {
        // Several seeds, so the check does not depend on one bot behaviour producing a multi-hand game.
        var rollover = new List<(TableUpdateDto a, TableUpdateDto b)>();
        for (ulong seed = 8; seed < 16; seed++)
        {
            await using var table = Harness.NewTable(seed);
            var recorder = new Recorder();
            recorder.Attach(table);
            using var _ = table.Subscribe(recorder);
            await Harness.PlayToEndAsync(table, recorder, Policies.First);

            var updates = recorder.Updates;
            rollover.AddRange(updates.Zip(updates.Skip(1), (a, b) => (a, b))
                .Where(p => p.b.View.DealNumber > p.a.View.DealNumber
                    && p.b.View.LastCompletedTrick is { TrickNumber: 8 } last
                    // The update in which the eighth trick completes, not a later redeal of a voided hand.
                    && (p.a.View.LastCompletedTrick?.DealNumber != last.DealNumber
                        || p.a.View.LastCompletedTrick?.TrickNumber != last.TrickNumber)));
        }

        Assert.NotEmpty(rollover);
        Assert.All(rollover, p =>
        {
            Assert.Equal(p.a.View.DealNumber, p.b.View.LastCompletedTrick!.DealNumber);
            Assert.Equal(4, p.b.View.LastCompletedTrick.Plays.Count);
        });
    }
}
