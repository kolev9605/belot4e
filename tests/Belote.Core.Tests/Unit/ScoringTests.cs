using System.Collections.Immutable;
using Belote.Core;
using Xunit;

namespace Belote.Core.Tests;

public class GamePointConverterTests
{
    [Theory]
    // Suit contract totals 162 -> 16 game points.
    [InlineData(100, 62, Team.NorthSouth, 10, 6)]
    [InlineData(85, 77, Team.NorthSouth, 8, 8)]
    // Both ending in 6: the team with fewer points rounds up, the other rounds down.
    [InlineData(66, 96, Team.NorthSouth, 7, 9)]
    // All-trump 154 + 104 = 258 -> 26: scores ending in 4 round up for the team with fewer points.
    [InlineData(154, 104, Team.EastWest, 15, 11)]
    [InlineData(144, 114, Team.EastWest, 14, 12)]
    // No-trump keeps plain rounding because values are even.
    [InlineData(134, 126, Team.EastWest, 13, 13)]
    [InlineData(150, 110, Team.EastWest, 15, 11)]
    // One team takes everything.
    [InlineData(162, 0, Team.NorthSouth, 16, 0)]
    [InlineData(0, 258, Team.EastWest, 0, 26)]
    [InlineData(185, 0, Team.NorthSouth, 19, 0)]
    public void R24_Game_points_are_split_by_largest_remainder_and_always_sum_to_the_pool(
        int northSouth, int eastWest, Team favored, int expectedNs, int expectedEw)
    {
        var shares = GamePointConverter.Split(new[] { northSouth, eastWest }, 10, favored);
        Assert.Equal(new[] { expectedNs, expectedEw }, shares);
        Assert.Equal(GamePointConverter.Pool(northSouth + eastWest, 10), shares.Sum());
    }

    [Theory]
    // Tied hands: the declarer (favoured, held over) gets the extra point.
    [InlineData(106, 106, 11, 10)]
    [InlineData(154, 154, 16, 15)]
    [InlineData(81, 81, 8, 8)]
    public void R22_Tied_hands_give_the_extra_game_point_to_the_hanging_declarer(
        int northSouth, int eastWest, int expectedDeclarer, int expectedDefenders)
    {
        var shares = GamePointConverter.Split(new[] { northSouth, eastWest }, 10, Team.NorthSouth);
        Assert.Equal(new[] { expectedDeclarer, expectedDefenders }, shares);
    }

    [Fact]
    public void Pool_rounds_half_up()
    {
        Assert.Equal(16, GamePointConverter.Pool(162, 10));
        Assert.Equal(15, GamePointConverter.Pool(149, 10));
        Assert.Equal(26, GamePointConverter.Pool(258, 10));
        Assert.Equal(13, GamePointConverter.Pool(125, 10));
        Assert.Equal(12, GamePointConverter.Pool(124, 10));
    }
}

public class HandScoringTests
{
    private const string Filler = "7S 8S 9S 7D";

    private static HandResult Score(
        ContractKind kind,
        Seat declarer,
        int multiplier,
        ImmutableArray<Trick> tricks,
        int heldOver = 0,
        ImmutableArray<DeclaredAnnouncement>? declared = null,
        Team? sequenceTeam = null,
        Team? carreTeam = null,
        ImmutableArray<BeloteAward>? belotes = null,
        RuleSet? rules = null) =>
        HandScorer.Score(
            rules ?? T.Rules,
            new Contract(kind, declarer, multiplier),
            tricks,
            declared ?? ImmutableArray<DeclaredAnnouncement>.Empty,
            sequenceTeam,
            carreTeam,
            belotes ?? ImmutableArray<BeloteAward>.Empty,
            heldOver);

    // North-South (declarer North) win three rich tricks (111 card points); East-West win the rest
    // and the last trick (28 + 10).
    private static ImmutableArray<Trick> NorthSouthMade() => T.Tricks(
        (Seat.North, "AS 10S KS QS"),
        (Seat.North, "AD 10D KD QD"),
        (Seat.North, "JH 9H AH 10H"),
        (Seat.East, Filler),
        (Seat.East, Filler),
        (Seat.East, Filler),
        (Seat.East, Filler),
        (Seat.East, "AC 10C KC QC"));

    // Same cards, but East-West take the rich tricks and the declarer only wins the last trick.
    private static ImmutableArray<Trick> NorthSouthFailed() => T.Tricks(
        (Seat.East, "AS 10S KS QS"),
        (Seat.East, "AD 10D KD QD"),
        (Seat.East, "JH 9H AH 10H"),
        (Seat.East, Filler),
        (Seat.East, Filler),
        (Seat.East, Filler),
        (Seat.East, Filler),
        (Seat.North, "AC 10C KC QC"));

