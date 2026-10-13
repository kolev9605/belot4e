using Belote.Core;
using Xunit;

namespace Belote.Bots.Tests;

public class TrickSituationTests
{
    private static TrickSituation Of(SeatView view) => TrickSituation.From(view, CardMemory.From(view));

    [Fact]
    public void Leading_has_no_winner_and_three_to_play_after()
    {
        var s = Of(V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "7C 8C 9C 10C JC QC KC AC"));
        Assert.True(s.IsLeading);
        Assert.Null(s.CurrentWinner);
        Assert.Equal(3, s.PositionFromLast);
        Assert.False(s.PartnerWinning);
    }

    [Fact]
    public void Partner_winning_with_a_master_is_secure_and_points_are_summed()
    {
        var s = Of(V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "7C 8C 9C 10C JC QC KC AC",
            trick: new[] { (Seat.North, "AD"), (Seat.East, "7D") }));
        Assert.Equal(Seat.North, s.CurrentWinner);
        Assert.True(s.PartnerWinning);
        Assert.Equal(1, s.PositionFromLast);
        Assert.Equal(11, s.PointsOnTable);
        Assert.True(s.PartnerSecure);
    }

    [Fact]
    public void A_trump_over_the_led_suit_changes_the_winner()
    {
        var s = Of(V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "7C 8C 9C 10C JC QC KC AC",
            trick: new[] { (Seat.North, "AD"), (Seat.East, "7H") }));
        Assert.Equal(Seat.East, s.CurrentWinner);
        Assert.False(s.PartnerWinning);
    }

    [Fact]
    public void Partner_winning_with_a_non_master_is_not_secure_unless_the_bot_is_last()
    {
        // South holds the king, so the ace of diamonds is still unseen and the ten is not master.
        var notLast = Of(V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "KD 8D 9C 10C JC QC KC AC",
            trick: new[] { (Seat.North, "10D"), (Seat.East, "7D") }));
        Assert.True(notLast.PartnerWinning);
        Assert.False(notLast.PartnerSecure);

        var last = Of(V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "KD 8D 9C 10C JC QC KC AC",
            trick: new[] { (Seat.West, "8S"), (Seat.North, "10S"), (Seat.East, "7S") }));
        Assert.Equal(0, last.PositionFromLast);
        Assert.True(last.PartnerSecure);
    }
}
