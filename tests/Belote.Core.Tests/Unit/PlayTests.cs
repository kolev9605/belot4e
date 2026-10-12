using System.Collections.Immutable;
using Belote.Core;
using Xunit;

namespace Belote.Core.Tests;

public class LegalCardsTests
{
    private static string[] Legal(ContractKind kind, Seat seat, string hand, params (Seat, string)[] trick) =>
        LegalCards.For(T.Rules, kind, seat, T.Hand(hand), T.Plays(trick))
            .Select(c => c.ToString())
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

    private static string[] Sorted(params string[] cards) => cards.OrderBy(s => s, StringComparer.Ordinal).ToArray();

    [Fact]
    public void The_leader_may_play_any_card()
    {
        Assert.Equal(Sorted("7S", "AH", "9D"), Legal(ContractKind.Hearts, Seat.South, "7S AH 9D"));
    }

    [Fact]
    public void R11_Must_follow_the_led_suit_in_a_plain_suit_with_no_duty_to_go_higher()
    {
        var legal = Legal(ContractKind.Hearts, Seat.South, "7S AS 9H", (Seat.East, "KS"));
        Assert.Equal(Sorted("7S", "AS"), legal);
    }

    [Fact]
    public void R12_Without_the_led_suit_must_trump_when_an_opponent_is_winning()
    {
        var legal = Legal(ContractKind.Hearts, Seat.South, "9H JH 7D", (Seat.West, "7S"), (Seat.North, "8S"), (Seat.East, "KS"));
        Assert.Equal(Sorted("9H", "JH"), legal);
    }

    [Fact]
    public void R12_Partner_winning_exempts_you_from_having_to_trump()
    {
        var legal = Legal(ContractKind.Hearts, Seat.South, "9H 7D 8C", (Seat.West, "7S"), (Seat.North, "AS"), (Seat.East, "8S"));
        Assert.Equal(Sorted("9H", "7D", "8C"), legal);
    }

    [Fact]
    public void R12_Without_trump_or_led_suit_any_card_is_legal()
    {
        var legal = Legal(ContractKind.Hearts, Seat.South, "7D 8C 9C", (Seat.West, "7S"), (Seat.North, "8S"), (Seat.East, "KS"));
        Assert.Equal(Sorted("7D", "8C", "9C"), legal);
    }

    [Fact]
    public void R13_Must_overtrump_an_opponents_trump_when_able()
    {
        var legal = Legal(ContractKind.Hearts, Seat.South, "JH 7H 8D", (Seat.West, "7S"), (Seat.North, "8S"), (Seat.East, "9H"));
        Assert.Equal(Sorted("JH"), legal);
    }

    [Fact]
    public void R13_When_unable_to_overtrump_any_card_may_be_played()
    {
        var legal = Legal(ContractKind.Hearts, Seat.South, "7H 8H 8D", (Seat.West, "7S"), (Seat.North, "8S"), (Seat.East, "9H"));
        Assert.Equal(Sorted("7H", "8H", "8D"), legal);
    }

    [Fact]
    public void R13_Trump_led_requires_a_higher_trump_if_you_have_one()
    {
        var withHigher = Legal(ContractKind.Hearts, Seat.South, "JH 8H AS", (Seat.West, "9H"));
        Assert.Equal(Sorted("JH"), withHigher);

        var withoutHigher = Legal(ContractKind.Hearts, Seat.South, "8H 7H AS", (Seat.West, "9H"));
        Assert.Equal(Sorted("8H", "7H"), withoutHigher);
    }

    [Fact]
    public void R13_Overtrump_duty_applies_even_when_the_partner_is_the_one_winning_with_trump()
    {
        // Trump led: partner North is winning with 9H, South still must beat it if possible.
        var legal = Legal(ContractKind.Hearts, Seat.South, "JH 7H", (Seat.West, "8H"), (Seat.North, "9H"), (Seat.East, "7H"));
        Assert.Equal(Sorted("JH"), legal);
    }

