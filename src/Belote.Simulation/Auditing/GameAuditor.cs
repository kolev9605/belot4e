using System;
using System.Collections.Generic;
using System.Linq;
using Belote.Core;

namespace Belote.Simulation;

public sealed record AuditIssue(int? Sequence, string Check, string Message)
{
    public override string ToString() => Sequence is { } s ? $"[#{s}] {Check}: {Message}" : $"{Check}: {Message}";
}

public sealed record AuditReport(IReadOnlyList<AuditIssue> Issues)
{
    public bool Ok => Issues.Count == 0;

    public AuditIssue? First => Issues.Count == 0 ? null : Issues[0];
}

/// <summary>
/// Verifies a finished game log in two independent ways: (1) replaying every accepted action through the
/// engine must reproduce the identical event stream; (2) separate accounting code re-derives card
/// conservation, trick winners, hand totals, point conservation and the match result from the events.
/// </summary>
public static class GameAuditor
{
    public static AuditReport Audit(GameLog log)
    {
        var issues = new List<AuditIssue>();
        try
        {
            Replay(log, issues);
            Invariants(log, issues);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            issues.Add(new AuditIssue(null, "AuditError", ex.Message));
        }

        return new AuditReport(issues);
    }

    public static AuditReport AuditJsonLines(string text) => Audit(GameLog.FromJsonLines(text));

    private static (Seat Seat, PlayerAction Action)? ActionOf(GameEvent e) => e switch
    {
        BidMade b => (b.Seat, new BidAction(b.Kind)),
        PassMade p => (p.Seat, new PassAction()),
        ContraMade c => (c.Seat, new ContraAction()),
        RecontraMade r => (r.Seat, new RecontraAction()),
        AnnouncementDeclared a => (a.Seat, new AnnounceAction(a.Spec)),
        CardPlayed c => (c.Seat, new PlayCardAction(c.Card)),
        _ => null,
    };

    private static void Replay(GameLog log, List<AuditIssue> issues)
    {
        var start = GameEngine.Start(log.RuleSet, log.Seed);
        var state = start.State;
        var replay = new List<GameEvent>(start.Events);

        foreach (var e in log.Events)
        {
            if (ActionOf(e) is not { } step)
            {
                continue;
            }

            var result = GameEngine.Apply(state, step.Seat, step.Action);
            if (result is Rejected rejected)
            {
                issues.Add(new AuditIssue(e.Sequence, "IllegalAction", rejected.Detail));
                return;
            }

            var accepted = (Accepted)result;
            state = accepted.State;
            replay.AddRange(accepted.Events);
        }

        int common = Math.Min(log.Events.Count, replay.Count);
        for (int i = 0; i < common; i++)
        {
            if (GameLog.EventLine(log.Events[i]) != GameLog.EventLine(replay[i]))
            {
                issues.Add(new AuditIssue(log.Events[i].Sequence, "ReplayMismatch", $"Event {i} differs from the replay."));
                return;
            }
        }

        if (log.Events.Count != replay.Count)
        {
            issues.Add(new AuditIssue(null, "ReplayMismatch", $"Log has {log.Events.Count} events, replay produced {replay.Count}."));
            return;
        }

        if (state.Phase != Phase.Finished)
        {
            issues.Add(new AuditIssue(null, "NoWinner", "The game did not reach a finished state."));
        }
    }

