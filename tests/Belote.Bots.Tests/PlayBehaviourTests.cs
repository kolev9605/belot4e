using System.Collections.Immutable;
using Belote.Core;
using Xunit;

namespace Belote.Bots.Tests;

public class PlayBehaviourTests
{
    private static PlayerAction Decide(SeatView view)
    {
        var action = V.Decide(view);
        Assert.Contains(action, LegalActions.For(view));
        return action;
    }

    private static PlayerAction Play(string card) => new PlayCardAction(Card.Parse(card));

    [Fact]
    public void It_declares_the_most_valuable_announcement_before_playing_a_card()
    {
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.North, "JH JS JD JC 7S 8S 9S QD");
        Assert.Equal(new AnnounceAction(AnnouncementSpec.Carre(Rank.Jack)), Decide(view));
    }

    [Fact]
    public void It_declares_a_sequence_when_it_is_the_only_announcement()
    {
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.North, "7S 8S 9S QD KC 10C 8H 9C");
        var action = Assert.IsType<AnnounceAction>(Decide(view));
        Assert.Equal(AnnouncementKind.Sequence, action.Spec.Kind);
    }

    [Fact]
    public void It_feeds_a_trick_its_partner_is_securely_winning()
    {
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "10D 7D 8S QS 7C 9C KC 7H",
            trick: new[] { (Seat.North, "AD"), (Seat.East, "7D") });
        Assert.Equal(Play("10D"), Decide(view));
    }

    [Fact]
    public void It_keeps_valuable_cards_when_the_partner_win_is_not_secure()
    {
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "KD 8D 8S QS 7C 9C KC 7H",
            trick: new[] { (Seat.North, "10D"), (Seat.East, "7D") });
        Assert.Equal(Play("8D"), Decide(view));
    }

    [Fact]
    public void It_wins_with_the_cheapest_card_that_beats_the_opponent()
    {
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.North, "QS AS 7S 8H 9H 7C 9C KC",
            trick: new[] { (Seat.East, "JS") });
        Assert.Equal(Play("QS"), Decide(view));
    }

    [Fact]
    public void It_discards_its_least_valuable_card_when_it_cannot_win()
    {
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.North, "KS 9S 8S 7C 9C KC 7D 8D",
            trick: new[] { (Seat.East, "AS") });
        Assert.Equal(Play("8S"), Decide(view));
    }

    [Fact]
    public void It_cashes_a_master_card_when_leading()
    {
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "AD 7D 8S 9S 7C 9C KC 10H");
        Assert.Equal(Play("AD"), Decide(view));
    }

    [Fact]
    public void The_declarer_draws_trumps_with_its_master_trump()
    {
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.South, "JH 9H 7S 9S KS 7C 9C KC");
        Assert.Equal(Play("JH"), Decide(view));
    }

    [Fact]
    public void It_does_not_lead_an_unprotected_ten()
    {
        var view = V.Playing(Seat.South, ContractKind.Spades, Seat.East, "10D 7D 7C 9C KC 7H 9H KH");
        var card = Assert.IsType<PlayCardAction>(Decide(view)).Card;
        Assert.NotEqual(Card.Parse("10D"), card);
    }

    [Fact]
    public void In_no_trump_it_feeds_using_the_no_trump_point_table()
    {
        var view = V.Playing(Seat.South, ContractKind.NoTrump, Seat.East, "10D 8D 7S 9S KS 7C 9C KC",
            trick: new[] { (Seat.North, "AD"), (Seat.East, "7D") });
        Assert.Equal(Play("10D"), Decide(view));
    }

    [Fact]
    public void In_all_trump_it_leads_the_master_jack()
    {
        var view = V.Playing(Seat.South, ContractKind.AllTrump, Seat.East, "JS 7D 8D KH 7C 9C QC AD");
        Assert.Equal(Play("JS"), Decide(view));
    }

    [Fact]
    public void It_does_not_lead_into_an_opponent_known_to_trump_when_another_suit_is_safe()
    {
        // West discarded on a diamond lead, so diamonds are cuttable; the bot leads another suit.
        var trick = V.Trick(RuleSets.Bulgarian, ContractKind.Hearts,
            (Seat.South, "7D"), (Seat.West, "8S"), (Seat.North, "8D"), (Seat.East, "9D"));
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "10D JD 7C 9C 9H QS KS 8S",
            completed: ImmutableArray.Create(trick));
        var card = Assert.IsType<PlayCardAction>(Decide(view)).Card;
        Assert.NotEqual(Suit.Diamonds, card.Suit);
    }
}