    [Fact]
    public void R14_All_trump_requires_following_suit_and_going_higher_when_able()
    {
        var higher = Legal(ContractKind.AllTrump, Seat.South, "JS 8S 7H", (Seat.West, "9S"));
        Assert.Equal(Sorted("JS"), higher);

        var cannotBeat = Legal(ContractKind.AllTrump, Seat.South, "8S 7S 7H", (Seat.West, "9S"));
        Assert.Equal(Sorted("8S", "7S"), cannotBeat);

        var noSuit = Legal(ContractKind.AllTrump, Seat.South, "7H 8D", (Seat.West, "9S"));
        Assert.Equal(Sorted("7H", "8D"), noSuit);
    }

    [Fact]
    public void R14_All_trump_overtake_duty_ignores_who_is_winning()
    {
        var legal = Legal(ContractKind.AllTrump, Seat.South, "JS 10S", (Seat.West, "7S"), (Seat.North, "9S"), (Seat.East, "8S"));
        Assert.Equal(Sorted("JS"), legal);
    }

    [Fact]
    public void R14_All_trump_uses_the_trump_order_in_every_suit()
    {
        // In every suit the Jack beats the Nine beats the Ace beats the Ten.
        var legal = Legal(ContractKind.AllTrump, Seat.South, "AD 10D 9D QD", (Seat.West, "KD"));
        Assert.Equal(Sorted("AD", "10D", "9D"), legal);
    }

    [Fact]
    public void R11_No_trump_only_requires_following_suit()
    {
        var follows = Legal(ContractKind.NoTrump, Seat.South, "KS 7S 9H", (Seat.West, "AS"));
        Assert.Equal(Sorted("KS", "7S"), follows);

        var free = Legal(ContractKind.NoTrump, Seat.South, "KH 7D", (Seat.West, "AS"));
        Assert.Equal(Sorted("KH", "7D"), free);
    }
}

public class TrickResolutionTests
{
    private static Seat Winner(ContractKind kind, params (Seat, string)[] plays) =>
        TrickResolver.Winner(T.Rules, kind, T.Plays(plays));

    [Fact]
    public void R7_A_trump_beats_the_led_suit_and_the_highest_trump_wins()
    {
        Assert.Equal(
            Seat.North,
            Winner(ContractKind.Hearts, (Seat.West, "AS"), (Seat.North, "7H"), (Seat.East, "KS"), (Seat.South, "QS")));
        Assert.Equal(
            Seat.East,
            Winner(ContractKind.Hearts, (Seat.West, "AS"), (Seat.North, "7H"), (Seat.East, "9H"), (Seat.South, "QS")));
        Assert.Equal(
            Seat.South,
            Winner(ContractKind.Hearts, (Seat.West, "AS"), (Seat.North, "AH"), (Seat.East, "9H"), (Seat.South, "JH")));
    }

    [Fact]
    public void Off_suit_non_trump_cards_never_win()
    {
        Assert.Equal(
            Seat.West,
            Winner(ContractKind.Hearts, (Seat.West, "8S"), (Seat.North, "AD"), (Seat.East, "AC"), (Seat.South, "7S")));
    }

    [Fact]
    public void R7_Plain_suit_order_is_ace_ten_king_queen_jack_nine()
    {
        Assert.Equal(
            Seat.North,
            Winner(ContractKind.Hearts, (Seat.West, "KS"), (Seat.North, "10S"), (Seat.East, "QS"), (Seat.South, "JS")));
    }

    [Fact]
    public void R9_No_trump_has_no_trump_and_orders_cards_ace_ten_king()
    {
        Assert.Equal(
            Seat.East,
            Winner(ContractKind.NoTrump, (Seat.West, "KS"), (Seat.North, "QS"), (Seat.East, "10S"), (Seat.South, "7H")));
    }

    [Fact]
    public void R10_All_trump_orders_the_led_suit_jack_nine_ace_ten()
    {
        Assert.Equal(
            Seat.North,
            Winner(ContractKind.AllTrump, (Seat.West, "AH"), (Seat.North, "9H"), (Seat.East, "10H"), (Seat.South, "JD")));
        Assert.Equal(
            Seat.South,
            Winner(ContractKind.AllTrump, (Seat.West, "AH"), (Seat.North, "9H"), (Seat.East, "10H"), (Seat.South, "JH")));
    }