    private static void Invariants(GameLog log, List<AuditIssue> issues)
    {
        var rules = log.RuleSet;
        Seat[] dealt = Array.Empty<Seat>();
        Card[][] hands = new Card[4][];
        var played = new HashSet<Card>();
        var trickPlays = new List<(Seat Seat, Card Card)>();
        var trickWinners = new List<Seat>();
        ContractKind? kind = null;
        int multiplier = 1;
        var declared = new List<(Seat Seat, AnnouncementSpec Spec, int Points)>();
        int beloteTotal = 0;
        Team? sequenceTeam = null;
        Team? carreTeam = null;
        int expectedHeld = 0;
        int ns = 0;
        int ew = 0;
        bool finished = false;
        _ = dealt;

        void Issue(GameEvent e, string check, string message) =>
            issues.Add(new AuditIssue(e.Sequence, check, message));

        foreach (var e in log.Events)
        {
            if (finished)
            {
                Issue(e, "EventAfterFinish", "Events were recorded after the game finished.");
                return;
            }

            switch (e)
            {
                case HandDealt d:
                    {
                        var all = d.Hands.SelectMany(h => h).ToList();
                        if (d.Hands.Length != 4 || d.Hands.Any(h => h.Length != 8) || all.Distinct().Count() != 32)
                        {
                            Issue(e, "CardAccounting", "A deal must give four hands of eight distinct cards from the 32-card deck.");
                        }

                        for (int i = 0; i < 4; i++)
                        {
                            hands[i] = d.Hands[i].ToArray();
                        }

                        played.Clear();
                        trickPlays.Clear();
                        trickWinners.Clear();
                        kind = null;
                        multiplier = 1;
                        declared.Clear();
                        beloteTotal = 0;
                        sequenceTeam = null;
                        carreTeam = null;
                        break;
                    }

                case BiddingClosed bc:
                    kind = bc.Kind;
                    multiplier = bc.Multiplier;
                    break;

                case AnnouncementDeclared ad:
                    declared.Add((ad.Seat, ad.Spec, ad.Points));
                    if (!ad.Spec.Cards().All(c => hands[(int)ad.Seat].Contains(c)))
                    {
                        Issue(e, "AnnouncementInHand", $"{ad.Seat} declared {ad.Spec} without holding the cards.");
                    }

                    if (ad.Points != ad.Spec.Points(rules.Announcements))
                    {
                        Issue(e, "AnnouncementPoints", $"{ad.Spec} is worth {ad.Spec.Points(rules.Announcements)}, not {ad.Points}.");
                    }

                    break;

                case AnnouncementsCompared ac:
                    sequenceTeam = ac.SequenceTeam;
                    carreTeam = ac.CarreTeam;
                    break;

                case BeloteAwarded ba:
                    beloteTotal += ba.Points;
                    break;

                case CardPlayed cp:
                    {
                        if (!hands[(int)cp.Seat].Contains(cp.Card))
                        {
                            Issue(e, "CardAccounting", $"{cp.Seat} played {cp.Card}, which was not in their dealt hand.");
                        }

                        if (!played.Add(cp.Card))
                        {
                            Issue(e, "CardAccounting", $"{cp.Card} was played twice in one hand.");
                        }

                        trickPlays.Add((cp.Seat, cp.Card));
                        break;
                    }

                case TrickWon tw:
                    {
                        if (trickPlays.Count != 4 || kind is null)
                        {
                            Issue(e, "TrickShape", "A trick must consist of four plays after the contract is set.");
                            break;
                        }

                        var expected = IndependentWinner(rules, kind.Value, trickPlays);
                        if (expected != tw.Winner)
                        {
                            Issue(e, "TrickWinner", $"Trick {tw.TrickNumber} winner should be {expected}, log says {tw.Winner}.");
                        }

                        int points = trickPlays.Sum(p => IndependentCardPoints(rules, kind.Value, p.Card));
                        trickWinners.Add(tw.Winner);
                        if (trickWinners.Count == 8)
                        {
                            points += LastTrickBonus(rules, kind.Value);
                        }

                        if (points != tw.Points)
                        {
                            Issue(e, "TrickPoints", $"Trick {tw.TrickNumber} is worth {points}, log says {tw.Points}.");
                        }

                        trickPlays.Clear();
                        break;
                    }

                case HandScored hs:
                    {
                        var r = hs.Result;
                        if (kind is null || trickWinners.Count != 8 || played.Count != 32)
                        {
                            Issue(e, "CardAccounting", $"A scored hand must have 8 tricks and 32 distinct played cards (found {played.Count}).");
                            break;
                        }

                        int countedAnnouncements = declared
                            .Where(d => d.Spec.Kind == AnnouncementKind.Sequence
                                ? sequenceTeam == d.Seat.GetTeam()
                                : carreTeam == d.Seat.GetTeam())
                            .Sum(d => d.Points);
                        int sweep = trickWinners.Select(w => w.GetTeam()).Distinct().Count() == 1
                            ? rules.Scoring.AllTricksBonus
                            : 0;
                        int expectedPool = FullDeckPoints(rules, kind.Value)
                            + LastTrickBonus(rules, kind.Value)
                            + countedAnnouncements
                            + beloteTotal
                            + sweep;
                        if (r.RawPool != expectedPool)
                        {
                            Issue(e, "PointConservation", $"Raw points in play should be {expectedPool}, hand result has {r.RawPool}.");
                        }

                        int divisor = rules.Scoring.GamePointDivisor;
                        int total = expectedPool * multiplier;
                        int pool = total / divisor + (total % divisor * 2 >= divisor ? 1 : 0);
                        if (r.PoolGamePoints != pool)
                        {
                            Issue(e, "PointConservation", $"Game points in play should be {pool}, hand result has {r.PoolGamePoints}.");
                        }

                        int distributed = r.GamePoints[0] + r.GamePoints[1];
                        if (distributed != r.PoolGamePoints - r.NewlyHeld + r.AwardedFromHeldOver)
                        {
                            Issue(e, "PointConservation", "Awarded game points plus held-over change do not equal the pool.");
                        }

                        if (r.HeldOverBefore != expectedHeld
                            || r.HeldOverAfter != r.HeldOverBefore + r.NewlyHeld - r.AwardedFromHeldOver)
                        {
                            Issue(e, "HeldOver", "Held-over points are inconsistent with the previous hand.");
                        }

                        expectedHeld = r.HeldOverAfter;
                        ns += r.GamePoints[0];
                        ew += r.GamePoints[1];
                        break;
                    }

                case MatchScoreUpdated ms:
                    if (ms.NorthSouth != ns || ms.EastWest != ew)
                    {
                        Issue(e, "MatchScore", $"Match score should be {ns}-{ew}, log says {ms.NorthSouth}-{ms.EastWest}.");
                    }

                    break;

                case GameFinished gf:
                    {
                        finished = true;
                        int winnerScore = gf.Winner == Team.NorthSouth ? ns : ew;
                        int otherScore = gf.Winner == Team.NorthSouth ? ew : ns;
                        if (winnerScore < rules.Match.TargetScore || winnerScore < otherScore)
                        {
                            Issue(e, "Winner", "The winner must have reached the target and not trail the other team.");
                        }

                        break;
                    }
            }
        }

        if (!finished)
        {
            issues.Add(new AuditIssue(null, "NoWinner", "The log does not end with GameFinished."));
        }
    }

