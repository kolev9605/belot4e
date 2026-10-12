using System.Collections.Immutable;
using Belote.Core;
using Xunit;

namespace Belote.Core.Tests;

public class AnnouncementDetectionTests
{
    private static ImmutableArray<AnnouncementSpec> Detect(string hand) =>
        AnnouncementDetector.Detect(T.Rules.Announcements, T.Hand(hand));

    [Theory]
    [InlineData("7H 8H 9H KD AS 7S 8C JC", 3, Rank.Nine, 20)]
    [InlineData("JS QS KS AS 7D 8D 7C 9C", 4, Rank.Ace, 50)]
    [InlineData("7C 8C 9C 10C JC 7D 9D KS", 5, Rank.Jack, 100)]
    [InlineData("7C 8C 9C 10C JC QC 7D KS", 6, Rank.Queen, 100)]
    public void R15_Sequences_are_detected_by_length_and_worth_tierce_quarte_quinte(
        string hand, int length, Rank top, int points)
    {
        var found = Assert.Single(Detect(hand));
        Assert.Equal(AnnouncementKind.Sequence, found.Kind);
        Assert.Equal(length, found.Length);
        Assert.Equal(top, found.Rank);
        Assert.Equal(points, found.Points(T.Rules.Announcements));
    }

    [Theory]
    [InlineData(Rank.Jack, 200)]
    [InlineData(Rank.Nine, 150)]
    [InlineData(Rank.Ace, 100)]
    [InlineData(Rank.King, 100)]
    [InlineData(Rank.Queen, 100)]
    [InlineData(Rank.Ten, 100)]
    public void R15_Four_of_a_kind_values(Rank rank, int points)
    {
        string r = Card.RankText(rank);
        var found = Assert.Single(Detect($"{r}C {r}D {r}H {r}S 7C 7D 8H 8S"));
        Assert.Equal(AnnouncementKind.Carre, found.Kind);
        Assert.Equal(rank, found.Rank);
        Assert.Equal(points, found.Points(T.Rules.Announcements));
    }

    [Theory]
    [InlineData(Rank.Seven)]
    [InlineData(Rank.Eight)]
    public void R15_Four_sevens_or_eights_are_not_an_announcement(Rank rank)
    {
        string r = Card.RankText(rank);
        Assert.Empty(Detect($"{r}C {r}D {r}H {r}S 10C KD QH AS"));
    }

    [Fact]
    public void Two_cards_or_a_gapped_run_are_not_a_sequence()
    {
        Assert.Empty(Detect("7H 8H 10H JH KS QD 9C AD"));
    }

    [Fact]
    public void Overlapping_combinations_are_all_detected_so_the_player_can_choose()
    {
        var found = Detect("JC JD JH JS QS KS 7D 8D");
        Assert.Contains(AnnouncementSpec.Carre(Rank.Jack), found);
        Assert.Contains(AnnouncementSpec.Sequence(Suit.Spades, Rank.King, 3), found);
    }

    [Fact]
    public void R17_No_announcements_may_be_declared_in_a_no_trump_hand()
    {
        var state = T.Playing(
            ContractKind.NoTrump,
            Seat.North,
            north: "7H 8H 9H JD QD KD AS 7S",
            east: "KS QS JS 8D 9D AD 7C 8C",
            south: "10C 10D 10H 10S AH KH QC 9C",
            west: "JH QH 7D 8S 9S JC KC AC");
        var legal = LegalActions.For(Projection.For(state, Seat.North));
        Assert.DoesNotContain(legal, a => a is AnnounceAction);
        Assert.Equal(
            RejectionReason.AnnouncementNotAllowed,
            T.Reject(state, Seat.North, new AnnounceAction(AnnouncementSpec.Sequence(Suit.Hearts, Rank.Nine, 3))).Reason);
    }
}

public class AnnouncementComparisonTests
{
    private static AnnouncementOutcome Compare(params (Seat, AnnouncementSpec)[] declared) =>
        AnnouncementComparer.Compare(T.Rules.Announcements, declared);

