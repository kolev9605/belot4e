using Belote.Core;
using Xunit;

namespace Belote.Bots.Tests;

public class SmartBotContractTests
{
    [Fact]
    public void It_is_a_seat_controller()
    {
        Assert.IsAssignableFrom<IPlayer>(new SmartBot());
    }

    [Fact]
    public void It_throws_when_the_seat_has_no_legal_action()
    {
        var view = V.Bidding(Seat.South, "7C 8C 9C 10C JC") with { ToAct = Seat.West };
        var bot = new SmartBot();
        Assert.Throws<InvalidOperationException>(() => bot.DecideAsync(view, default));
    }

    [Fact]
    public void It_completes_synchronously_and_takes_a_forced_move()
    {
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "7S");
        var task = new SmartBot().DecideAsync(view, default);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(new PlayCardAction(Card.Parse("7S")), task.Result);
    }

    [Fact]
    public void Options_must_be_finite_and_non_negative()
    {
        Assert.Throws<ArgumentException>(() => new SmartBot(SmartBotOptions.Standard with { BidThreshold = -1 }));
        Assert.Throws<ArgumentException>(() => new SmartBot(SmartBotOptions.Standard with { NoTrumpScale = double.NaN }));
    }
}
