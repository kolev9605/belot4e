using System.Collections.Immutable;
using Belote.Core;

namespace Belote.Bots;

/// <summary>The trick being played, from the point of view of the seat that must act.</summary>
internal sealed record TrickSituation(
    Suit? LedSuit,
    Seat? CurrentWinner,
    Card? CurrentBest,
    bool PartnerWinning,
    int PositionFromLast,
    int PointsOnTable,
    bool PartnerSecure)
{
    public bool IsLeading => LedSuit is null;

    public static TrickSituation From(SeatView view, CardMemory memory)
    {
        var contract = view.Contract!;
        var plays = view.CurrentTrick;
        int remainingAfterMe = 3 - plays.Length;

        if (plays.IsDefaultOrEmpty)
        {
            return new TrickSituation(null, null, null, false, 3, 0, false);
        }

        var best = TrickResolver.Best(view.RuleSet, contract.Kind, plays);
        int points = 0;
        foreach (var play in plays)
        {
            points += view.RuleSet.CardPoints(contract.Kind, play.Card);
        }

        bool partnerWinning = best.Seat == view.Seat.Partner();
        bool secure = false;
        if (partnerWinning)
        {
            secure = remainingAfterMe == 0 || (memory.IsMaster(best.Card) && !CanBeTrumped(view, memory, best, remainingAfterMe));
        }

        return new TrickSituation(
            plays[0].Card.Suit, best.Seat, best.Card, partnerWinning, remainingAfterMe, points, secure);
    }

    /// <summary>
    /// True when a still-to-play opponent is known to be void in the led suit while trumps remain
    /// unseen in a suit contract, so a plain winning card may yet be cut.
    /// </summary>
    private static bool CanBeTrumped(SeatView view, CardMemory memory, PlayedCard best, int remainingAfterMe)
    {
        var trump = view.Contract!.Kind.TrumpSuit();
        if (trump is null || best.Card.Suit == trump.Value || memory.TrumpsRemaining.IsEmpty)
        {
            return false;
        }

        for (int step = 1; step <= remainingAfterMe; step++)
        {
            var next = view.Seat.After(step);
            if (next.GetTeam() != view.Seat.GetTeam() && memory.IsVoid(next, best.Card.Suit))
            {
                return true;
            }
        }

        return false;
    }
}
