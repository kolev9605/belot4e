using System;
using System.Collections.Immutable;
using System.Linq;

namespace Belote.Core;

public enum HandOutcome
{
    /// <summary>The declaring team scored more points than the defenders.</summary>
    Made,

    /// <summary>The declaring team scored fewer points than the defenders ("inside").</summary>
    Failed,

    /// <summary>Both teams scored the same; the declarer's points are held over.</summary>
    Tied,
}

/// <summary>Full breakdown of a scored hand. Arrays are indexed by <see cref="Team"/>.</summary>
public sealed record HandResult(
    ContractKind Kind,
    Team DeclaringTeam,
    int Multiplier,
    ImmutableArray<int> CardPoints,
    Team LastTrickTeam,
    int LastTrickBonus,
    ImmutableArray<int> AnnouncementPoints,
    ImmutableArray<int> BelotePoints,
    Team? AllTricksTeam,
    int AllTricksBonus,
    ImmutableArray<int> RawTotals,
    HandOutcome Outcome,
    int PoolGamePoints,
    ImmutableArray<int> GamePoints,
    int HeldOverBefore,
    int AwardedFromHeldOver,
    Team? HeldOverAwardedTo,
    int NewlyHeld,
    int HeldOverAfter)
{
    /// <summary>Sum of both teams' raw hand points (before multiplier and conversion).</summary>
    public int RawPool => RawTotals[0] + RawTotals[1];
}

public static class HandScorer
{
    public static HandResult Score(
        RuleSet rules,
        Contract contract,
        ImmutableArray<Trick> tricks,
        ImmutableArray<DeclaredAnnouncement> declared,
        Team? sequenceTeam,
        Team? carreTeam,
        ImmutableArray<BeloteAward> belotes,
        int heldOverBefore)
    {
        var kind = contract.Kind;
        var cardPoints = new int[2];
        foreach (var trick in tricks)
        {
            int points = trick.Plays.Sum(p => rules.CardPoints(kind, p.Card));
            cardPoints[(int)trick.Winner.GetTeam()] += points;
        }

        var lastTrickTeam = tricks[^1].Winner.GetTeam();
        int lastTrickBonus = rules.LastTrickBonus(kind);

        var announcementPoints = new int[2];
        foreach (var d in declared)
        {
            var team = d.Seat.GetTeam();
            bool counts = d.Spec.Kind == AnnouncementKind.Sequence ? sequenceTeam == team : carreTeam == team;
            if (counts)
            {
                announcementPoints[(int)team] += d.Points;
            }
        }

        var belotePoints = new int[2];
        foreach (var award in belotes)
        {
            belotePoints[(int)award.Seat.GetTeam()] += rules.Announcements.BelotePoints;
        }

        Team? allTricksTeam = tricks.Select(t => t.Winner.GetTeam()).Distinct().Count() == 1
            ? tricks[0].Winner.GetTeam()
            : null;
        int allTricksBonus = allTricksTeam is null ? 0 : rules.Scoring.AllTricksBonus;

        var raw = new int[2];
        for (int t = 0; t < 2; t++)
        {
            raw[t] = cardPoints[t] + announcementPoints[t] + belotePoints[t];
        }

        raw[(int)lastTrickTeam] += lastTrickBonus;
        if (allTricksTeam is { } sweeper)
        {
            raw[(int)sweeper] += allTricksBonus;
        }

        var declarerTeam = contract.DeclaringTeam;
        var defenderTeam = declarerTeam.Opponent();
        int declarerRaw = raw[(int)declarerTeam];
        int defenderRaw = raw[(int)defenderTeam];
        var outcome = declarerRaw > defenderRaw
            ? HandOutcome.Made
            : declarerRaw < defenderRaw ? HandOutcome.Failed : HandOutcome.Tied;

        int divisor = rules.Scoring.GamePointDivisor;
        int multiplier = contract.Multiplier;
        int[] amounts;
        Team favored;
        Team? winner = null;

        if (outcome == HandOutcome.Tied)
        {
            amounts = new[] { raw[0] * multiplier, raw[1] * multiplier };
            favored = declarerTeam;
        }
        else
        {
            winner = outcome == HandOutcome.Made ? declarerTeam : defenderTeam;
            bool winnerTakesAll = outcome == HandOutcome.Failed
                || (multiplier > 1 && rules.Scoring.WinnerTakesAllUnderMultiplier);
            if (winnerTakesAll)
            {
                amounts = new int[2];
                amounts[(int)winner.Value] = (raw[0] + raw[1]) * multiplier;
                favored = winner.Value;
            }
            else
            {
                amounts = new[] { raw[0] * multiplier, raw[1] * multiplier };
                favored = rules.Scoring.RoundingTieFavorsFewerPoints
                    ? (raw[0] <= raw[1] ? Team.NorthSouth : Team.EastWest)
                    : declarerTeam;
            }
        }

        int pool = GamePointConverter.Pool(amounts[0] + amounts[1], divisor);
        var shares = GamePointConverter.Split(amounts, divisor, favored);

        int awardedFromHeld = 0;
        Team? heldTo = null;
        int newlyHeld = 0;
        int heldAfter = heldOverBefore;
        var gamePoints = new[] { shares[0], shares[1] };

        if (outcome == HandOutcome.Tied)
        {
            newlyHeld = shares[(int)declarerTeam];
            gamePoints[(int)declarerTeam] = 0;
            heldAfter = heldOverBefore + newlyHeld;
        }
        else if (heldOverBefore > 0)
        {
            heldTo = winner;
            awardedFromHeld = heldOverBefore;
            gamePoints[(int)winner!.Value] += heldOverBefore;
            heldAfter = 0;
        }

        return new HandResult(
            kind,
            declarerTeam,
            multiplier,
            ImmutableArray.Create(cardPoints),
            lastTrickTeam,
            lastTrickBonus,
            ImmutableArray.Create(announcementPoints),
            ImmutableArray.Create(belotePoints),
            allTricksTeam,
            allTricksBonus,
            ImmutableArray.Create(raw),
            outcome,
            pool,
            ImmutableArray.Create(gamePoints),
            heldOverBefore,
            awardedFromHeld,
            heldTo,
            newlyHeld,
            heldAfter);
    }
}

public readonly record struct MatchOutcome(ImmutableArray<int> Score, Team? Winner);

public static class MatchScorer
{
    /// <summary>Adds a scored hand to the match score and decides whether the match is over.</summary>
    public static MatchOutcome Apply(RuleSet rules, ImmutableArray<int> score, HandResult result)
    {
        var updated = ImmutableArray.Create(
            score[0] + result.GamePoints[0],
            score[1] + result.GamePoints[1]);

        int target = rules.Match.TargetScore;
        bool northSouthReached = updated[0] >= target;
        bool eastWestReached = updated[1] >= target;

        if (!northSouthReached && !eastWestReached)
        {
            return new MatchOutcome(updated, null);
        }

        if (rules.Match.AllTricksCannotEndMatch && result.AllTricksTeam is not null)
        {
            return new MatchOutcome(updated, null);
        }

        if (northSouthReached && eastWestReached)
        {
            if (updated[0] == updated[1])
            {
                return new MatchOutcome(updated, null);
            }

            return new MatchOutcome(updated, updated[0] > updated[1] ? Team.NorthSouth : Team.EastWest);
        }

        return new MatchOutcome(updated, northSouthReached ? Team.NorthSouth : Team.EastWest);
    }
}
