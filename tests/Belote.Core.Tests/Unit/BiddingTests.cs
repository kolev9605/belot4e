using Belote.Core;
using Xunit;

namespace Belote.Core.Tests;

public class DealingTests
{
    [Fact]
    public void R1_R2_Each_seat_gets_eight_distinct_cards_and_all_32_are_dealt_once()
    {
        var start = GameEngine.Start(T.Rules, 12345);
        var dealt = Assert.IsType<HandDealt>(start.Events.Single(e => e is HandDealt));

        Assert.Equal(4, dealt.Hands.Length);
        Assert.All(dealt.Hands, h => Assert.Equal(8, h.Length));
        Assert.Equal(32, dealt.Hands.SelectMany(h => h).Distinct().Count());
        Assert.Equal(5, dealt.DealtBeforeBidding);
    }

    [Fact]
    public void R2_Deal_follows_the_three_two_three_packet_pattern_from_the_player_after_the_dealer()
    {
        const ulong seed = 777;
        var start = GameEngine.Start(T.Rules, seed);
        var hand = start.State.Hand;

        var deck = Deck.Standard32.ToArray();
        new SeededRandom(Seeds.ForHand(seed, 1)).Shuffle(deck);

        var first = hand.Dealer.Next();
        var expected = new List<Card>[4];
        for (int i = 0; i < 4; i++)
        {
            expected[i] = new List<Card>();
        }

        int next = 0;
        foreach (int packet in new[] { 3, 2, 3 })
        {
            for (int k = 0; k < 4; k++)
            {
                for (int c = 0; c < packet; c++)
                {
                    expected[(int)first.After(k)].Add(deck[next++]);
                }
            }
        }

        foreach (var seat in Seats.All)
        {
            Assert.Equal(expected[(int)seat].ToArray(), hand.Hands[(int)seat].ToArray());
        }
    }

    [Fact]
    public void Same_seed_gives_the_same_deal_and_different_seeds_differ()
    {
        var a = GameEngine.Start(T.Rules, 5).State.Hand;
        var b = GameEngine.Start(T.Rules, 5).State.Hand;
        var c = GameEngine.Start(T.Rules, 6).State.Hand;
        Assert.Equal(a.Dealer, b.Dealer);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(a.Hands[i].ToArray(), b.Hands[i].ToArray());
        }

        Assert.NotEqual(a.Hands[0].ToArray(), c.Hands[0].ToArray());
    }

    [Fact]
    public void R2_Bidding_happens_after_five_cards_and_the_last_three_are_dealt_when_bidding_closes()
    {
        var state = GameEngine.Start(T.Rules, 42).State;
        var first = state.ToAct;
        Assert.Equal(5, Projection.For(state, first).OwnHand.Length);
        Assert.All(Seats.All, s => Assert.Equal(5, Projection.For(state, s).OwnHand.Length));

        state = T.Accept(state, state.ToAct, new BidAction(ContractKind.Hearts));
        state = T.Accept(state, state.ToAct, new PassAction());
        state = T.Accept(state, state.ToAct, new PassAction());
        state = T.Accept(state, state.ToAct, new PassAction());

        Assert.Equal(Phase.Playing, state.Phase);
        Assert.All(Seats.All, s => Assert.Equal(8, Projection.For(state, s).OwnHand.Length));
    }

    [Fact]
    public void The_dealer_rotates_between_hands()
    {
        int transitions = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var dealers = new List<Seat> { GameEngine.Start(T.Rules, seed).State.Hand.Dealer };
            RandomGames.Play(seed, (_, _, _, accepted) =>
            {
                dealers.AddRange(accepted.Events.OfType<HandDealt>().Select(e => e.Dealer));
            });

            for (int i = 1; i < dealers.Count; i++)
            {
                Assert.Equal(dealers[i - 1].Next(), dealers[i]);
                transitions++;
            }
        }

        Assert.True(transitions > 0);
    }
}

public class BiddingTests
{
    private static GameState NewGame(ulong seed = 1) => GameEngine.Start(T.Rules, seed).State;

