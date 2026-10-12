using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Belote.Core;

public abstract record ApplyResult;

public sealed record Accepted(GameState State, IReadOnlyList<GameEvent> Events) : ApplyResult;

public sealed record Rejected(RejectionReason Reason, string Detail) : ApplyResult;

public sealed record GameStart(GameState State, IReadOnlyList<GameEvent> Events);

/// <summary>
/// The pure rules engine. A game is an immutable state advanced by validated seat actions; the
/// engine never knows what controls a seat. Same (rule set, seed, actions) gives the same game.
/// </summary>
public static class GameEngine
{
    public static GameStart Start(RuleSet rules, ulong seed)
    {
        RuleSetValidator.ThrowIfInvalid(rules);

        var sink = new EventSink(0, 1);
        var dealer = (Seat)new SeededRandom(Seeds.ForDealer(seed)).NextInt(4);
        sink.Add(new GameStarted(seed, rules.Name, rules.Version));

        var hand = HandDealer.Deal(rules, seed, 1, dealer);
        sink.Add(new HandDealt(hand.Dealer, hand.Hands, hand.DealtCount));

        var state = new GameState(
            rules,
            seed,
            Phase.Bidding,
            hand,
            ImmutableArray.Create(0, 0),
            HeldOverPoints: 0,
            Winner: null,
            HandsScored: 0,
            NextSequence: sink.NextSequence);
        return new GameStart(state, sink.Events);
    }

    public static ApplyResult Apply(GameState state, Seat seat, PlayerAction action)
    {
        var view = Projection.For(state, seat);
        if (LegalActions.Check(view, action) is { } reason)
        {
            return new Rejected(reason, $"{seat}: {action} rejected ({reason}).");
        }

        var sink = new EventSink(state.NextSequence, state.DealNumber);
        return state.Phase == Phase.Bidding
            ? ApplyBidding(state, seat, action, sink)
            : ApplyPlaying(state, seat, action, sink);
    }

    private static ApplyResult ApplyBidding(GameState state, Seat seat, PlayerAction action, EventSink sink)
    {
        var rules = state.RuleSet;
        var hand = state.Hand;
        var bidding = hand.Bidding with { History = hand.Bidding.History.Add(new BidEntry(seat, action)) };

        switch (action)
        {
            case PassAction:
                bidding = bidding with { PassStreak = bidding.PassStreak + 1 };
                sink.Add(new PassMade(seat));
                break;
            case BidAction bid:
                bidding = bidding with { Standing = bid.Kind, Bidder = seat, PassStreak = 0 };
                sink.Add(new BidMade(seat, bid.Kind));
                break;
            case ContraAction:
                bidding = bidding with { Contra = true, PassStreak = 0 };
                sink.Add(new ContraMade(seat));
                break;
            case RecontraAction:
                bidding = bidding with { Recontra = true, PassStreak = 0 };
                sink.Add(new RecontraMade(seat));
                break;
        }

        if (bidding.Standing is null && bidding.PassStreak >= 4)
        {
            return VoidAndRedeal(state, sink);
        }

        bool closed = (bidding.Recontra && rules.Bidding.CloseOnRecontra)
            || (bidding.Standing is not null && bidding.PassStreak >= rules.Bidding.CloseAfterConsecutivePasses);

        if (!closed)
        {
            hand = hand with { Bidding = bidding, ToAct = seat.Next() };
            return new Accepted(state with { Hand = hand, NextSequence = sink.NextSequence }, sink.Events);
        }

        int multiplier = bidding.Recontra
            ? rules.Scoring.RecontraMultiplier
            : bidding.Contra ? rules.Scoring.ContraMultiplier : 1;
        var contract = new Contract(bidding.Standing!.Value, bidding.Bidder!.Value, multiplier);
        sink.Add(new BiddingClosed(contract.Kind, contract.Declarer, contract.Multiplier));

        var first = hand.Dealer.Next();
        hand = hand with
        {
            Bidding = bidding,
            Contract = contract,
            DealtCount = rules.Deal.Pattern.Sum(),
            Leader = first,
            ToAct = first,
        };
        return new Accepted(
            state with { Phase = Phase.Playing, Hand = hand, NextSequence = sink.NextSequence },
            sink.Events);
    }