    [Fact]
    public void The_trick_winner_leads_next_and_the_hand_ends_after_eight_tricks()
    {
        var state = T.Playing(
            ContractKind.Hearts,
            Seat.North,
            north: "AS 7D 7C 8C 9C 10C JC QC",
            east: "KS 8D 9D 10D JD QD KD AD",
            south: "7S 8S 9S 10S JS QS KH AH",
            west: "7H 8H 9H 10H JH QH KC AC");

        var events = new List<GameEvent>();
        state = T.PlayCards(state, "AS KS 7S 7H", events);

        var won = events.OfType<TrickWon>().Single();
        Assert.Equal(Seat.West, won.Winner);
        Assert.Equal(Seat.West, state.ToAct);
        Assert.Equal(Seat.West, state.Hand.Leader);
        Assert.Single(state.Hand.Tricks);
        Assert.Empty(state.Hand.CurrentPlays);
    }
}

public class PlayRejectionTests
{
    private static GameState Sample() => T.Playing(
        ContractKind.Hearts,
        Seat.North,
        north: "AS 7D 7C 8C 9C 10C JC QC",
        east: "KS 8D 9D 10D JD QD KD AD",
        south: "7S 8S 9S 10S JS QS KH AH",
        west: "7H 8H 9H 10H JH QH KC AC");

    [Fact]
    public void Out_of_turn_play_is_rejected_and_state_is_unchanged()
    {
        var state = Sample();
        var rejected = T.Reject(state, Seat.East, T.Play("KS"));
        Assert.Equal(RejectionReason.NotYourTurn, rejected.Reason);
        Assert.Equal(Seat.North, state.ToAct);
        Assert.Empty(state.Hand.CurrentPlays);
    }

    [Fact]
    public void A_card_not_in_hand_is_rejected()
    {
        var state = Sample();
        Assert.Equal(RejectionReason.CardNotInHand, T.Reject(state, Seat.North, T.Play("KS")).Reason);
    }

    [Fact]
    public void An_illegal_card_is_rejected_with_the_obligation_that_was_broken_and_a_legal_one_still_works()
    {
        var state = T.PlayCards(Sample(), "AS");
        // East holds KS: must follow suit, so 8D is illegal.
        var rejected = T.Reject(state, Seat.East, T.Play("8D"));
        Assert.Equal(RejectionReason.MustFollowSuit, rejected.Reason);

        var after = T.Accept(state, Seat.East, T.Play("KS"));
        Assert.Equal(Seat.South, after.ToAct);
    }

    [Fact]
    public void Must_trump_violation_is_reported_as_must_trump()
    {
        var state = T.Playing(
            ContractKind.Hearts,
            Seat.North,
            north: "AS 7H 8H 10H JH QH KH AH",
            east: "KS 8D 9D 10D JD QD KD AD",
            south: "9H 7D 7C 8C 9C 10C JC QC",
            west: "7S 8S 9S 10S JS QS KC AC",
            leader: Seat.East);
        state = T.PlayCards(state, "KS");

        // South has no spade and an opponent (East) is winning, so only the trump is legal.
        var rejected = T.Reject(state, Seat.South, T.Play("7D"));
        Assert.Equal(RejectionReason.MustTrump, rejected.Reason);
        var after = T.Accept(state, Seat.South, T.Play("9H"));
        Assert.Equal(Seat.West, after.ToAct);
    }

    [Fact]
    public void Illegal_action_kinds_for_the_phase_are_rejected()
    {
        var state = Sample();
        Assert.Equal(RejectionReason.WrongPhase, T.Reject(state, Seat.North, new PassAction()).Reason);
        Assert.Equal(RejectionReason.WrongPhase, T.Reject(state, Seat.North, new BidAction(ContractKind.Hearts)).Reason);
    }

    [Fact]
    public void Nothing_is_accepted_after_the_game_is_finished()
    {
        var finished = RandomGames.Play(21);
        var seat = finished.ToAct;
        Assert.Equal(RejectionReason.GameFinished, T.Reject(finished, seat, new PassAction()).Reason);
    }

    [Fact]
    public void FR_011_a_legal_card_always_exists_for_the_seat_to_act()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            RandomGames.Play(seed, (before, seat, _, _) =>
            {
                Assert.NotEmpty(LegalActions.For(Projection.For(before, seat)));
            });
        }
    }
}