    [Fact]
    public void R3_A_bid_must_outrank_the_standing_bid()
    {
        var state = NewGame();
        var s0 = state.ToAct;
        state = T.Accept(state, s0, new BidAction(ContractKind.Hearts));
        var s1 = state.ToAct;

        Assert.Equal(RejectionReason.BidNotHigher, T.Reject(state, s1, new BidAction(ContractKind.Clubs)).Reason);
        Assert.Equal(RejectionReason.BidNotHigher, T.Reject(state, s1, new BidAction(ContractKind.Diamonds)).Reason);
        Assert.Equal(RejectionReason.BidNotHigher, T.Reject(state, s1, new BidAction(ContractKind.Hearts)).Reason);

        state = T.Accept(state, s1, new BidAction(ContractKind.Spades));
        Assert.Equal(ContractKind.Spades, state.Hand.Bidding.Standing);
        Assert.Equal(s1, state.Hand.Bidding.Bidder);
    }

    [Fact]
    public void R3_All_six_contract_kinds_can_be_bid_and_all_trump_is_the_top_bid()
    {
        foreach (var kind in Enum.GetValues<ContractKind>())
        {
            var state = NewGame();
            state = T.Accept(state, state.ToAct, new BidAction(kind));
            Assert.Equal(kind, state.Hand.Bidding.Standing);
        }

        var top = NewGame();
        top = T.Accept(top, top.ToAct, new BidAction(ContractKind.AllTrump));
        var legal = LegalActions.For(Projection.For(top, top.ToAct));
        Assert.DoesNotContain(legal, a => a is BidAction);
    }

    [Fact]
    public void R4_Bidding_closes_after_three_consecutive_passes_following_a_bid()
    {
        var state = NewGame();
        var bidder = state.ToAct;
        var dealer = state.Hand.Dealer;
        state = T.Accept(state, bidder, new BidAction(ContractKind.Spades));
        state = T.Accept(state, state.ToAct, new PassAction());
        state = T.Accept(state, state.ToAct, new PassAction());
        Assert.Equal(Phase.Bidding, state.Phase);
        state = T.Accept(state, state.ToAct, new PassAction());

        Assert.Equal(Phase.Playing, state.Phase);
        Assert.Equal(new Contract(ContractKind.Spades, bidder, 1), state.Hand.Contract);
        Assert.Equal(dealer.Next(), state.ToAct);
    }

    [Fact]
    public void R4_Declarer_is_the_last_bidder_and_a_higher_bid_restarts_the_pass_count()
    {
        var state = NewGame();
        var s0 = state.ToAct;
        state = T.Accept(state, s0, new BidAction(ContractKind.Clubs));
        var s1 = state.ToAct;
        state = T.Accept(state, s1, new BidAction(ContractKind.Hearts));
        state = T.Accept(state, state.ToAct, new PassAction());
        state = T.Accept(state, state.ToAct, new PassAction());
        state = T.Accept(state, state.ToAct, new PassAction());

        Assert.Equal(new Contract(ContractKind.Hearts, s1, 1), state.Hand.Contract);
    }

    [Fact]
    public void R5_Contra_by_an_opponent_then_recontra_by_the_bidding_team_closes_bidding_at_times_four()
    {
        var state = NewGame();
        var bidder = state.ToAct;
        state = T.Accept(state, bidder, new BidAction(ContractKind.Hearts));
        var opponent = state.ToAct;
        state = T.Accept(state, opponent, new ContraAction());
        Assert.True(state.Hand.Bidding.Contra);
        Assert.Equal(Phase.Bidding, state.Phase);

        var partner = state.ToAct;
        Assert.Equal(bidder.Partner(), partner);
        state = T.Accept(state, partner, new RecontraAction());

        Assert.Equal(Phase.Playing, state.Phase);
        Assert.Equal(new Contract(ContractKind.Hearts, bidder, 4), state.Hand.Contract);
    }

    [Fact]
    public void R5_Contra_alone_gives_a_times_two_multiplier_once_passes_follow()
    {
        var state = NewGame();
        var bidder = state.ToAct;
        state = T.Accept(state, bidder, new BidAction(ContractKind.NoTrump));
        state = T.Accept(state, state.ToAct, new ContraAction());
        state = T.Accept(state, state.ToAct, new PassAction());
        state = T.Accept(state, state.ToAct, new PassAction());
        state = T.Accept(state, state.ToAct, new PassAction());

        Assert.Equal(new Contract(ContractKind.NoTrump, bidder, 2), state.Hand.Contract);
    }

