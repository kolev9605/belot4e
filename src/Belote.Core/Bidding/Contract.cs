using System.Collections.Immutable;

namespace Belote.Core;

/// <summary>The winning bid of a hand with its declarer and doubling multiplier (1, 2 or 4 by default).</summary>
public sealed record Contract(ContractKind Kind, Seat Declarer, int Multiplier)
{
    public Team DeclaringTeam => Declarer.GetTeam();

    public ContractFamily Family => Kind.Family();
}

/// <summary>One public bidding-phase action with the seat that made it.</summary>
public sealed record BidEntry(Seat Seat, PlayerAction Action);

public sealed record BiddingState(
    ImmutableArray<BidEntry> History,
    ContractKind? Standing,
    Seat? Bidder,
    bool Contra,
    bool Recontra,
    int PassStreak)
{
    public static BiddingState Empty { get; } =
        new(ImmutableArray<BidEntry>.Empty, null, null, false, false, 0);
}
