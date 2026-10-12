using System.Collections.Immutable;

namespace Belote.Core;

/// <summary>Built-in rule sets. This is the only place Bulgarian rule values are written down.</summary>
public static class RuleSets
{
    // Order is strongest first; points are indexed by Rank (7, 8, 9, 10, J, Q, K, A).
    private static readonly ImmutableArray<Rank> TrumpOrder = ImmutableArray.Create(
        Rank.Jack, Rank.Nine, Rank.Ace, Rank.Ten, Rank.King, Rank.Queen, Rank.Eight, Rank.Seven);

    private static readonly ImmutableArray<Rank> PlainOrder = ImmutableArray.Create(
        Rank.Ace, Rank.Ten, Rank.King, Rank.Queen, Rank.Jack, Rank.Nine, Rank.Eight, Rank.Seven);

    /// <summary>
    /// Bulgarian belote, following the rules published at belot.bg (see research.md for the
    /// verification table). Bidding happens after 5 cards; all-trump totals 258, no-trump 260,
    /// suit contracts 162.
    /// </summary>
    public static RuleSet Bulgarian { get; } = BuildBulgarian();

    private static RuleSet BuildBulgarian()
    {
        var trump = new CardTable(TrumpOrder, ImmutableArray.Create(0, 0, 14, 10, 20, 3, 4, 11));
        var plain = new CardTable(PlainOrder, ImmutableArray.Create(0, 0, 0, 10, 2, 3, 4, 11));
        var noTrump = new CardTable(PlainOrder, ImmutableArray.Create(0, 0, 0, 20, 4, 6, 8, 22));
        var allTrump = new CardTable(TrumpOrder, ImmutableArray.Create(0, 0, 14, 10, 20, 3, 4, 11));

        return new RuleSet(
            Name: "bulgarian",
            Version: 1,
            Deal: new DealRules(ImmutableArray.Create(3, 2, 3), BiddingAfterPacket: 2),
            Bidding: new BiddingRules(
                Ranking: ImmutableArray.Create(
                    ContractKind.Clubs, ContractKind.Diamonds, ContractKind.Hearts, ContractKind.Spades,
                    ContractKind.NoTrump, ContractKind.AllTrump),
                Contra: true,
                Recontra: true,
                CloseAfterConsecutivePasses: 3,
                CloseOnRecontra: true,
                AllPass: AllPassPolicy.VoidAndRedealNextDealer),
            Contracts: new ContractRules(
                Suit: new SuitContractRules(trump, plain, LastTrickBonus: 10),
                NoTrump: new TableContractRules(noTrump, LastTrickBonus: 20),
                AllTrump: new TableContractRules(allTrump, LastTrickBonus: 10)),
            Play: new PlayRules(
                MustFollowSuit: true,
                CutOnlyWhenOpponentWinning: true,
                MustOvertrump: true,
                MustOvertakeInAllTrump: true,
                AnyCardWhenCannotOvertrump: true),
            Announcements: new AnnouncementRules(
                AllowedInSuit: true,
                AllowedInNoTrump: false,
                AllowedInAllTrump: true,
                Sequences: ImmutableArray.Create(
                    new SequenceValue(3, 20), new SequenceValue(4, 50), new SequenceValue(5, 100)),
                // Indexed by Rank: 7, 8 have no four-of-a-kind value.
                CarrePoints: ImmutableArray.Create(0, 0, 150, 100, 200, 100, 100, 100),
                CarreOrder: ImmutableArray.Create(
                    Rank.Jack, Rank.Nine, Rank.Ace, Rank.Ten, Rank.King, Rank.Queen),
                BelotePoints: 20,
                BeloteInSuit: true,
                BeloteInNoTrump: false,
                BeloteInAllTrump: true,
                Comparison: AnnouncementComparison.PerCategory,
                RevealAfterTrick: 1),
            Scoring: new ScoringRules(
                ContraMultiplier: 2,
                RecontraMultiplier: 4,
                WinnerTakesAllUnderMultiplier: true,
                AllTricksBonus: 90,
                GamePointDivisor: 10,
                Rounding: RoundingMode.LargestRemainder,
                RoundingTieFavorsFewerPoints: true),
            Match: new MatchRules(TargetScore: 151, AllTricksCannotEndMatch: true));
    }
}