    [Fact]
    public void R5_Contra_is_illegal_without_a_bid_or_against_your_own_team()
    {
        var state = NewGame();
        Assert.Equal(RejectionReason.ContraNotAllowed, T.Reject(state, state.ToAct, new ContraAction()).Reason);

        state = T.Accept(state, state.ToAct, new BidAction(ContractKind.Hearts));
        state = T.Accept(state, state.ToAct, new PassAction());
        var partnerOfBidder = state.ToAct;
        Assert.Equal(RejectionReason.ContraNotAllowed, T.Reject(state, partnerOfBidder, new ContraAction()).Reason);

        state = T.Accept(state, partnerOfBidder, new PassAction());
        state = T.Accept(state, state.ToAct, new ContraAction());
        Assert.True(state.Hand.Bidding.Contra);
    }

    [Fact]
    public void R5_Recontra_is_only_for_the_bidding_team_after_a_contra_and_no_further_doubling_follows()
    {
        var state = NewGame();
        var bidder = state.ToAct;
        Assert.Equal(RejectionReason.RecontraNotAllowed, T.Reject(state, bidder, new RecontraAction()).Reason);

        state = T.Accept(state, bidder, new BidAction(ContractKind.Spades));
        var contraSeat = state.ToAct;
        state = T.Accept(state, contraSeat, new ContraAction());
        var bidderPartner = state.ToAct;
        state = T.Accept(state, bidderPartner, new PassAction());

        var contraPartner = state.ToAct;
        Assert.Equal(contraSeat.Partner(), contraPartner);
        Assert.Equal(RejectionReason.RecontraNotAllowed, T.Reject(state, contraPartner, new RecontraAction()).Reason);
        Assert.Equal(RejectionReason.ContraNotAllowed, T.Reject(state, contraPartner, new ContraAction()).Reason);
        Assert.Equal(RejectionReason.BidNotAllowed, T.Reject(state, contraPartner, new BidAction(ContractKind.AllTrump)).Reason);
    }

    [Fact]
    public void R6_All_pass_voids_the_hand_and_the_next_dealer_redeals_with_no_score()
    {
        var state = NewGame(3);
        var dealer = state.Hand.Dealer;
        var events = new List<GameEvent>();
        for (int i = 0; i < 4; i++)
        {
            state = T.Accept(state, state.ToAct, new PassAction(), events);
        }

        Assert.Equal(Phase.Bidding, state.Phase);
        Assert.Equal(2, state.DealNumber);
        Assert.Equal(dealer.Next(), state.Hand.Dealer);
        Assert.Equal(new[] { 0, 0 }, state.MatchScore.ToArray());
        Assert.Equal(0, state.HandsScored);
        Assert.Single(events.OfType<HandVoided>());
        Assert.Equal(2, events.OfType<HandDealt>().Single().Deal);
        Assert.Equal(5, Projection.For(state, state.ToAct).OwnHand.Length);
    }

    [Fact]
    public void Actions_out_of_turn_or_in_the_wrong_phase_are_rejected()
    {
        var state = NewGame();
        var other = state.ToAct.Next();
        Assert.Equal(RejectionReason.NotYourTurn, T.Reject(state, other, new PassAction()).Reason);
        Assert.Equal(RejectionReason.WrongPhase, T.Reject(state, state.ToAct, T.Play("JH")).Reason);
        Assert.Equal(
            RejectionReason.WrongPhase,
            T.Reject(state, state.ToAct, new AnnounceAction(AnnouncementSpec.Carre(Rank.Jack))).Reason);
    }

    [Fact]
    public void Bidding_never_changes_any_hand_and_always_leaves_a_legal_action()
    {
        var state = NewGame(77);
        var before = state.Hand.Hands;
        var rng = new SeededRandom(1);
        while (state.Phase == Phase.Bidding && state.DealNumber == 1)
        {
            var legal = LegalActions.For(Projection.For(state, state.ToAct));
            Assert.NotEmpty(legal);
            Assert.Contains(legal, a => a is PassAction);
            state = T.Accept(state, state.ToAct, legal[rng.NextInt(legal.Count)]);
            if (state.DealNumber == 1)
            {
                Assert.Equal(before, state.Hand.Hands);
            }
        }
    }
}