    // 81 - 81 once the last-trick bonus is added to East-West.
    private static ImmutableArray<Trick> Tied() => T.Tricks(
        (Seat.North, "AS 10S KS QS"),
        (Seat.North, "AD 10D KD QD"),
        (Seat.North, "AH 10H 7S 8S"),
        (Seat.North, "KH 7D 8D 9D"),
        (Seat.East, "JH 9H QH 7S"),
        (Seat.East, "AC 10C KC QC"),
        (Seat.East, "JC 7C 8C 9C"),
        (Seat.East, "JD JS 7H 8H"));

    private static ImmutableArray<Trick> FullDeckTricks()
    {
        var deck = Deck.Standard32;
        var tricks = ImmutableArray.CreateBuilder<Trick>();
        for (int i = 0; i < 8; i++)
        {
            var cards = string.Join(' ', deck.Skip(4 * i).Take(4));
            tricks.Add(T.Trick(Seats.All[i % 4], cards));
        }

        return tricks.ToImmutable();
    }

    [Theory]
    [InlineData(ContractKind.Hearts, 162)]
    [InlineData(ContractKind.NoTrump, 260)]
    [InlineData(ContractKind.AllTrump, 258)]
    public void R8_R9_R10_R20_All_points_in_play_are_distributed_including_the_last_trick_bonus(
        ContractKind kind, int expectedTotal)
    {
        var result = Score(kind, Seat.North, 1, FullDeckTricks());
        Assert.Equal(expectedTotal, result.RawPool);
        Assert.Equal(expectedTotal, result.CardPoints.Sum() + result.LastTrickBonus);
    }

    [Fact]
    public void R20_The_last_trick_bonus_goes_to_the_team_that_wins_the_eighth_trick()
    {
        var result = Score(ContractKind.Hearts, Seat.North, 1, NorthSouthMade());
        Assert.Equal(Team.EastWest, result.LastTrickTeam);
        Assert.Equal(10, result.LastTrickBonus);
        Assert.Equal(new[] { 111, 38 }, result.RawTotals.ToArray());
        Assert.Equal(new[] { 111, 28 }, result.CardPoints.ToArray());
    }

    [Fact]
    public void A_made_contract_scores_each_team_its_own_points()
    {
        var result = Score(ContractKind.Hearts, Seat.North, 1, NorthSouthMade());
        Assert.Equal(HandOutcome.Made, result.Outcome);
        Assert.Equal(15, result.PoolGamePoints);
        Assert.Equal(new[] { 11, 4 }, result.GamePoints.ToArray());
        Assert.Equal(0, result.NewlyHeld);
    }

    [Fact]
    public void R21_A_failed_contract_gives_the_defenders_all_points_and_the_declarer_nothing()
    {
        var result = Score(ContractKind.Hearts, Seat.North, 1, NorthSouthFailed());
        Assert.Equal(HandOutcome.Failed, result.Outcome);
        Assert.Equal(new[] { 38, 111 }, result.RawTotals.ToArray());
        Assert.Equal(new[] { 0, 15 }, result.GamePoints.ToArray());
    }

    [Theory]
    [InlineData(2, 30)]
    [InlineData(4, 60)]
    public void R21_Contra_and_recontra_multiply_the_failed_contract_transfer(int multiplier, int expected)
    {
        var result = Score(ContractKind.Hearts, Seat.North, multiplier, NorthSouthFailed());
        Assert.Equal(new[] { 0, expected }, result.GamePoints.ToArray());
    }

    [Fact]
    public void R5_A_made_contract_under_contra_takes_everything_multiplied()
    {
        var result = Score(ContractKind.Hearts, Seat.North, 2, NorthSouthMade());
        Assert.Equal(HandOutcome.Made, result.Outcome);
        Assert.Equal(new[] { 30, 0 }, result.GamePoints.ToArray());
    }

    [Fact]
    public void The_failure_threshold_is_strictly_fewer_points_than_the_defenders()
    {
        var tied = Score(ContractKind.Hearts, Seat.North, 1, Tied());
        Assert.Equal(new[] { 81, 81 }, tied.RawTotals.ToArray());
        Assert.Equal(HandOutcome.Tied, tied.Outcome);
    }

    [Fact]
    public void R22_A_tied_hand_holds_the_declarers_points_over_and_defenders_keep_theirs()
    {
        var result = Score(ContractKind.Hearts, Seat.North, 1, Tied());
        Assert.Equal(16, result.PoolGamePoints);
        Assert.Equal(new[] { 0, 8 }, result.GamePoints.ToArray());
        Assert.Equal(8, result.NewlyHeld);
        Assert.Equal(8, result.HeldOverAfter);
        Assert.Equal(0, result.AwardedFromHeldOver);
    }

