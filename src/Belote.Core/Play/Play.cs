using System;
using System.Collections.Immutable;
using System.Linq;

namespace Belote.Core;

/// <summary>Contract-dependent card strength used to decide who wins a trick.</summary>
public static class CardStrength
{
    /// <summary>
    /// Strength of a card in a trick where <paramref name="led"/> was led. Higher is stronger;
    /// -1 means the card cannot win (neither led suit nor trump).
    /// </summary>
    public static int Of(RuleSet rules, ContractKind kind, Suit led, Card card)
    {
        switch (kind.Family())
        {
            case ContractFamily.Suit:
                var trump = kind.TrumpSuit()!.Value;
                if (card.Suit == trump)
                {
                    return 100 + rules.Contracts.Suit.Trump.Strength(card.Rank);
                }

                return card.Suit == led ? rules.Contracts.Suit.Plain.Strength(card.Rank) : -1;
            case ContractFamily.NoTrump:
                return card.Suit == led ? rules.Contracts.NoTrump.Table.Strength(card.Rank) : -1;
            default:
                return card.Suit == led ? rules.Contracts.AllTrump.Table.Strength(card.Rank) : -1;
        }
    }
}

public static class TrickResolver
{
    /// <summary>The play that currently wins the (possibly incomplete) trick.</summary>
    public static PlayedCard Best(RuleSet rules, ContractKind kind, ImmutableArray<PlayedCard> plays)
    {
        var led = plays[0].Card.Suit;
        var best = plays[0];
        int bestStrength = CardStrength.Of(rules, kind, led, best.Card);
        for (int i = 1; i < plays.Length; i++)
        {
            int strength = CardStrength.Of(rules, kind, led, plays[i].Card);
            if (strength > bestStrength)
            {
                best = plays[i];
                bestStrength = strength;
            }
        }

        return best;
    }

    public static Seat Winner(RuleSet rules, ContractKind kind, ImmutableArray<PlayedCard> plays) =>
        Best(rules, kind, plays).Seat;
}

public static class LegalCards
{
    /// <summary>
    /// The cards a seat may legally play given its hand and the plays already in the trick,
    /// applying follow-suit, must-trump and must-overtrump obligations of the rule set.
    /// </summary>
    public static ImmutableArray<Card> For(
        RuleSet rules,
        ContractKind kind,
        Seat seat,
        ImmutableArray<Card> hand,
        ImmutableArray<PlayedCard> trick)
    {
        if (trick.IsDefaultOrEmpty || hand.IsEmpty || !rules.Play.MustFollowSuit)
        {
            return hand;
        }

        var led = trick[0].Card.Suit;
        var follow = hand.Where(c => c.Suit == led).ToImmutableArray();
        var best = TrickResolver.Best(rules, kind, trick);
        int bestStrength = CardStrength.Of(rules, kind, led, best.Card);

        switch (kind.Family())
        {
            case ContractFamily.NoTrump:
                return follow.IsEmpty ? hand : follow;

            case ContractFamily.AllTrump:
                {
                    if (follow.IsEmpty)
                    {
                        return hand;
                    }

                    if (rules.Play.MustOvertakeInAllTrump)
                    {
                        var higher = follow
                            .Where(c => CardStrength.Of(rules, kind, led, c) > bestStrength)
                            .ToImmutableArray();
                        if (!higher.IsEmpty)
                        {
                            return higher;
                        }
                    }

                    return follow;
                }

            default:
                {
                    var trump = kind.TrumpSuit()!.Value;
                    if (!follow.IsEmpty)
                    {
                        if (led == trump && rules.Play.MustOvertrump)
                        {
                            var higher = follow
                                .Where(c => CardStrength.Of(rules, kind, led, c) > bestStrength)
                                .ToImmutableArray();
                            if (!higher.IsEmpty)
                            {
                                return higher;
                            }
                        }

                        return follow;
                    }

                    var trumps = hand.Where(c => c.Suit == trump).ToImmutableArray();
                    if (trumps.IsEmpty)
                    {
                        return hand;
                    }

                    bool partnerWinning = best.Seat == seat.Partner();
                    if (partnerWinning && rules.Play.CutOnlyWhenOpponentWinning)
                    {
                        return hand;
                    }

                    if (best.Card.Suit == trump && rules.Play.MustOvertrump)
                    {
                        var higher = trumps
                            .Where(c => CardStrength.Of(rules, kind, led, c) > bestStrength)
                            .ToImmutableArray();
                        if (!higher.IsEmpty)
                        {
                            return higher;
                        }

                        return rules.Play.AnyCardWhenCannotOvertrump ? hand : trumps;
                    }

                    return trumps;
                }
        }
    }

    /// <summary>Explains why a card from the hand is not legal.</summary>
    public static RejectionReason Diagnose(
        RuleSet rules,
        ContractKind kind,
        ImmutableArray<Card> hand,
        ImmutableArray<PlayedCard> trick,
        Card card)
    {
        var led = trick[0].Card.Suit;
        bool hasLed = hand.Any(c => c.Suit == led);
        if (hasLed && card.Suit != led)
        {
            return RejectionReason.MustFollowSuit;
        }

        if (hasLed)
        {
            return RejectionReason.MustOvertrump;
        }

        return kind.Family() == ContractFamily.Suit && card.Suit != kind.TrumpSuit()!.Value
            ? RejectionReason.MustTrump
            : RejectionReason.MustOvertrump;
    }
}
