using System.Collections.Immutable;

namespace Belote.Core;

/// <summary>
/// An entry in the ordered game log. <see cref="Sequence"/> is monotonic from 0 and <see cref="Deal"/>
/// is the deal number the event belongs to. Events carry language-neutral data only.
/// </summary>
public abstract record GameEvent
{
    public int Sequence { get; init; }

    public int Deal { get; init; }

    public string Type => GetType().Name;
}

public sealed record GameStarted(ulong Seed, string RuleSetName, int RuleSetVersion) : GameEvent;

/// <summary>All eight cards of every seat (omniscient log); <c>DealtBeforeBidding</c> are visible while bidding.</summary>
public sealed record HandDealt(Seat Dealer, ImmutableArray<ImmutableArray<Card>> Hands, int DealtBeforeBidding) : GameEvent;

public sealed record BidMade(Seat Seat, ContractKind Kind) : GameEvent;

public sealed record PassMade(Seat Seat) : GameEvent;

public sealed record ContraMade(Seat Seat) : GameEvent;

public sealed record RecontraMade(Seat Seat) : GameEvent;

public sealed record BiddingClosed(ContractKind Kind, Seat Declarer, int Multiplier) : GameEvent;

public sealed record HandVoided(Seat NextDealer) : GameEvent;

public sealed record AnnouncementDeclared(Seat Seat, AnnouncementSpec Spec, int Points) : GameEvent;

public sealed record AnnouncementsRevealed(ImmutableArray<DeclaredAnnouncement> Announcements) : GameEvent;

public sealed record AnnouncementsCompared(Team? SequenceTeam, Team? CarreTeam) : GameEvent;

public sealed record CardPlayed(Seat Seat, Card Card, int TrickNumber) : GameEvent;

public sealed record BeloteAwarded(Seat Seat, Suit Suit, int Points) : GameEvent;

public sealed record TrickWon(Seat Winner, int TrickNumber, int Points) : GameEvent;

public sealed record HandScored(HandResult Result) : GameEvent;

public sealed record PointsHeldOver(int Points, int Total) : GameEvent;

public sealed record HeldOverAwarded(Team Team, int Points) : GameEvent;

public sealed record MatchScoreUpdated(int NorthSouth, int EastWest) : GameEvent;

public sealed record GameFinished(Team Winner, int NorthSouth, int EastWest) : GameEvent;
