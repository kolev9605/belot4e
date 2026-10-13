using System.Collections.Immutable;
using Belote.Core;
using Xunit;

namespace Belote.Bots.Tests;

public class HandEvaluatorTests
{
    private static readonly RuleSet Rules = RuleSets.Bulgarian;
    private static readonly SmartBotOptions O = SmartBotOptions.Standard;

    private static HandEvaluation Eval(string hand, SmartBotOptions? o = null) =>
        HandEvaluator.Evaluate(Rules, V.Hand(hand), o ?? O);

    [Fact]
    public void Trump_jack_nine_and_ace_make_the_suit_the_best_contract()
    {
        var e = Eval("JH 9H AH KD 7C");
        Assert.Equal(ContractKind.Hearts, e.Best);
        Assert.True(e.ScoreOf(ContractKind.Hearts) > e.ScoreOf(ContractKind.Clubs));
        Assert.True(e.ScoreOf(ContractKind.Hearts) > e.ScoreOf(ContractKind.Spades));
    }

    [Fact]
    public void Four_aces_favour_no_trump()
    {
        Assert.Equal(ContractKind.NoTrump, Eval("AS AH AD AC 10S").Best);
    }

    [Fact]
    public void Jacks_and_nines_across_suits_favour_all_trump()
    {
        Assert.Equal(ContractKind.AllTrump, Eval("JS JH JD 9C 9S").Best);
    }

    [Fact]
    public void A_weak_hand_scores_below_the_bid_threshold_everywhere()
    {
        var e = Eval("7C 8D 7H 8S 9D");
        Assert.All(e.Scores.Values, s => Assert.True(s < O.BidThreshold));
    }

    [Fact]
    public void Holding_king_and_queen_of_trump_adds_the_belote_weight()
    {
        var with = Eval("KH QH 7C 8D 9S").ScoreOf(ContractKind.Hearts);
        var without = Eval("KH QH 7C 8D 9S", O with { BeloteWeight = 0 }).ScoreOf(ContractKind.Hearts);
        Assert.Equal(O.BeloteWeight, with - without, 6);
    }

    [Fact]
    public void Ties_resolve_to_the_cheaper_bid()
    {
        Assert.Equal(ContractKind.Clubs, Eval("7C 8D 7H 8S 7D", O with { TrumpLengthWeight = 0 }).Best);
    }

    [Fact]
    public void Only_kinds_in_the_rule_sets_ranking_are_scored()
    {
        var rules = Rules with
        {
            Bidding = Rules.Bidding with { Ranking = ImmutableArray.Create(ContractKind.Hearts, ContractKind.Spades) },
        };
        var e = HandEvaluator.Evaluate(rules, V.Hand("JH 9H AH KD 7C"), O);
        Assert.Equal(2, e.Scores.Count);
        Assert.False(e.Scores.ContainsKey(ContractKind.NoTrump));
    }
}
