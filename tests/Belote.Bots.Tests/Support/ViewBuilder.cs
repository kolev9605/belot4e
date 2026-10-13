using System.Collections.Immutable;
using Belote.Core;

namespace Belote.Bots.Tests;

/// <summary>Builds crafted <see cref="SeatView"/>s from short card strings such as "JH 9H AS".</summary>
internal static class V
{
    public static ImmutableArray<Card> Hand(string cards) =>
        cards.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Card.Parse).ToImmutableArray();

    public static ImmutableArray<PlayedCard> Plays(params (Seat Seat, string Card)[] plays) =>
        plays.Select(p => new PlayedCard(p.Seat, Card.Parse(p.Card))).ToImmutableArray();

    /// <summary>A completed trick whose winner is resolved by the rules.</summary>
    public static Trick Trick(RuleSet rules, ContractKind kind, params (Seat Seat, string Card)[] plays)
    {
        var played = Plays(plays);
        return new Trick(played[0].Seat, played, TrickResolver.Winner(rules, kind, played));
    }

    public static SeatView Bidding(
        Seat seat,
        string hand,
        ContractKind? standing = null,
        Seat? bidder = null,
        bool contra = false,
        bool recontra = false,
        RuleSet? rules = null)
    {
        var history = ImmutableArray<BidEntry>.Empty;
        if (standing is { } kind && bidder is { } who)
        {
            history = history.Add(new BidEntry(who, new BidAction(kind)));
        }

        return new SeatView(
            rules ?? RuleSets.Bulgarian, seat, Phase.Bidding, Dealer: Seat.North, ToAct: seat, DealNumber: 1,
            OwnHand: Hand(hand), BiddingHistory: history, StandingBid: standing, StandingBidder: bidder,
            Contra: contra, Recontra: recontra, Contract: null, Leader: Seat.East,
            CurrentTrick: ImmutableArray<PlayedCard>.Empty, CompletedTricks: ImmutableArray<Trick>.Empty,
            Announcements: ImmutableArray<PublicAnnouncement>.Empty, OwnDeclared: ImmutableArray<AnnouncementSpec>.Empty,
            Belotes: ImmutableArray<BeloteAward>.Empty, MatchScore: ImmutableArray.Create(0, 0),
            HeldOverPoints: 0, Winner: null);
    }

    public static SeatView Playing(
        Seat seat,
        ContractKind kind,
        Seat declarer,
        string hand,
        (Seat Seat, string Card)[]? trick = null,
        ImmutableArray<Trick>? completed = null,
        RuleSet? rules = null,
        ImmutableArray<AnnouncementSpec>? declared = null)
    {
        var current = trick is null ? ImmutableArray<PlayedCard>.Empty : Plays(trick);
        return new SeatView(
            rules ?? RuleSets.Bulgarian, seat, Phase.Playing, Dealer: Seat.North, ToAct: seat, DealNumber: 1,
            OwnHand: Hand(hand), BiddingHistory: ImmutableArray<BidEntry>.Empty, StandingBid: kind,
            StandingBidder: declarer, Contra: false, Recontra: false, Contract: new Contract(kind, declarer, 1),
            Leader: current.IsEmpty ? seat : current[0].Seat, CurrentTrick: current,
            CompletedTricks: completed ?? ImmutableArray<Trick>.Empty,
            Announcements: ImmutableArray<PublicAnnouncement>.Empty,
            OwnDeclared: declared ?? ImmutableArray<AnnouncementSpec>.Empty,
            Belotes: ImmutableArray<BeloteAward>.Empty, MatchScore: ImmutableArray.Create(0, 0),
            HeldOverPoints: 0, Winner: null);
    }

    public static PlayerAction Decide(SeatView view) =>
        new SmartBot().DecideAsync(view, default).AsTask().Result;
}
