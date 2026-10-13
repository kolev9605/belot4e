using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Belote.Core;

namespace Belote.Bots;

/// <summary>
/// A calculated (non-random) seat controller: it evaluates its hand to bid, tracks the cards played
/// from the public trick history, and plays with a small priority list of Belote principles
/// (see specs/004-smart-bot-ai/research.md).
/// <para>
/// It sees only its <see cref="SeatView"/>, keeps no state between calls (so an equal view always
/// yields an equal action), uses no randomness, and only ever returns an element of
/// <see cref="LegalActions.For"/>.
/// </para>
/// </summary>
public sealed class SmartBot : IPlayer
{
    private readonly SmartBotOptions _options;

    public SmartBot(SmartBotOptions? options = null)
    {
        _options = options ?? SmartBotOptions.Standard;
        _options.Validate();
    }

    public ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken)
    {
        var legal = LegalActions.For(view);
        if (legal.Count == 0)
        {
            throw new InvalidOperationException($"{view.Seat} was asked to act but has no legal action.");
        }

        PlayerAction choice = legal.Count == 1 ? legal[0] : Choose(view, legal);

        // A heuristic can never produce an illegal move: fall back to the first legal action.
        if (LegalActions.Check(view, choice) is not null)
        {
            choice = legal[0];
        }

        return new ValueTask<PlayerAction>(choice);
    }

    private PlayerAction Choose(SeatView view, IReadOnlyList<PlayerAction> legal)
    {
        if (view.Phase == Phase.Bidding)
        {
            return DecideBid(view, legal);
        }

        var announcement = DecideAnnouncement(view, legal);
        if (announcement is not null)
        {
            return announcement;
        }

        var cards = legal.OfType<PlayCardAction>().Select(a => a.Card).ToList();
        if (cards.Count == 0)
        {
            return legal[0];
        }

        var memory = CardMemory.From(view);
        var trick = TrickSituation.From(view, memory);
        var card = trick.IsLeading
            ? ChooseLead(view, memory, cards)
            : ChooseFollow(view, memory, trick, cards);
        return new PlayCardAction(card);
    }

    // ---------------------------------------------------------------- bidding

    private PlayerAction DecideBid(SeatView view, IReadOnlyList<PlayerAction> legal)
    {
        var rules = view.RuleSet;
        var evaluation = HandEvaluator.Evaluate(rules, view.OwnHand, _options);
        var pass = legal.OfType<PassAction>().FirstOrDefault() ?? (PlayerAction)legal[0];

        if (legal.OfType<RecontraAction>().Any())
        {
            var standing = view.StandingBid!.Value;
            return evaluation.ScoreOf(standing) >= _options.RecontraThreshold
                ? legal.OfType<RecontraAction>().First()
                : pass;
        }

        if (legal.OfType<ContraAction>().Any() && view.StandingBid is { } opposing
            && evaluation.ScoreOf(opposing) >= _options.ContraThreshold)
        {
            return legal.OfType<ContraAction>().First();
        }

        double threshold = _options.BidThreshold;
        if (view.StandingBidder is { } bidder && bidder == view.Seat.Partner())
        {
            threshold += _options.OvercallPartnerMargin;
        }

        BidAction? best = null;
        double bestScore = double.MinValue;
        foreach (var bid in legal.OfType<BidAction>().OrderBy(b => rules.BidRank(b.Kind)))
        {
            double score = evaluation.ScoreOf(bid.Kind);
            if (score > bestScore)
            {
                bestScore = score;
                best = bid;
            }
        }

        return best is not null && bestScore >= threshold ? best : pass;
    }

    // ---------------------------------------------------------- announcements

    private static PlayerAction? DecideAnnouncement(SeatView view, IReadOnlyList<PlayerAction> legal)
    {
        AnnounceAction? best = null;
        foreach (var candidate in legal.OfType<AnnounceAction>())
        {
            if (best is null || Compare(view.RuleSet, candidate, best) > 0)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static int Compare(RuleSet rules, AnnounceAction a, AnnounceAction b)
    {
        int byPoints = a.Spec.Points(rules.Announcements).CompareTo(b.Spec.Points(rules.Announcements));
        if (byPoints != 0)
        {
            return byPoints;
        }

        // Canonical tie-break so equal-valued announcements are chosen deterministically.
        int byLength = a.Spec.Length.CompareTo(b.Spec.Length);
        if (byLength != 0)
        {
            return byLength;
        }

        int byRank = a.Spec.Rank.CompareTo(b.Spec.Rank);
        return byRank != 0 ? byRank : a.Spec.Suit.CompareTo(b.Spec.Suit);
    }

    // ------------------------------------------------------------------ play

    private Card ChooseFollow(SeatView view, CardMemory memory, TrickSituation trick, List<Card> cards)
    {
        var rules = view.RuleSet;
        var kind = view.Contract!.Kind;
        var led = trick.LedSuit!.Value;

        if (trick.PartnerWinning)
        {
            if (trick.PartnerSecure)
            {
                // Feed: put the most valuable card on a trick that is safely ours, sparing trumps
                // when the trick was not won with one.
                var pool = !IsTrump(kind, trick.CurrentBest!.Value) && cards.Any(c => !IsTrump(kind, c))
                    ? cards.Where(c => !IsTrump(kind, c)).ToList()
                    : cards;
                return pool
                    .OrderByDescending(c => rules.CardPoints(kind, c))
                    .ThenBy(c => Strength(rules, kind, c))
                    .ThenBy(c => c)
                    .First();
            }

            return Cheapest(view, cards);
        }

        int bestStrength = CardStrength.Of(rules, kind, led, trick.CurrentBest!.Value);
        var winners = cards
            .Where(c => CardStrength.Of(rules, kind, led, c) > bestStrength)
            .ToList();

        if (winners.Count > 0)
        {
            var cheapest = winners
                .OrderBy(c => CardStrength.Of(rules, kind, led, c))
                .ThenBy(c => rules.CardPoints(kind, c))
                .ThenBy(c => c)
                .First();

            // Do not spend a trump to take a worthless trick when another card is allowed.
            bool cutsEmptyTrick = IsTrump(kind, cheapest) && !IsTrump(kind, trick.CurrentBest.Value)
                && trick.PointsOnTable == 0 && trick.PositionFromLast > 0
                && cards.Any(c => !IsTrump(kind, c));
            if (!cutsEmptyTrick)
            {
                return cheapest;
            }
        }

        return Cheapest(view, cards);
    }

    private Card ChooseLead(SeatView view, CardMemory memory, List<Card> cards)
    {
        var rules = view.RuleSet;
        var contract = view.Contract!;
        var kind = contract.Kind;
        var trumpSuit = kind.TrumpSuit();
        bool declaring = contract.DeclaringTeam == view.Seat.GetTeam();

        // Declaring a suit contract: draw the opponents' trumps with a master trump.
        if (trumpSuit is { } trump && declaring && !memory.TrumpsRemaining.IsEmpty)
        {
            var masterTrumps = cards.Where(c => c.Suit == trump && memory.IsMaster(c)).ToList();
            if (masterTrumps.Count > 0)
            {
                return masterTrumps.OrderByDescending(c => Strength(rules, kind, c)).ThenBy(c => c).First();
            }
        }

        bool IsPlain(Card c) => trumpSuit is null || c.Suit != trumpSuit.Value;

        // Cash a master that cannot be trumped.
        var cash = cards.Where(c => IsPlain(c) && memory.IsSafeMaster(c))
            .OrderByDescending(c => rules.CardPoints(kind, c))
            .ThenBy(c => c)
            .ToList();
        if (cash.Count > 0)
        {
            return cash[0];
        }

        var pool = cards.Where(IsPlain).ToList();
        if (pool.Count == 0)
        {
            pool = cards;
        }

        var safe = pool.Where(c => memory.IsMaster(c) || rules.CardPoints(kind, c) < _options.RiskyLeadPoints).ToList();
        if (safe.Count == 0)
        {
            return Cheapest(view, pool);
        }

        // Lead the lowest card of the longest suit, avoiding suits an opponent is known to trump.
        var opponents = new[] { view.Seat.After(1), view.Seat.After(3) };
        bool trumpsOut = !memory.TrumpsRemaining.IsEmpty;
        int SuitValue(Suit suit)
        {
            int length = view.OwnHand.Count(c => c.Suit == suit);
            bool cuttable = trumpsOut && opponents.Any(o => memory.IsVoid(o, suit));
            return length - (cuttable ? 10 : 0);
        }

        var suit = safe.Select(c => c.Suit).Distinct()
            .OrderByDescending(SuitValue)
            .ThenBy(s => s)
            .First();
        return Cheapest(view, safe.Where(c => c.Suit == suit).ToList());
    }

    /// <summary>The least valuable card: fewest points, then weakest, keeping a belote pair together.</summary>
    private static Card Cheapest(SeatView view, List<Card> cards)
    {
        var rules = view.RuleSet;
        var kind = view.Contract!.Kind;
        var trump = kind.TrumpSuit();
        bool BreaksPair(Card c) => trump is { } t && c.Suit == t
            && (c.Rank == Rank.King || c.Rank == Rank.Queen)
            && view.OwnHand.Contains(new Card(t, c.Rank == Rank.King ? Rank.Queen : Rank.King));

        return cards
            .OrderBy(c => BreaksPair(c) ? 1 : 0)
            .ThenBy(c => rules.CardPoints(kind, c))
            .ThenBy(c => Strength(rules, kind, c))
            .ThenBy(c => c)
            .First();
    }

    private static int Strength(RuleSet rules, ContractKind kind, Card card) =>
        CardStrength.Of(rules, kind, card.Suit, card);

    private static bool IsTrump(ContractKind kind, Card card) => kind.TrumpSuit() is { } t && card.Suit == t;
}