    [Fact]
    public void R22_Held_over_points_stack_on_consecutive_tied_hands()
    {
        var result = Score(ContractKind.Hearts, Seat.North, 1, Tied(), heldOver: 8);
        Assert.Equal(16, result.HeldOverAfter);
        Assert.Equal(new[] { 0, 8 }, result.GamePoints.ToArray());
    }

    [Fact]
    public void R22_Held_over_points_go_to_the_team_that_wins_the_next_hand()
    {
        var made = Score(ContractKind.Hearts, Seat.North, 1, NorthSouthMade(), heldOver: 8);
        Assert.Equal(new[] { 19, 4 }, made.GamePoints.ToArray());
        Assert.Equal(8, made.AwardedFromHeldOver);
        Assert.Equal(Team.NorthSouth, made.HeldOverAwardedTo);
        Assert.Equal(0, made.HeldOverAfter);

        var failed = Score(ContractKind.Hearts, Seat.North, 1, NorthSouthFailed(), heldOver: 8);
        Assert.Equal(new[] { 0, 23 }, failed.GamePoints.ToArray());
        Assert.Equal(Team.EastWest, failed.HeldOverAwardedTo);
    }

    [Fact]
    public void R22_A_tie_under_contra_multiplies_both_the_kept_and_the_held_points()
    {
        var result = Score(ContractKind.Hearts, Seat.North, 2, Tied());
        Assert.Equal(new[] { 0, 16 }, result.GamePoints.ToArray());
        Assert.Equal(16, result.NewlyHeld);
    }

    private static ImmutableArray<Trick> NorthSouthSweep() => T.Tricks(
        (Seat.North, "AS 10S KS QS"),
        (Seat.North, "AD 10D KD QD"),
        (Seat.North, "AH 10H 7S 8S"),
        (Seat.North, "KH 7D 8D 9D"),
        (Seat.South, "JH 9H QH 7S"),
        (Seat.South, "AC 10C KC QC"),
        (Seat.South, "JC 7C 8C 9C"),
        (Seat.South, "JD JS 7H 8H"));

    [Fact]
    public void R23_Winning_all_eight_tricks_adds_the_fixed_all_tricks_bonus()
    {
        var result = Score(ContractKind.Hearts, Seat.North, 1, NorthSouthSweep());
        Assert.Equal(Team.NorthSouth, result.AllTricksTeam);
        Assert.Equal(90, result.AllTricksBonus);
        Assert.Equal(new[] { 152 + 10 + 90, 0 }, result.RawTotals.ToArray());
        Assert.Equal(new[] { 25, 0 }, result.GamePoints.ToArray());
    }

    [Fact]
    public void R23_A_defender_sweep_also_earns_the_bonus_and_fails_the_contract()
    {
        var tricks = T.Tricks(
            (Seat.East, "AS 10S KS QS"),
            (Seat.East, "AD 10D KD QD"),
            (Seat.East, "AH 10H 7S 8S"),
            (Seat.East, "KH 7D 8D 9D"),
            (Seat.West, "JH 9H QH 7S"),
            (Seat.West, "AC 10C KC QC"),
            (Seat.West, "JC 7C 8C 9C"),
            (Seat.West, "JD JS 7H 8H"));
        var result = Score(ContractKind.Hearts, Seat.North, 1, tricks);
        Assert.Equal(Team.EastWest, result.AllTricksTeam);
        Assert.Equal(HandOutcome.Failed, result.Outcome);
        Assert.Equal(new[] { 0, 25 }, result.GamePoints.ToArray());
    }

    [Fact]
    public void R23_The_bonus_value_comes_from_the_rule_set()
    {
        var rules = T.Rules with { Scoring = T.Rules.Scoring with { AllTricksBonus = 0 } };
        var result = Score(ContractKind.Hearts, Seat.North, 1, NorthSouthSweep(), rules: rules);
        Assert.Equal(new[] { 152 + 10, 0 }, result.RawTotals.ToArray());
    }

    [Fact]
    public void Counted_announcements_and_belote_are_added_to_the_hand_total()
    {
        var declared = ImmutableArray.Create(
            new DeclaredAnnouncement(Seat.North, AnnouncementSpec.Sequence(Suit.Hearts, Rank.Queen, 3), 20));
        var belotes = ImmutableArray.Create(new BeloteAward(Seat.East, Suit.Hearts));

        var counted = Score(
            ContractKind.Hearts, Seat.North, 1, NorthSouthMade(),
            declared: declared, sequenceTeam: Team.NorthSouth, belotes: belotes);
        Assert.Equal(new[] { 131, 58 }, counted.RawTotals.ToArray());
        Assert.Equal(new[] { 20, 0 }, counted.AnnouncementPoints.ToArray());
        Assert.Equal(new[] { 0, 20 }, counted.BelotePoints.ToArray());

        var notCounted = Score(
            ContractKind.Hearts, Seat.North, 1, NorthSouthMade(),
            declared: declared, sequenceTeam: Team.EastWest);
        Assert.Equal(new[] { 111, 38 }, notCounted.RawTotals.ToArray());
    }

