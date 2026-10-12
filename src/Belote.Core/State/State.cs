using System.Collections.Immutable;

namespace Belote.Core;

public enum Phase
{
    Bidding,
    Playing,
    Finished,
}

public readonly record struct PlayedCard(Seat Seat, Card Card);

public sealed record Trick(Seat Leader, ImmutableArray<PlayedCard> Plays, Seat Winner);

public sealed record DeclaredAnnouncement(Seat Seat, AnnouncementSpec Spec, int Points);

/// <summary>Both king and queen of a belote suit were played by the same seat.</summary>
public readonly record struct BeloteAward(Seat Seat, Suit Suit);

/// <summary>
/// One hand (deal). Holds hidden information (all hands, including cards not yet dealt during
/// bidding); controllers never see it, only <see cref="SeatView"/> projections.
/// </summary>
public sealed record HandState(
    Seat Dealer,
    int DealNumber,
    ImmutableArray<ImmutableArray<Card>> Hands,
    int DealtCount,
    BiddingState Bidding,
    Contract? Contract,
    ImmutableArray<Trick> Tricks,
    Seat Leader,
    ImmutableArray<PlayedCard> CurrentPlays,
    Seat ToAct,
    ImmutableArray<DeclaredAnnouncement> Declared,
    bool AnnouncementsRevealed,
    Team? SequenceTeam,
    Team? CarreTeam,
    ImmutableArray<BeloteAward> Belotes);

public sealed record GameState(
    RuleSet RuleSet,
    ulong Seed,
    Phase Phase,
    HandState Hand,
    ImmutableArray<int> MatchScore,
    int HeldOverPoints,
    Team? Winner,
    int HandsScored,
    int NextSequence)
{
    public int DealNumber => Hand.DealNumber;

    public Seat ToAct => Hand.ToAct;
}

/// <summary>Public view of a declared announcement; the combination itself is revealed later.</summary>
public sealed record PublicAnnouncement(
    Seat Seat,
    AnnouncementKind Kind,
    int Length,
    AnnouncementSpec? Revealed,
    int? Points);

/// <summary>
/// Everything one seat is entitled to know. Contains no other seat's unplayed cards, no undealt
/// cards, and no seed.
/// </summary>
public sealed record SeatView(
    RuleSet RuleSet,
    Seat Seat,
    Phase Phase,
    Seat Dealer,
    Seat ToAct,
    int DealNumber,
    ImmutableArray<Card> OwnHand,
    ImmutableArray<BidEntry> BiddingHistory,
    ContractKind? StandingBid,
    Seat? StandingBidder,
    bool Contra,
    bool Recontra,
    Contract? Contract,
    Seat Leader,
    ImmutableArray<PlayedCard> CurrentTrick,
    ImmutableArray<Trick> CompletedTricks,
    ImmutableArray<PublicAnnouncement> Announcements,
    ImmutableArray<AnnouncementSpec> OwnDeclared,
    ImmutableArray<BeloteAward> Belotes,
    ImmutableArray<int> MatchScore,
    int HeldOverPoints,
    Team? Winner)
{
    public bool IsMyTurn => Phase != Phase.Finished && ToAct == Seat;
}