    private static AnnouncementSpec Seq(Suit suit, Rank top, int length) => AnnouncementSpec.Sequence(suit, top, length);

    [Fact]
    public void R19_The_longer_sequence_wins_and_only_that_team_counts()
    {
        var outcome = Compare((Seat.North, Seq(Suit.Hearts, Rank.Ace, 3)), (Seat.East, Seq(Suit.Spades, Rank.Nine, 4)));
        Assert.Equal(Team.EastWest, outcome.SequenceTeam);
    }

    [Fact]
    public void R19_Equal_length_is_decided_by_the_higher_top_card()
    {
        var outcome = Compare((Seat.North, Seq(Suit.Hearts, Rank.Queen, 3)), (Seat.East, Seq(Suit.Spades, Rank.King, 3)));
        Assert.Equal(Team.EastWest, outcome.SequenceTeam);
    }

    [Fact]
    public void R19_Identical_sequences_cancel_out_and_neither_team_counts()
    {
        var outcome = Compare((Seat.North, Seq(Suit.Hearts, Rank.King, 3)), (Seat.West, Seq(Suit.Spades, Rank.King, 3)));
        Assert.Null(outcome.SequenceTeam);
    }

    [Fact]
    public void R19_A_team_with_a_four_of_a_kind_does_not_block_the_other_teams_sequence()
    {
        var outcome = Compare(
            (Seat.North, AnnouncementSpec.Carre(Rank.Ace)),
            (Seat.East, Seq(Suit.Hearts, Rank.Jack, 5)));
        Assert.Equal(Team.EastWest, outcome.SequenceTeam);
        Assert.Equal(Team.NorthSouth, outcome.CarreTeam);
    }

    [Fact]
    public void R19_Four_of_a_kinds_compare_in_trump_order_jack_nine_ace_ten()
    {
        Assert.Equal(
            Team.EastWest,
            Compare((Seat.North, AnnouncementSpec.Carre(Rank.Nine)), (Seat.East, AnnouncementSpec.Carre(Rank.Jack))).CarreTeam);
        Assert.Equal(
            Team.NorthSouth,
            Compare((Seat.South, AnnouncementSpec.Carre(Rank.Ace)), (Seat.West, AnnouncementSpec.Carre(Rank.Ten))).CarreTeam);
    }

    [Fact]
    public void A_team_winning_a_category_scores_all_its_announcements_in_it()
    {
        var outcome = Compare(
            (Seat.North, Seq(Suit.Hearts, Rank.Nine, 3)),
            (Seat.South, Seq(Suit.Diamonds, Rank.King, 4)),
            (Seat.East, Seq(Suit.Spades, Rank.King, 3)));
        Assert.Equal(Team.NorthSouth, outcome.SequenceTeam);
    }

    [Fact]
    public void No_announcements_means_no_counting_team()
    {
        var outcome = Compare();
        Assert.Null(outcome.SequenceTeam);
        Assert.Null(outcome.CarreTeam);
    }
}

public class AnnouncementFlowTests
{
    private static GameState Sample(ContractKind kind = ContractKind.Hearts) => T.Playing(
        kind,
        Seat.North,
        north: "7H 8H 9H JD QD KD AS 7S",
        east: "KS QS JS 8D 9D AD 7C 8C",
        south: "10C 10D 10H 10S AH KH QC 9C",
        west: "JH QH 7D 8S 9S JC KC AC");

    private static GameState FinishHand(GameState state, List<GameEvent> events)
    {
        int scored = state.HandsScored;
        while (state.HandsScored == scored)
        {
            var legal = LegalActions.For(Projection.For(state, state.ToAct));
            var action = legal.OfType<PlayCardAction>().First();
            state = T.Accept(state, state.ToAct, action, events);
        }

        return state;
    }

