using System.Collections.Immutable;
using System.Reflection;
using Belote.Core;
using FsCheck.Xunit;
using Xunit;

namespace Belote.Core.Tests;

/// <summary>Invariants that must hold in every reachable state of every seeded game.</summary>
public class GameProperties
{
    [Property(MaxTest = 40)]
    public void FR_011_Every_state_has_a_legal_action_and_every_legal_action_is_accepted(ulong seed)
    {
        RandomGames.Play(seed, (before, seat, _, _) =>
        {
            var view = Projection.For(before, seat);
            var legal = LegalActions.For(view);
            Assert.NotEmpty(legal);
            foreach (var action in legal)
            {
                Assert.IsType<Accepted>(GameEngine.Apply(before, seat, action));
            }
        });
    }

    [Property(MaxTest = 40)]
    public void Cards_that_are_not_legal_are_rejected_and_leave_the_state_unchanged(ulong seed)
    {
        RandomGames.Play(seed, (before, seat, _, _) =>
        {
            var view = Projection.For(before, seat);
            var legalCards = LegalActions.For(view).OfType<PlayCardAction>().Select(a => a.Card).ToHashSet();
            if (before.Phase != Phase.Playing)
            {
                return;
            }

            string Snapshot(GameState s) => string.Join(
                "|",
                s.Hand.Hands.Select(h => string.Join(",", h))
                    .Concat(new[]
                    {
                        string.Join(",", s.Hand.CurrentPlays.Select(p => $"{p.Seat}{p.Card}")),
                        s.Hand.Tricks.Length.ToString(),
                        s.ToAct.ToString(),
                        string.Join(",", s.MatchScore),
                        s.NextSequence.ToString(),
                    }));

            string before_ = Snapshot(before);
            foreach (var card in view.OwnHand.Where(c => !legalCards.Contains(c)))
            {
                var result = GameEngine.Apply(before, seat, new PlayCardAction(card));
                var rejected = Assert.IsType<Rejected>(result);
                Assert.True(rejected.Reason is RejectionReason.MustFollowSuit
                    or RejectionReason.MustTrump
                    or RejectionReason.MustOvertrump);
                Assert.Equal(before_, Snapshot(before));
            }
        });
    }

    [Property(MaxTest = 40)]
    public void Cards_are_conserved_through_every_hand(ulong seed)
    {
        RandomGames.Play(seed, (_, _, _, accepted) =>
        {
            var state = accepted.State;
            if (state.Phase == Phase.Finished)
            {
                return;
            }

            var hand = state.Hand;
            var all = hand.Hands.SelectMany(h => h)
                .Concat(hand.Tricks.SelectMany(t => t.Plays.Select(p => p.Card)))
                .Concat(hand.CurrentPlays.Select(p => p.Card))
                .ToList();
            Assert.Equal(32, all.Count);
            Assert.Equal(32, all.Distinct().Count());
        });
    }

    [Property(MaxTest = 40)]
    public void Seat_views_only_contain_the_seats_own_cards(ulong seed)
    {
        RandomGames.Play(seed, (_, _, _, accepted) =>
        {
            var state = accepted.State;
            if (state.Phase == Phase.Finished)
            {
                return;
            }

            var hand = state.Hand;
            var seen = new List<Card>();
            foreach (var seat in Seats.All)
            {
                var view = Projection.For(state, seat);
                Assert.All(view.OwnHand, c => Assert.Contains(c, hand.Hands[(int)seat]));
                if (state.Phase == Phase.Bidding)
                {
                    Assert.Equal(hand.DealtCount, view.OwnHand.Length);
                    Assert.Equal(hand.Hands[(int)seat].Take(hand.DealtCount), view.OwnHand);
                }
                else
                {
                    Assert.Equal(hand.Hands[(int)seat].Length, view.OwnHand.Length);
                }

                seen.AddRange(view.OwnHand);
            }

            Assert.Equal(seen.Count, seen.Distinct().Count());
        });
    }

