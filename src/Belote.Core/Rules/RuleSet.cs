using System.Collections.Immutable;
using System.Linq;

namespace Belote.Core;

public enum ContractKind
{
    Clubs = 0,
    Diamonds = 1,
    Hearts = 2,
    Spades = 3,
    NoTrump = 4,
    AllTrump = 5,
}

public enum ContractFamily
{
    Suit,
    NoTrump,
    AllTrump,
}

public enum AllPassPolicy
{
    /// <summary>The hand is voided with no score; the next dealer reshuffles and redeals.</summary>
    VoidAndRedealNextDealer,
}

public enum AnnouncementComparison
{
    /// <summary>Sequences are compared among sequences and four-of-a-kinds among four-of-a-kinds.</summary>
    PerCategory,
}

public enum RoundingMode
{
    /// <summary>
    /// Pool game points = round(total / divisor); each team gets its floor share and the leftover
    /// game points go to the largest remainders (ties to the configured favoured team).
    /// </summary>
    LargestRemainder,
}

/// <summary>Strength order (strongest first) and card points (indexed by <see cref="Rank"/>) of a card set.</summary>
public sealed record CardTable(ImmutableArray<Rank> Order, ImmutableArray<int> Points)
{
    /// <summary>Strength of a rank: 8 for the strongest ... 1 for the weakest.</summary>
    public int Strength(Rank rank) => Order.Length - Order.IndexOf(rank);

    public int PointsOf(Rank rank) => Points[(int)rank];

    public int Total => Points.Sum();
}

public sealed record DealRules(ImmutableArray<int> Pattern, int BiddingAfterPacket);

public sealed record BiddingRules(
    ImmutableArray<ContractKind> Ranking,
    bool Contra,
    bool Recontra,
    int CloseAfterConsecutivePasses,
    bool CloseOnRecontra,
    AllPassPolicy AllPass);

public sealed record SuitContractRules(CardTable Trump, CardTable Plain, int LastTrickBonus);

public sealed record TableContractRules(CardTable Table, int LastTrickBonus);

public sealed record ContractRules(SuitContractRules Suit, TableContractRules NoTrump, TableContractRules AllTrump);

public sealed record PlayRules(
    bool MustFollowSuit,
    bool CutOnlyWhenOpponentWinning,
    bool MustOvertrump,
    bool MustOvertakeInAllTrump,
    bool AnyCardWhenCannotOvertrump);

public sealed record SequenceValue(int MinLength, int Points);

public sealed record AnnouncementRules(
    bool AllowedInSuit,
    bool AllowedInNoTrump,
    bool AllowedInAllTrump,
    ImmutableArray<SequenceValue> Sequences,
    ImmutableArray<int> CarrePoints,
    ImmutableArray<Rank> CarreOrder,
    int BelotePoints,
    bool BeloteInSuit,
    bool BeloteInNoTrump,
    bool BeloteInAllTrump,
    AnnouncementComparison Comparison,
    int RevealAfterTrick)
{
    public bool AllowedIn(ContractFamily family) => family switch
    {
        ContractFamily.Suit => AllowedInSuit,
        ContractFamily.NoTrump => AllowedInNoTrump,
        _ => AllowedInAllTrump,
    };

    public bool BeloteIn(ContractFamily family) => family switch
    {
        ContractFamily.Suit => BeloteInSuit,
        ContractFamily.NoTrump => BeloteInNoTrump,
        _ => BeloteInAllTrump,
    };
}

public sealed record ScoringRules(
    int ContraMultiplier,
    int RecontraMultiplier,
    bool WinnerTakesAllUnderMultiplier,
    int AllTricksBonus,
    int GamePointDivisor,
    RoundingMode Rounding,
    bool RoundingTieFavorsFewerPoints);

public sealed record MatchRules(int TargetScore, bool AllTricksCannotEndMatch);

/// <summary>
/// The single place where variant rules live. Immutable; passed explicitly to every engine entry point.
/// </summary>
public sealed record RuleSet(
    string Name,
    int Version,
    DealRules Deal,
    BiddingRules Bidding,
    ContractRules Contracts,
    PlayRules Play,
    AnnouncementRules Announcements,
    ScoringRules Scoring,
    MatchRules Match)
{
    /// <summary>Card points plus last-trick bonus in a hand of the given contract, before announcements.</summary>
    public int PointsInPlay(ContractKind kind) => kind.Family() switch
    {
        ContractFamily.Suit => Contracts.Suit.Trump.Total + 3 * Contracts.Suit.Plain.Total + Contracts.Suit.LastTrickBonus,
        ContractFamily.NoTrump => 4 * Contracts.NoTrump.Table.Total + Contracts.NoTrump.LastTrickBonus,
        _ => 4 * Contracts.AllTrump.Table.Total + Contracts.AllTrump.LastTrickBonus,
    };

    public int LastTrickBonus(ContractKind kind) => kind.Family() switch
    {
        ContractFamily.Suit => Contracts.Suit.LastTrickBonus,
        ContractFamily.NoTrump => Contracts.NoTrump.LastTrickBonus,
        _ => Contracts.AllTrump.LastTrickBonus,
    };

    /// <summary>Card points of a card in a hand of the given contract.</summary>
    public int CardPoints(ContractKind kind, Card card) => kind.Family() switch
    {
        ContractFamily.Suit => card.Suit == kind.TrumpSuit()!.Value
            ? Contracts.Suit.Trump.PointsOf(card.Rank)
            : Contracts.Suit.Plain.PointsOf(card.Rank),
        ContractFamily.NoTrump => Contracts.NoTrump.Table.PointsOf(card.Rank),
        _ => Contracts.AllTrump.Table.PointsOf(card.Rank),
    };
}

public static class ContractKindExtensions
{
    public static ContractFamily Family(this ContractKind kind) => kind switch
    {
        ContractKind.NoTrump => ContractFamily.NoTrump,
        ContractKind.AllTrump => ContractFamily.AllTrump,
        _ => ContractFamily.Suit,
    };

    /// <summary>The trump suit for a suit contract, otherwise null.</summary>
    public static Suit? TrumpSuit(this ContractKind kind) => kind.Family() == ContractFamily.Suit
        ? (Suit)(int)kind
        : null;

    public static ContractKind ForSuit(Suit suit) => (ContractKind)(int)suit;

    /// <summary>Position of a bid in the rule set's bid ranking (higher is a stronger bid).</summary>
    public static int BidRank(this RuleSet rules, ContractKind kind) =>
        rules.Bidding.Ranking.IndexOf(kind);
}