    [Fact]
    public void R18_Announcements_are_offered_with_the_seats_first_card_and_cannot_overlap_cards()
    {
        var state = Sample();
        var north = LegalActions.For(Projection.For(state, Seat.North)).OfType<AnnounceAction>().Select(a => a.Spec).ToList();
        Assert.Contains(AnnouncementSpec.Sequence(Suit.Hearts, Rank.Nine, 3), north);
        Assert.Contains(AnnouncementSpec.Sequence(Suit.Diamonds, Rank.King, 3), north);

        state = T.Accept(state, Seat.North, new AnnounceAction(AnnouncementSpec.Sequence(Suit.Hearts, Rank.Nine, 3)));
        Assert.Equal(Seat.North, state.ToAct);
        Assert.Single(state.Hand.Declared);
        Assert.Equal(20, state.Hand.Declared[0].Points);

        // The same combination cannot be declared twice.
        var rejected = T.Reject(state, Seat.North, new AnnounceAction(AnnouncementSpec.Sequence(Suit.Hearts, Rank.Nine, 3)));
        Assert.Equal(RejectionReason.AnnouncementNotInHand, rejected.Reason);
    }

    [Fact]
    public void R18_Declaring_cards_you_do_not_hold_is_rejected()
    {
        var state = Sample();
        var rejected = T.Reject(state, Seat.North, new AnnounceAction(AnnouncementSpec.Carre(Rank.Jack)));
        Assert.Equal(RejectionReason.AnnouncementNotInHand, rejected.Reason);
    }

    [Fact]
    public void R18_R19_Announcements_are_revealed_and_compared_once_after_the_first_trick()
    {
        var state = Sample();
        var events = new List<GameEvent>();

        state = T.Accept(state, Seat.North, new AnnounceAction(AnnouncementSpec.Sequence(Suit.Hearts, Rank.Nine, 3)), events);
        state = T.Accept(state, Seat.North, new AnnounceAction(AnnouncementSpec.Sequence(Suit.Diamonds, Rank.King, 3)), events);
        state = T.Accept(state, Seat.North, T.Play("AS"), events);

        // West has not seen the combinations yet: it only sees that a tierce was declared.
        var westView = Projection.For(state, Seat.West);
        Assert.Equal(2, westView.Announcements.Length);
        Assert.All(westView.Announcements, a => Assert.Null(a.Revealed));

        state = T.Accept(state, Seat.East, new AnnounceAction(AnnouncementSpec.Sequence(Suit.Spades, Rank.King, 3)), events);
        state = T.Accept(state, Seat.East, T.Play("KS"), events);
        state = T.Accept(state, Seat.South, new AnnounceAction(AnnouncementSpec.Carre(Rank.Ten)), events);
        state = T.Accept(state, Seat.South, T.Play("10S"), events);
        Assert.Empty(events.OfType<AnnouncementsRevealed>());
        state = T.Accept(state, Seat.West, T.Play("8S"), events);

        Assert.Single(events.OfType<AnnouncementsRevealed>());
        var compared = Assert.Single(events.OfType<AnnouncementsCompared>());
        Assert.True(events.IndexOf(compared) > events.IndexOf(events.OfType<TrickWon>().First()));

        // North's best tierce (top King) equals East's tierce: sequences cancel; South's four tens count.
        Assert.Null(compared.SequenceTeam);
        Assert.Equal(Team.NorthSouth, compared.CarreTeam);
        Assert.All(Projection.For(state, Seat.West).Announcements, a => Assert.NotNull(a.Revealed));

        // The window is now closed for everyone.
        var late = T.Reject(state, state.ToAct, new AnnounceAction(AnnouncementSpec.Carre(Rank.Ten)));
        Assert.Equal(RejectionReason.AnnouncementWindowClosed, late.Reason);
    }