    // The helpers below deliberately re-implement strength and points from the rule-set tables
    // instead of calling the engine's CardStrength/RuleSet helpers.

    private static Seat IndependentWinner(RuleSet rules, ContractKind kind, List<(Seat Seat, Card Card)> plays)
    {
        var led = plays[0].Card.Suit;
        int Rate(Card c)
        {
            switch (kind)
            {
                case ContractKind.NoTrump:
                    return c.Suit == led ? 1 + Index(rules.Contracts.NoTrump.Table.Order, c.Rank) : 0;
                case ContractKind.AllTrump:
                    return c.Suit == led ? 1 + Index(rules.Contracts.AllTrump.Table.Order, c.Rank) : 0;
                default:
                    var trump = (Suit)(int)kind;
                    if (c.Suit == trump)
                    {
                        return 100 + Index(rules.Contracts.Suit.Trump.Order, c.Rank);
                    }

                    return c.Suit == led ? 1 + Index(rules.Contracts.Suit.Plain.Order, c.Rank) : 0;
            }
        }

        // Index is "how many ranks are weaker", so larger means stronger.
        var best = plays[0];
        foreach (var play in plays.Skip(1))
        {
            if (Rate(play.Card) > Rate(best.Card))
            {
                best = play;
            }
        }

        return best.Seat;
    }

    private static int Index(System.Collections.Immutable.ImmutableArray<Rank> order, Rank rank)
    {
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == rank)
            {
                return order.Length - 1 - i;
            }
        }

        throw new ArgumentException("Rank missing from order.");
    }

    private static int IndependentCardPoints(RuleSet rules, ContractKind kind, Card card) => kind switch
    {
        ContractKind.NoTrump => rules.Contracts.NoTrump.Table.Points[(int)card.Rank],
        ContractKind.AllTrump => rules.Contracts.AllTrump.Table.Points[(int)card.Rank],
        _ => (int)card.Suit == (int)kind
            ? rules.Contracts.Suit.Trump.Points[(int)card.Rank]
            : rules.Contracts.Suit.Plain.Points[(int)card.Rank],
    };

    private static int LastTrickBonus(RuleSet rules, ContractKind kind) => kind switch
    {
        ContractKind.NoTrump => rules.Contracts.NoTrump.LastTrickBonus,
        ContractKind.AllTrump => rules.Contracts.AllTrump.LastTrickBonus,
        _ => rules.Contracts.Suit.LastTrickBonus,
    };

    private static int FullDeckPoints(RuleSet rules, ContractKind kind) =>
        Deck.Standard32.Sum(c => IndependentCardPoints(rules, kind, c));
}