    [Fact]
    public void Counted_announcements_of_the_defenders_raise_their_total()
    {
        var declared = ImmutableArray.Create(
            new DeclaredAnnouncement(Seat.East, AnnouncementSpec.Sequence(Suit.Spades, Rank.King, 3), 20));
        var result = Score(
            ContractKind.Hearts, Seat.North, 1, NorthSouthMade(),
            declared: declared, sequenceTeam: Team.EastWest);
        Assert.Equal(new[] { 111, 58 }, result.RawTotals.ToArray());
        Assert.Equal(HandOutcome.Made, result.Outcome);
    }
}

public class MatchScoringTests
{
    private static HandResult Result(int northSouth, int eastWest, Team? allTricks = null) =>
        HandScorer.Score(
            T.Rules,
            new Contract(ContractKind.Hearts, Seat.North, 1),
            T.Tricks(
                (Seat.North, "AS 10S KS QS"),
                (Seat.East, "7S 8S 9S 7D"),
                (Seat.East, "7S 8S 9S 7D"),
                (Seat.East, "7S 8S 9S 7D"),
                (Seat.East, "7S 8S 9S 7D"),
                (Seat.East, "7S 8S 9S 7D"),
                (Seat.East, "7S 8S 9S 7D"),
                (Seat.East, "7S 8S 9S 7D")),
            ImmutableArray<DeclaredAnnouncement>.Empty,
            null,
            null,
            ImmutableArray<BeloteAward>.Empty,
            0) with
        {
            GamePoints = ImmutableArray.Create(northSouth, eastWest),
            AllTricksTeam = allTricks,
        };

    [Fact]
    public void Scores_accumulate_per_team_across_hands()
    {
        var outcome = MatchScorer.Apply(T.Rules, ImmutableArray.Create(10, 20), Result(11, 4));
        Assert.Equal(new[] { 21, 24 }, outcome.Score.ToArray());
        Assert.Null(outcome.Winner);
    }

    [Fact]
    public void R25_The_match_ends_when_a_team_reaches_the_target()
    {
        var outcome = MatchScorer.Apply(T.Rules, ImmutableArray.Create(140, 100), Result(11, 4));
        Assert.Equal(new[] { 151, 104 }, outcome.Score.ToArray());
        Assert.Equal(Team.NorthSouth, outcome.Winner);
    }

    [Fact]
    public void R25_When_both_teams_reach_the_target_in_one_hand_the_higher_total_wins()
    {
        var outcome = MatchScorer.Apply(T.Rules, ImmutableArray.Create(145, 150), Result(10, 10));
        Assert.Equal(new[] { 155, 160 }, outcome.Score.ToArray());
        Assert.Equal(Team.EastWest, outcome.Winner);
    }

    [Fact]
    public void R25_Exactly_equal_totals_at_or_above_the_target_play_on()
    {
        var outcome = MatchScorer.Apply(T.Rules, ImmutableArray.Create(145, 145), Result(10, 10));
        Assert.Equal(new[] { 155, 155 }, outcome.Score.ToArray());
        Assert.Null(outcome.Winner);
    }

    [Fact]
    public void The_match_cannot_be_won_by_an_all_tricks_hand()
    {
        var sweep = MatchScorer.Apply(T.Rules, ImmutableArray.Create(145, 100), Result(25, 0, Team.NorthSouth));
        Assert.Null(sweep.Winner);
        Assert.Equal(170, sweep.Score[0]);

        var next = MatchScorer.Apply(T.Rules, sweep.Score, Result(11, 4));
        Assert.Equal(Team.NorthSouth, next.Winner);
    }

    [Fact]
    public void The_all_tricks_restriction_is_configurable()
    {
        var rules = T.Rules with { Match = T.Rules.Match with { AllTricksCannotEndMatch = false } };
        var sweep = MatchScorer.Apply(rules, ImmutableArray.Create(145, 100), Result(25, 0, Team.NorthSouth));
        Assert.Equal(Team.NorthSouth, sweep.Winner);
    }

    [Fact]
    public void The_target_score_comes_from_the_rule_set_and_ends_a_real_game()
    {
        var rules = T.Rules with { Match = T.Rules.Match with { TargetScore = 1, AllTricksCannotEndMatch = false } };
        var final = RandomGames.Play(3, rules: rules);

        Assert.Equal(Phase.Finished, final.Phase);
        Assert.NotNull(final.Winner);
        Assert.True(final.MatchScore.Max() >= 1);
        Assert.Equal(1, final.HandsScored);
    }
}
