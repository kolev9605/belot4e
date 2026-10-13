using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Belote.Core;

namespace Belote.Bots;

/// <summary>Estimated strength of a hand for every contract kind the rule set allows to bid.</summary>
internal sealed record HandEvaluation(ImmutableDictionary<ContractKind, double> Scores, ContractKind? Best)
{
    public double ScoreOf(ContractKind kind) => Scores.TryGetValue(kind, out var s) ? s : 0;
}

/// <summary>
/// Scores a (typically 5-card) bidding hand per contract kind in estimated card points, using only
/// the rule set's point tables and strength orders; see research.md D4.
/// </summary>
internal static class HandEvaluator
{
    public static HandEvaluation Evaluate(RuleSet rules, IReadOnlyCollection<Card> hand, SmartBotOptions options)
    {
        var scores = ImmutableDictionary.CreateBuilder<ContractKind, double>();
        ContractKind? best = null;
        double bestScore = double.MinValue;

        foreach (var kind in rules.Bidding.Ranking)
        {
            double score = Score(rules, kind, hand, options);
            scores[kind] = score;
            // Ranking is ascending by bid rank, so a strict '>' keeps the cheaper bid on ties.
            if (score > bestScore)
            {
                bestScore = score;
                best = kind;
            }
        }

        return new HandEvaluation(scores.ToImmutable(), best);
    }

    private static double Score(RuleSet rules, ContractKind kind, IReadOnlyCollection<Card> hand, SmartBotOptions o)
    {
        switch (kind.Family())
        {
            case ContractFamily.Suit:
                return SuitScore(rules, kind, hand, o);
            case ContractFamily.NoTrump:
                return hand.Sum(c => rules.CardPoints(kind, c)) * o.NoTrumpScale;
            default:
                return hand.Sum(c => rules.CardPoints(kind, c)) * o.AllTrumpScale;
        }
    }

    private static double SuitScore(RuleSet rules, ContractKind kind, IReadOnlyCollection<Card> hand, SmartBotOptions o)
    {
        var trump = kind.TrumpSuit()!.Value;
        var trumps = hand.Where(c => c.Suit == trump).ToList();
        double score = trumps.Sum(c => rules.CardPoints(kind, c));

        if (trumps.Count > 1)
        {
            score += o.TrumpLengthWeight * (trumps.Count - 1);
        }

        if (trumps.Any(c => c.Rank == Rank.King) && trumps.Any(c => c.Rank == Rank.Queen))
        {
            score += o.BeloteWeight;
        }

        var topPlain = rules.Contracts.Suit.Plain.Order[0];
        var secondPlain = rules.Contracts.Suit.Plain.Order[1];
        foreach (var card in hand.Where(c => c.Suit != trump))
        {
            if (card.Rank == topPlain)
            {
                score += o.SideAceWeight;
            }
            else if (card.Rank == secondPlain && hand.Any(c => c.Suit == card.Suit && c.Rank == topPlain))
            {
                score += o.SideAceWeight / 2;
            }
        }

        return score;
    }
}