    private static ApplyResult VoidAndRedeal(GameState state, EventSink sink)
    {
        var nextDealer = state.Hand.Dealer.Next();
        sink.Add(new HandVoided(nextDealer));

        int dealNumber = state.DealNumber + 1;
        sink.SetDeal(dealNumber);
        var hand = HandDealer.Deal(state.RuleSet, state.Seed, dealNumber, nextDealer);
        sink.Add(new HandDealt(hand.Dealer, hand.Hands, hand.DealtCount));
        return new Accepted(
            state with { Phase = Phase.Bidding, Hand = hand, NextSequence = sink.NextSequence },
            sink.Events);
    }

    private static ApplyResult ApplyPlaying(GameState state, Seat seat, PlayerAction action, EventSink sink)
    {
        var rules = state.RuleSet;
        var hand = state.Hand;
        var contract = hand.Contract!;

        if (action is AnnounceAction announce)
        {
            int points = announce.Spec.Points(rules.Announcements);
            hand = hand with { Declared = hand.Declared.Add(new DeclaredAnnouncement(seat, announce.Spec, points)) };
            sink.Add(new AnnouncementDeclared(seat, announce.Spec, points));
            return new Accepted(state with { Hand = hand, NextSequence = sink.NextSequence }, sink.Events);
        }

        var card = ((PlayCardAction)action).Card;
        var hands = hand.Hands.SetItem((int)seat, hand.Hands[(int)seat].Remove(card));
        var plays = hand.CurrentPlays.Add(new PlayedCard(seat, card));
        int trickNumber = hand.Tricks.Length + 1;
        sink.Add(new CardPlayed(seat, card, trickNumber));

        var belotes = hand.Belotes;
        if (IsBeloteCard(rules, contract, card) && PlayedBySeat(hand, seat, OtherBeloteCard(card)))
        {
            belotes = belotes.Add(new BeloteAward(seat, card.Suit));
            sink.Add(new BeloteAwarded(seat, card.Suit, rules.Announcements.BelotePoints));
        }

        if (plays.Length < 4)
        {
            hand = hand with { Hands = hands, CurrentPlays = plays, Belotes = belotes, ToAct = seat.Next() };
            return new Accepted(state with { Hand = hand, NextSequence = sink.NextSequence }, sink.Events);
        }

        var winner = TrickResolver.Winner(rules, contract.Kind, plays);
        var trick = new Trick(hand.Leader, plays, winner);
        var tricks = hand.Tricks.Add(trick);
        int trickPoints = plays.Sum(p => rules.CardPoints(contract.Kind, p.Card));
        if (tricks.Length == 8)
        {
            trickPoints += rules.LastTrickBonus(contract.Kind);
        }

        sink.Add(new TrickWon(winner, trickNumber, trickPoints));

        var sequenceTeam = hand.SequenceTeam;
        var carreTeam = hand.CarreTeam;
        bool revealed = hand.AnnouncementsRevealed;
        if (!revealed && tricks.Length >= rules.Announcements.RevealAfterTrick)
        {
            revealed = true;
            if (!hand.Declared.IsEmpty)
            {
                sink.Add(new AnnouncementsRevealed(hand.Declared));
                var outcome = AnnouncementComparer.Compare(
                    rules.Announcements,
                    hand.Declared.Select(d => (d.Seat, d.Spec)));
                sequenceTeam = outcome.SequenceTeam;
                carreTeam = outcome.CarreTeam;
                sink.Add(new AnnouncementsCompared(sequenceTeam, carreTeam));
            }
        }

        hand = hand with
        {
            Hands = hands,
            CurrentPlays = ImmutableArray<PlayedCard>.Empty,
            Tricks = tricks,
            Leader = winner,
            ToAct = winner,
            Belotes = belotes,
            AnnouncementsRevealed = revealed,
            SequenceTeam = sequenceTeam,
            CarreTeam = carreTeam,
        };

        if (tricks.Length < 8)
        {
            return new Accepted(state with { Hand = hand, NextSequence = sink.NextSequence }, sink.Events);
        }

        return EndHand(state with { Hand = hand }, sink);
    }

