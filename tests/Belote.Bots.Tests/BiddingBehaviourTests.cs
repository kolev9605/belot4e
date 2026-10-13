using Belote.Core;
using Xunit;

namespace Belote.Bots.Tests;

public class BiddingBehaviourTests
{
    private const string Weak = "7C 8D 7H 8S 9D";
    private const string Hearts = "JH 9H AH KD 7C";
    private const string Aces = "AS AH AD AC 10S";
    private const string HeavyHearts = "JH 9H AH 10H KD";

    private static PlayerAction Decide(SeatView view)
    {
        var action = V.Decide(view);
        Assert.Contains(action, LegalActions.For(view));
        return action;
    }

    [Fact]
    public void A_weak_hand_passes()
    {
        Assert.IsType<PassAction>(Decide(V.Bidding(Seat.South, Weak)));
    }

    [Fact]
    public void A_strong_suit_hand_bids_that_suit()
    {
        Assert.Equal(new BidAction(ContractKind.Hearts), Decide(V.Bidding(Seat.South, Hearts)));
    }

    [Fact]
    public void It_outbids_an_opponent_when_a_higher_contract_is_strong()
    {
        var view = V.Bidding(Seat.South, Aces, ContractKind.Spades, Seat.East);
        Assert.Equal(new BidAction(ContractKind.NoTrump), Decide(view));
    }

    [Fact]
    public void It_passes_when_only_higher_contracts_are_available_and_weak()
    {
        var view = V.Bidding(Seat.South, Hearts, ContractKind.Spades, Seat.East);
        Assert.IsType<PassAction>(Decide(view));
    }

    [Fact]
    public void It_does_not_overcall_its_partner_without_a_clear_margin()
    {
        var justOver = V.Bidding(Seat.South, "JH 9H 7C 8D 7S", ContractKind.Clubs, Seat.North);
        Assert.IsType<PassAction>(Decide(justOver));

        var clear = V.Bidding(Seat.South, Hearts, ContractKind.Clubs, Seat.North);
        Assert.Equal(new BidAction(ContractKind.Hearts), Decide(clear));
    }

    [Fact]
    public void It_doubles_a_contract_it_holds_the_trumps_of_but_never_its_partner()
    {
        var opposing = V.Bidding(Seat.South, HeavyHearts, ContractKind.Hearts, Seat.East);
        Assert.IsType<ContraAction>(Decide(opposing));

        var partner = V.Bidding(Seat.South, HeavyHearts, ContractKind.Hearts, Seat.North);
        Assert.IsNotType<ContraAction>(Decide(partner));
    }

    [Fact]
    public void It_redoubles_with_a_strong_hand_and_otherwise_passes()
    {
        var strong = V.Bidding(Seat.South, HeavyHearts, ContractKind.Hearts, Seat.North, contra: true);
        Assert.IsType<RecontraAction>(Decide(strong));

        var weak = V.Bidding(Seat.South, Weak, ContractKind.Hearts, Seat.North, contra: true);
        Assert.IsType<PassAction>(Decide(weak));
    }
}
