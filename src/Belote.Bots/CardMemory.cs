using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Belote.Core;

namespace Belote.Bots;

/// <summary>
/// What a seat can know about the cards of the hand in progress, rebuilt from its own
/// <see cref="SeatView"/> on every decision: nothing here is remembered between calls.
/// </summary>
internal sealed class CardMemory
{
    private readonly RuleSet _rules;
    private readonly ContractKind _kind;

    private CardMemory(
        RuleSet rules,
        ContractKind kind,
        Seat seat,
        ImmutableHashSet<Card> played,
        ImmutableArray<Card> unseen,
        IReadOnlyDictionary<Seat, ImmutableHashSet<Suit>> voids)
    {
        _rules = rules;
        _kind = kind;
        Seat = seat;
        Played = played;
        Unseen = unseen;
        KnownVoids = voids;
    }

    public Seat Seat { get; }

    /// <summary>Cards seen in completed tricks and in the current trick.</summary>
    public ImmutableHashSet<Card> Played { get; }

    /// <summary>The deck minus the own hand minus every played card (held by the other three seats).</summary>
    public ImmutableArray<Card> Unseen { get; }

    /// <summary>Suits a seat has shown it cannot follow.</summary>
    public IReadOnlyDictionary<Seat, ImmutableHashSet<Suit>> KnownVoids { get; }

    public ContractKind Kind => _kind;

    public int RemainingInSuit(Suit suit) => Unseen.Count(c => c.Suit == suit);

    /// <summary>Unseen trump cards; empty unless the contract has a trump suit.</summary>
    public ImmutableArray<Card> TrumpsRemaining
    {
        get
        {
            var trump = _kind.TrumpSuit();
            return trump is null
                ? ImmutableArray<Card>.Empty
                : Unseen.Where(c => c.Suit == trump.Value).ToImmutableArray();
        }
    }

    public bool IsVoid(Seat seat, Suit suit) =>
        KnownVoids.TryGetValue(seat, out var suits) && suits.Contains(suit);

    /// <summary>
    /// True when no unseen card of the same suit is stronger under the contract's strength table.
    /// A plain card of a suit contract can still be trumped; see <see cref="IsSafeMaster"/>.
    /// </summary>
    public bool IsMaster(Card card)
    {
        int own = CardStrength.Of(_rules, _kind, card.Suit, card);
        foreach (var other in Unseen)
        {
            if (other.Suit == card.Suit && CardStrength.Of(_rules, _kind, card.Suit, other) > own)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A master that no known-void opponent can trump.</summary>
    public bool IsSafeMaster(Card card)
    {
        if (!IsMaster(card))
        {
            return false;
        }

        var trump = _kind.TrumpSuit();
        if (trump is null || card.Suit == trump.Value || TrumpsRemaining.IsEmpty)
        {
            return true;
        }

        return !IsVoid(Seat.After(1), card.Suit) && !IsVoid(Seat.After(3), card.Suit);
    }

    public static CardMemory From(SeatView view)
    {
        var contract = view.Contract!;
        var played = new HashSet<Card>();
        var voids = new Dictionary<Seat, HashSet<Suit>>();

        void Absorb(ImmutableArray<PlayedCard> plays)
        {
            if (plays.IsDefaultOrEmpty)
            {
                return;
            }

            var led = plays[0].Card.Suit;
            foreach (var play in plays)
            {
                played.Add(play.Card);
                if (play.Card.Suit != led)
                {
                    if (!voids.TryGetValue(play.Seat, out var set))
                    {
                        voids[play.Seat] = set = new HashSet<Suit>();
                    }

                    set.Add(led);
                }
            }
        }

        foreach (var trick in view.CompletedTricks)
        {
            Absorb(trick.Plays);
        }

        Absorb(view.CurrentTrick);

        var own = new HashSet<Card>(view.OwnHand);
        var unseen = Deck.Standard32.Where(c => !own.Contains(c) && !played.Contains(c)).ToImmutableArray();
        return new CardMemory(
            view.RuleSet,
            contract.Kind,
            view.Seat,
            played.ToImmutableHashSet(),
            unseen,
            voids.ToDictionary(kv => kv.Key, kv => kv.Value.ToImmutableHashSet()));
    }
}