    [Fact]
    public void R19_Counted_announcements_flow_into_the_hand_score()
    {
        var state = Sample();
        var events = new List<GameEvent>();
        state = T.Accept(state, Seat.North, new AnnounceAction(AnnouncementSpec.Sequence(Suit.Hearts, Rank.Nine, 3)), events);
        state = T.Accept(state, Seat.North, T.Play("AS"), events);
        state = T.Accept(state, Seat.East, new AnnounceAction(AnnouncementSpec.Sequence(Suit.Spades, Rank.King, 3)), events);
        state = T.Accept(state, Seat.East, T.Play("KS"), events);
        state = T.Accept(state, Seat.South, new AnnounceAction(AnnouncementSpec.Carre(Rank.Ten)), events);
        state = T.Accept(state, Seat.South, T.Play("10S"), events);
        state = T.Accept(state, Seat.West, T.Play("8S"), events);
        FinishHand(state, events);

        var result = events.OfType<HandScored>().Single().Result;
        // East-West's tierce (King) beats North's (Nine): 20 for East-West, 100 for the carre of tens.
        Assert.Equal(new[] { 100, 20 }, result.AnnouncementPoints.ToArray());
    }

    [Fact]
    public void R16_Belote_is_awarded_when_the_second_of_king_and_queen_of_trump_is_played()
    {
        var state = T.Playing(
            ContractKind.Hearts,
            Seat.North,
            north: "KH QH 7S 8S 9S 10S JS QS",
            east: "7H 8H 9H 10H JH AH 7D 8D",
            south: "KS AS 9D 10D JD QD KD AD",
            west: "7C 8C 9C 10C JC QC KC AC");
        var events = new List<GameEvent>();

        state = T.Accept(state, Seat.North, T.Play("KH"), events);
        Assert.Empty(events.OfType<BeloteAwarded>());

        FinishHand(state, events);
        var award = Assert.Single(events.OfType<BeloteAwarded>());
        Assert.Equal(Seat.North, award.Seat);
        Assert.Equal(Suit.Hearts, award.Suit);
        Assert.Equal(20, award.Points);
        Assert.Equal(new[] { 20, 0 }, events.OfType<HandScored>().Single().Result.BelotePoints.ToArray());
    }

    [Fact]
    public void R16_King_and_queen_of_a_non_trump_suit_are_not_belote_in_a_suit_contract()
    {
        var state = T.Playing(
            ContractKind.Hearts,
            Seat.North,
            north: "KS QS 7H 8H 9H 10H JH QH",
            east: "AH KH 7S 8S 9S 10S JS AS",
            south: "7D 8D 9D 10D JD QD KD AD",
            west: "7C 8C 9C 10C JC QC KC AC");
        var events = new List<GameEvent>();
        FinishHand(state, events);
        Assert.Empty(events.OfType<BeloteAwarded>());
    }

    [Fact]
    public void R16_R17_In_all_trump_king_and_queen_of_any_suit_count_but_not_in_no_trump()
    {
        var hands = (north: "KS QS 7H 8H 9H 10H JH QH",
            east: "AH KH 7S 8S 9S 10S JS AS",
            south: "7D 8D 9D 10D JD QD KD AD",
            west: "7C 8C 9C 10C JC QC KC AC");

        var allTrump = T.Playing(ContractKind.AllTrump, Seat.North, hands.north, hands.east, hands.south, hands.west);
        var allEvents = new List<GameEvent>();
        FinishHand(allTrump, allEvents);
        // North holds KS+QS; South holds KD+QD; West holds KC+QC; East's KH and North's QH are split.
        Assert.Equal(3, allEvents.OfType<BeloteAwarded>().Count());

        var noTrump = T.Playing(ContractKind.NoTrump, Seat.North, hands.north, hands.east, hands.south, hands.west);
        var noEvents = new List<GameEvent>();
        FinishHand(noTrump, noEvents);
        Assert.Empty(noEvents.OfType<BeloteAwarded>());
    }

    [Fact]
    public void R16_King_and_queen_held_by_different_seats_are_not_belote()
    {
        var state = T.Playing(
            ContractKind.Hearts,
            Seat.North,
            north: "KH 7S 8S 9S 10S JS QS AS",
            east: "QH 7H 8H 9H 10H JH AH 7D",
            south: "8D 9D 10D JD QD KD AD 7C",
            west: "8C 9C 10C JC QC KC AC 7D".Replace("7D", "KS"));
        var events = new List<GameEvent>();
        FinishHand(state, events);
        Assert.Empty(events.OfType<BeloteAwarded>());
    }
}