    private static ApplyResult EndHand(GameState state, EventSink sink)
    {
        var rules = state.RuleSet;
        var hand = state.Hand;
        var result = HandScorer.Score(
            rules,
            hand.Contract!,
            hand.Tricks,
            hand.Declared,
            hand.SequenceTeam,
            hand.CarreTeam,
            hand.Belotes,
            state.HeldOverPoints);

        sink.Add(new HandScored(result));
        if (result.NewlyHeld > 0)
        {
            sink.Add(new PointsHeldOver(result.NewlyHeld, result.HeldOverAfter));
        }

        if (result.AwardedFromHeldOver > 0)
        {
            sink.Add(new HeldOverAwarded(result.HeldOverAwardedTo!.Value, result.AwardedFromHeldOver));
        }

        var match = MatchScorer.Apply(rules, state.MatchScore, result);
        sink.Add(new MatchScoreUpdated(match.Score[0], match.Score[1]));

        var scored = state with
        {
            MatchScore = match.Score,
            HeldOverPoints = result.HeldOverAfter,
            HandsScored = state.HandsScored + 1,
        };

        if (match.Winner is { } winner)
        {
            sink.Add(new GameFinished(winner, match.Score[0], match.Score[1]));
            return new Accepted(
                scored with { Phase = Phase.Finished, Winner = winner, NextSequence = sink.NextSequence },
                sink.Events);
        }

        int dealNumber = hand.DealNumber + 1;
        sink.SetDeal(dealNumber);
        var next = HandDealer.Deal(rules, state.Seed, dealNumber, hand.Dealer.Next());
        sink.Add(new HandDealt(next.Dealer, next.Hands, next.DealtCount));
        return new Accepted(
            scored with { Phase = Phase.Bidding, Hand = next, NextSequence = sink.NextSequence },
            sink.Events);
    }

    private static bool IsBeloteCard(RuleSet rules, Contract contract, Card card)
    {
        if (card.Rank != Rank.King && card.Rank != Rank.Queen)
        {
            return false;
        }

        var family = contract.Family;
        if (!rules.Announcements.BeloteIn(family))
        {
            return false;
        }

        return family == ContractFamily.AllTrump || card.Suit == contract.Kind.TrumpSuit();
    }

    private static Card OtherBeloteCard(Card card) =>
        new(card.Suit, card.Rank == Rank.King ? Rank.Queen : Rank.King);

    private static bool PlayedBySeat(HandState hand, Seat seat, Card card) =>
        hand.Tricks.Any(t => t.Plays.Any(p => p.Seat == seat && p.Card == card))
        || hand.CurrentPlays.Any(p => p.Seat == seat && p.Card == card);

    private sealed class EventSink
    {
        private readonly List<GameEvent> _events = new();
        private int _sequence;
        private int _deal;

        public EventSink(int nextSequence, int deal)
        {
            _sequence = nextSequence;
            _deal = deal;
        }

        public int NextSequence => _sequence;

        public IReadOnlyList<GameEvent> Events => _events;

        public void SetDeal(int deal) => _deal = deal;

        public void Add(GameEvent gameEvent) =>
            _events.Add(gameEvent with { Sequence = _sequence++, Deal = _deal });
    }
}

public static class HandDealer
{
    /// <summary>
    /// Deals a full hand deterministically from the game seed and deal number. Cards are stored in
    /// deal order; only the first <c>DealtCount</c> are visible while bidding.
    /// </summary>
    public static HandState Deal(RuleSet rules, ulong gameSeed, int dealNumber, Seat dealer)
    {
        var rng = new SeededRandom(Seeds.ForHand(gameSeed, dealNumber));
        var deck = Deck.Standard32.ToArray();
        rng.Shuffle(deck);

        var hands = new List<Card>[4];
        for (int i = 0; i < 4; i++)
        {
            hands[i] = new List<Card>(8);
        }

        int next = 0;
        var first = dealer.Next();
        foreach (int packet in rules.Deal.Pattern)
        {
            for (int k = 0; k < 4; k++)
            {
                var seat = first.After(k);
                for (int c = 0; c < packet; c++)
                {
                    hands[(int)seat].Add(deck[next++]);
                }
            }
        }

        int dealtBeforeBidding = rules.Deal.Pattern.Take(rules.Deal.BiddingAfterPacket).Sum();
        return new HandState(
            Dealer: dealer,
            DealNumber: dealNumber,
            Hands: hands.Select(h => h.ToImmutableArray()).ToImmutableArray(),
            DealtCount: dealtBeforeBidding,
            Bidding: BiddingState.Empty,
            Contract: null,
            Tricks: ImmutableArray<Trick>.Empty,
            Leader: first,
            CurrentPlays: ImmutableArray<PlayedCard>.Empty,
            ToAct: first,
            Declared: ImmutableArray<DeclaredAnnouncement>.Empty,
            AnnouncementsRevealed: false,
            SequenceTeam: null,
            CarreTeam: null,
            Belotes: ImmutableArray<BeloteAward>.Empty);
    }
}
