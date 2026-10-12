using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Belote.Core;

/// <summary>
/// Single source of truth for what a seat may do, computed from the seat's own view only. The engine
/// validates every action through <see cref="Check"/>, so accepted means "in the legal set".
/// </summary>
public static class LegalActions
{
    public static IReadOnlyList<PlayerAction> For(SeatView view)
    {
        if (!view.IsMyTurn)
        {
            return ImmutableArray<PlayerAction>.Empty;
        }

        var result = new List<PlayerAction>();
        if (view.Phase == Phase.Bidding)
        {
            foreach (PlayerAction candidate in BiddingCandidates(view))
            {
                if (Check(view, candidate) is null)
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

        var contract = view.Contract!;
        foreach (var announcement in AnnouncementOptions(view))
        {
            result.Add(new AnnounceAction(announcement));
        }

        foreach (var card in LegalCards.For(view.RuleSet, contract.Kind, view.Seat, view.OwnHand, view.CurrentTrick))
        {
            result.Add(new PlayCardAction(card));
        }

        return result;
    }

    /// <summary>Null when the action is legal for the seat in this view; otherwise the reason.</summary>
    public static RejectionReason? Check(SeatView view, PlayerAction action)
    {
        if (view.Phase == Phase.Finished)
        {
            return RejectionReason.GameFinished;
        }

        if (view.ToAct != view.Seat)
        {
            return RejectionReason.NotYourTurn;
        }

        var rules = view.RuleSet;

        if (view.Phase == Phase.Bidding)
        {
            switch (action)
            {
                case PassAction:
                    return null;

                case BidAction bid:
                    if (!rules.Bidding.Ranking.Contains(bid.Kind) || view.Contra || view.Recontra)
                    {
                        return RejectionReason.BidNotAllowed;
                    }

                    if (view.StandingBid is { } standing && rules.BidRank(bid.Kind) <= rules.BidRank(standing))
                    {
                        return RejectionReason.BidNotHigher;
                    }

                    return null;

                case ContraAction:
                    if (!rules.Bidding.Contra || view.StandingBid is null || view.Contra
                        || view.StandingBidder!.Value.GetTeam() == view.Seat.GetTeam())
                    {
                        return RejectionReason.ContraNotAllowed;
                    }

                    return null;

                case RecontraAction:
                    if (!rules.Bidding.Recontra || !view.Contra || view.Recontra
                        || view.StandingBidder!.Value.GetTeam() != view.Seat.GetTeam())
                    {
                        return RejectionReason.RecontraNotAllowed;
                    }

                    return null;

                default:
                    return RejectionReason.WrongPhase;
            }
        }

        var contract = view.Contract!;
        switch (action)
        {
            case PlayCardAction play:
                if (!view.OwnHand.Contains(play.Card))
                {
                    return RejectionReason.CardNotInHand;
                }

                var legal = LegalCards.For(rules, contract.Kind, view.Seat, view.OwnHand, view.CurrentTrick);
                return legal.Contains(play.Card)
                    ? null
                    : LegalCards.Diagnose(rules, contract.Kind, view.OwnHand, view.CurrentTrick, play.Card);

            case AnnounceAction announce:
                if (view.CompletedTricks.Length > 0 || !view.CurrentTrick.All(p => p.Seat != view.Seat))
                {
                    return RejectionReason.AnnouncementWindowClosed;
                }

                if (!rules.Announcements.AllowedIn(contract.Family))
                {
                    return RejectionReason.AnnouncementNotAllowed;
                }

                return AnnouncementOptions(view).Contains(announce.Spec)
                    ? null
                    : RejectionReason.AnnouncementNotInHand;

            default:
                return RejectionReason.WrongPhase;
        }
    }

    private static IEnumerable<PlayerAction> BiddingCandidates(SeatView view)
    {
        yield return new PassAction();
        foreach (var kind in view.RuleSet.Bidding.Ranking)
        {
            yield return new BidAction(kind);
        }

        yield return new ContraAction();
        yield return new RecontraAction();
    }

    /// <summary>Announcements the seat may declare right now (with its first card, disjoint from earlier ones).</summary>
    private static ImmutableArray<AnnouncementSpec> AnnouncementOptions(SeatView view)
    {
        var contract = view.Contract;
        if (contract is null || view.CompletedTricks.Length > 0
            || view.CurrentTrick.Any(p => p.Seat == view.Seat)
            || !view.RuleSet.Announcements.AllowedIn(contract.Family))
        {
            return ImmutableArray<AnnouncementSpec>.Empty;
        }

        var used = new HashSet<Card>(view.OwnDeclared.SelectMany(s => s.Cards()));
        return AnnouncementDetector.Detect(view.RuleSet.Announcements, view.OwnHand)
            .Where(spec => !spec.Cards().Any(used.Contains))
            .ToImmutableArray();
    }
}
