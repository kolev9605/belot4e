using System.Collections.Immutable;
using System.Linq;

namespace Belote.Core;

public static class Projection
{
    /// <summary>The per-seat view of a game state: only what the seat is entitled to see.</summary>
    public static SeatView For(GameState state, Seat seat)
    {
        var hand = state.Hand;
        var own = hand.Hands[(int)seat];
        if (hand.Contract is null && own.Length > hand.DealtCount)
        {
            own = own.Take(hand.DealtCount).ToImmutableArray();
        }

        var announcements = hand.Declared
            .Select(d => hand.AnnouncementsRevealed
                ? new PublicAnnouncement(d.Seat, d.Spec.Kind, d.Spec.Length, d.Spec, d.Points)
                : new PublicAnnouncement(d.Seat, d.Spec.Kind, d.Spec.Length, null, null))
            .ToImmutableArray();

        var ownDeclared = hand.Declared
            .Where(d => d.Seat == seat)
            .Select(d => d.Spec)
            .ToImmutableArray();

        // Public information: how many cards each seat holds (counts only, never identities).
        var sizes = hand.Hands
            .Select(h => hand.Contract is null && h.Length > hand.DealtCount ? hand.DealtCount : h.Length)
            .ToImmutableArray();

        return new SeatView(
            RuleSet: state.RuleSet,
            Seat: seat,
            Phase: state.Phase,
            Dealer: hand.Dealer,
            ToAct: hand.ToAct,
            DealNumber: hand.DealNumber,
            OwnHand: own,
            BiddingHistory: hand.Bidding.History,
            StandingBid: hand.Bidding.Standing,
            StandingBidder: hand.Bidding.Bidder,
            Contra: hand.Bidding.Contra,
            Recontra: hand.Bidding.Recontra,
            Contract: hand.Contract,
            Leader: hand.Leader,
            CurrentTrick: hand.CurrentPlays,
            CompletedTricks: hand.Tricks,
            Announcements: announcements,
            OwnDeclared: ownDeclared,
            Belotes: hand.Belotes,
            MatchScore: state.MatchScore,
            HeldOverPoints: state.HeldOverPoints,
            Winner: state.Winner,
            HandSizes: sizes);
    }
}
