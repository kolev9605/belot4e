using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Belote.Core;

public sealed record RuleSetIssue(string Path, string Message);

public static class RuleSetValidator
{
    public static IReadOnlyList<RuleSetIssue> Validate(RuleSet rules)
    {
        var issues = new List<RuleSetIssue>();

        if (rules.Deal.Pattern.IsDefaultOrEmpty || rules.Deal.Pattern.Any(p => p <= 0) || rules.Deal.Pattern.Sum() != 8)
        {
            issues.Add(new("deal.pattern", "Deal packets must be positive and sum to 8 cards per player."));
        }
        else if (rules.Deal.BiddingAfterPacket < 1 || rules.Deal.BiddingAfterPacket > rules.Deal.Pattern.Length)
        {
            issues.Add(new("deal.biddingAfterPacket", "Must be between 1 and the number of packets."));
        }

        var ranking = rules.Bidding.Ranking;
        var allKinds = Enum.GetValues<ContractKind>();
        if (ranking.IsDefaultOrEmpty || ranking.Distinct().Count() != ranking.Length
            || ranking.Length != allKinds.Length || allKinds.Any(k => !ranking.Contains(k)))
        {
            issues.Add(new("bidding.ranking", "Ranking must list each contract kind exactly once."));
        }

        if (rules.Bidding.CloseAfterConsecutivePasses is < 1 or > 3)
        {
            issues.Add(new("bidding.closeAfterConsecutivePasses", "Must be 1, 2 or 3."));
        }

        CheckTable(issues, "contracts.suit.trump", rules.Contracts.Suit.Trump);
        CheckTable(issues, "contracts.suit.plain", rules.Contracts.Suit.Plain);
        CheckTable(issues, "contracts.noTrump", rules.Contracts.NoTrump.Table);
        CheckTable(issues, "contracts.allTrump", rules.Contracts.AllTrump.Table);

        foreach (var kind in allKinds)
        {
            if (issues.Count == 0 && rules.PointsInPlay(kind) <= 0)
            {
                issues.Add(new($"contracts.{kind}", "Points in play must be positive."));
            }
        }

        if (rules.Announcements.CarrePoints.Length != 8)
        {
            issues.Add(new("announcements.carrePoints", "Must have one value per rank (8 values)."));
        }

        if (rules.Announcements.Sequences.IsDefaultOrEmpty
            || rules.Announcements.Sequences.Select(s => s.MinLength).Distinct().Count() != rules.Announcements.Sequences.Length
            || rules.Announcements.Sequences.Any(s => s.MinLength < 2 || s.MinLength > 8))
        {
            issues.Add(new("announcements.sequences", "Sequence minimum lengths must be distinct values from 2 to 8."));
        }

        if (rules.Announcements.RevealAfterTrick is < 1 or > 8)
        {
            issues.Add(new("announcements.revealAfterTrick", "Must be between 1 and 8."));
        }

        if (rules.Scoring.ContraMultiplier < 1 || rules.Scoring.RecontraMultiplier < rules.Scoring.ContraMultiplier)
        {
            issues.Add(new("scoring.multipliers", "Multipliers must be positive and recontra >= contra."));
        }

        if (rules.Scoring.GamePointDivisor <= 0)
        {
            issues.Add(new("scoring.gamePointDivisor", "Must be positive."));
        }

        if (rules.Scoring.AllTricksBonus < 0)
        {
            issues.Add(new("scoring.allTricksBonus", "Must not be negative."));
        }

        if (rules.Match.TargetScore <= 0)
        {
            issues.Add(new("match.targetScore", "Must be positive."));
        }

        return issues;
    }

    private static void CheckTable(List<RuleSetIssue> issues, string path, CardTable table)
    {
        var all = Enum.GetValues<Rank>();
        if (table.Order.IsDefaultOrEmpty || table.Order.Length != 8 || all.Any(r => !table.Order.Contains(r)))
        {
            issues.Add(new($"{path}.order", "Order must be a permutation of the eight ranks."));
        }

        if (table.Points.IsDefaultOrEmpty || table.Points.Length != 8 || table.Points.Any(p => p < 0))
        {
            issues.Add(new($"{path}.points", "Points must have one non-negative value per rank (8 values)."));
        }
    }

    public static void ThrowIfInvalid(RuleSet rules)
    {
        var issues = Validate(rules);
        if (issues.Count > 0)
        {
            throw new ArgumentException(
                "Invalid rule set: " + string.Join("; ", issues.Select(i => $"{i.Path}: {i.Message}")),
                nameof(rules));
        }
    }
}
