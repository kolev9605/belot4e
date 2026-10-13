using Belote.Bots;
using Belote.Core;
using Belote.Table;
using Xunit;

namespace Belote.Table.Tests;

public class SmartBotTableTests
{
    [Fact]
    public async Task A_default_table_plays_a_whole_game_against_smart_bots_without_rejections()
    {
        await using var table = Harness.NewTable(21);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);

        var outcome = await Harness.PlayToEndAsync(table, recorder, Policies.First);

        Assert.Empty(outcome.Rejections);
        var log = outcome.Finished!.Log;
        Assert.Empty(log.Rejections);
        Assert.All(new[] { Seat.North, Seat.East, Seat.West }, s => Assert.Equal("SmartBot", log.Seats[s]));
    }

    [Fact]
    public async Task A_caller_supplied_bot_factory_still_overrides_the_default()
    {
        await using var table = Harness.NewTable(
            22, bots: seat => new RandomLegalBot(Seeds.ForSeat(22, seat)));
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);

        var outcome = await Harness.PlayToEndAsync(table, recorder, Policies.First);

        Assert.Empty(outcome.Rejections);
        Assert.NotNull(outcome.Finished);
        Assert.DoesNotContain("SmartBot", outcome.Finished!.Log.Seats.Values);
    }
}
