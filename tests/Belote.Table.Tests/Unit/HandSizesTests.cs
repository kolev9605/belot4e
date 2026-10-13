using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

public class HandSizesTests
{
    [Theory]
    [InlineData(11UL)]
    [InlineData(12UL)]
    public async Task HandSizesAreCountsOfCardsHeldAndFollowPlay(ulong seed)
    {
        await using var table = Harness.NewTable(seed);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);
        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        Assert.NotEmpty(recorder.Updates);
        foreach (var update in recorder.Updates)
        {
            var view = update.View;
            var sizes = view.HandSizes;
            Assert.Equal(view.OwnHand.Count, sizes.South);

            if (view.Phase == "bidding")
            {
                Assert.Equal(sizes.South, sizes.North);
                Assert.Equal(sizes.South, sizes.East);
                Assert.Equal(sizes.South, sizes.West);
                continue;
            }

            if (view.Phase != "playing")
            {
                continue;
            }

            var plays = view.CompletedTricks.SelectMany(t => t.Plays).Concat(view.CurrentTrick).ToArray();
            int Played(string seat) => plays.Count(p => p.Seat == seat);
            Assert.Equal(8 - Played("north"), sizes.North);
            Assert.Equal(8 - Played("east"), sizes.East);
            Assert.Equal(8 - Played("south"), sizes.South);
            Assert.Equal(8 - Played("west"), sizes.West);
        }
    }

    [Fact]
    public void HandSizesCarryOnlyIntegers()
    {
        Assert.All(
            typeof(HandSizesDto).GetProperties(),
            p => Assert.Equal(typeof(int), p.PropertyType));
    }
}