    [Fact]
    public void SeatView_has_no_member_that_could_carry_other_seats_hidden_cards()
    {
        // Structural guard: the only card collection that is private is OwnHand; everything else is
        // public information (tricks, current trick, announcements). No GameState/HandState/seed leaks.
        var forbidden = new[] { typeof(GameState), typeof(HandState), typeof(ImmutableArray<ImmutableArray<Card>>) };
        foreach (var property in typeof(SeatView).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.DoesNotContain(property.PropertyType, forbidden);
            Assert.DoesNotContain("Seed", property.Name);
            Assert.DoesNotContain("Hands", property.Name);
            Assert.DoesNotContain("Deck", property.Name);
        }
    }

    [Property(MaxTest = 40)]
    public void FR_018_Hand_points_are_conserved_and_held_over_points_are_accounted(ulong seed)
    {
        var rules = T.Rules;
        int heldOver = 0;
        RandomGames.Play(seed, (_, _, _, accepted) =>
        {
            foreach (var scored in accepted.Events.OfType<HandScored>())
            {
                var r = scored.Result;
                int expectedRaw = rules.PointsInPlay(r.Kind)
                    + r.AnnouncementPoints.Sum()
                    + r.BelotePoints.Sum()
                    + r.AllTricksBonus;
                Assert.Equal(expectedRaw, r.RawPool);
                Assert.Equal(GamePointConverter.Pool(r.RawPool * r.Multiplier, rules.Scoring.GamePointDivisor), r.PoolGamePoints);
                Assert.Equal(r.PoolGamePoints - r.NewlyHeld + r.AwardedFromHeldOver, r.GamePoints.Sum());
                Assert.Equal(heldOver, r.HeldOverBefore);
                Assert.Equal(r.HeldOverBefore + r.NewlyHeld - r.AwardedFromHeldOver, r.HeldOverAfter);
                heldOver = r.HeldOverAfter;
            }
        });
    }

    [Property(MaxTest = 40)]
    public void The_game_always_ends_with_exactly_one_winner_at_or_above_the_target(ulong seed)
    {
        var final = RandomGames.Play(seed);
        Assert.Equal(Phase.Finished, final.Phase);
        var winner = final.Winner!.Value;
        int winnerScore = final.MatchScore[(int)winner];
        Assert.True(winnerScore >= T.Rules.Match.TargetScore);
        Assert.True(winnerScore >= final.MatchScore[(int)winner.Opponent()]);
    }

    [Property(MaxTest = 40)]
    public void Match_scores_never_decrease(ulong seed)
    {
        var last = new[] { 0, 0 };
        RandomGames.Play(seed, (_, _, _, accepted) =>
        {
            var score = accepted.State.MatchScore;
            Assert.True(score[0] >= last[0] && score[1] >= last[1]);
            last = new[] { score[0], score[1] };
        });
    }

    [Property(MaxTest = 20)]
    public void Same_seed_and_same_decisions_give_the_same_game(ulong seed)
    {
        var first = new List<string>();
        var second = new List<string>();
        var a = RandomGames.Play(seed, (_, seat, action, acc) => first.Add($"{seat}:{action}:{acc.Events.Count}"));
        var b = RandomGames.Play(seed, (_, seat, action, acc) => second.Add($"{seat}:{action}:{acc.Events.Count}"));

        Assert.Equal(first, second);
        Assert.Equal(a.MatchScore.ToArray(), b.MatchScore.ToArray());
        Assert.Equal(a.HandsScored, b.HandsScored);
        Assert.Equal(a.DealNumber, b.DealNumber);
    }

    [Property(MaxTest = 40)]
    public void Event_sequence_numbers_are_strictly_consecutive(ulong seed)
    {
        var start = GameEngine.Start(T.Rules, seed);
        int expected = start.Events.Count;
        Assert.Equal(Enumerable.Range(0, expected), start.Events.Select(e => e.Sequence));
        RandomGames.Play(seed, (_, _, _, accepted) =>
        {
            foreach (var e in accepted.Events)
            {
                Assert.Equal(expected, e.Sequence);
                expected++;
            }
        });
    }
}
