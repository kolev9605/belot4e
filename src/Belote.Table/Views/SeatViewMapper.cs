using System;
using System.Linq;
using Belote.Core;
using Belote.Simulation;

namespace Belote.Table;

/// <summary>Projects a <see cref="SeatView"/> (already free of hidden information) onto the wire.</summary>
public static class SeatViewMapper
{
    public static TableUpdateDto ToUpdate(int version, SeatView view, CompletedTrick? lastCompleted) =>
        new(
            TableUpdateDto.CurrentProtocolVersion,
            version,
            view.Phase == Phase.Finished,
            Map(view, lastCompleted));

    public static SeatViewDto Map(SeatView view, CompletedTrick? lastCompleted)
    {
        var legal = LegalActions.For(view).Select(a => LegalActionMapper.ToDto(view.Seat, a)).ToArray();
        return new SeatViewDto(
            Seat: JsonNames.Of(view.Seat),
            Phase: PhaseName(view.Phase),
            Dealer: JsonNames.Of(view.Dealer),
            ToAct: JsonNames.Of(view.ToAct),
            DealNumber: view.DealNumber,
            OwnHand: view.OwnHand.Select(c => c.ToString()).ToArray(),
            HandSizes: Sizes(view),
            BiddingHistory: view.BiddingHistory.Select(Bid).ToArray(),
            StandingBid: view.StandingBid is { } sb ? JsonNames.Of(sb) : null,
            StandingBidder: view.StandingBidder is { } who ? JsonNames.Of(who) : null,
            Contra: view.Contra,
            Recontra: view.Recontra,
            Contract: view.Contract is { } c
                ? new ContractDto(JsonNames.Of(c.Kind), JsonNames.Of(c.Declarer), c.Multiplier)
                : null,
            Leader: JsonNames.Of(view.Leader),
            CurrentTrick: view.CurrentTrick.Select(Played).ToArray(),
            LastCompletedTrick: lastCompleted is null ? null : Trick(lastCompleted),
            CompletedTricks: view.CompletedTricks
                .Select((t, i) => new CompletedTrickDto(
                    view.DealNumber, i + 1, JsonNames.Of(t.Leader), t.Plays.Select(Played).ToArray(), JsonNames.Of(t.Winner)))
                .ToArray(),
            Announcements: view.Announcements
                .Select(a => new AnnouncementDto(
                    JsonNames.Of(a.Seat),
                    a.Kind == AnnouncementKind.Sequence ? "sequence" : "carre",
                    a.Length,
                    a.Revealed is { } spec ? Spec(spec) : null,
                    a.Points))
                .ToArray(),
            OwnDeclared: view.OwnDeclared.Select(Spec).ToArray(),
            Belotes: view.Belotes.Select(b => new BeloteDto(JsonNames.Of(b.Seat), JsonNames.Of(b.Suit))).ToArray(),
            MatchScore: new ScoreDto(view.MatchScore[0], view.MatchScore[1]),
            HeldOverPoints: view.HeldOverPoints,
            Winner: view.Winner is { } w ? JsonNames.Of(w) : null,
            LegalActions: legal);
    }

    private static HandSizesDto Sizes(SeatView view)
    {
        var s = view.HandSizes;
        return s.IsDefault
            ? new HandSizesDto(0, 0, 0, 0)
            : new HandSizesDto(s[(int)Seat.North], s[(int)Seat.East], s[(int)Seat.South], s[(int)Seat.West]);
    }

    public static AnnouncementSpecDto Spec(AnnouncementSpec spec) =>
        spec.Kind == AnnouncementKind.Sequence
            ? new AnnouncementSpecDto("sequence", JsonNames.Of(spec.Suit), Card.RankText(spec.Rank), spec.Length)
            : new AnnouncementSpecDto("carre", null, Card.RankText(spec.Rank), spec.Length);

    private static string PhaseName(Phase phase) => phase switch
    {
        Phase.Bidding => "bidding",
        Phase.Playing => "playing",
        _ => "finished",
    };

    private static PlayedCardDto Played(PlayedCard p) => new(JsonNames.Of(p.Seat), p.Card.ToString());

    private static CompletedTrickDto Trick(CompletedTrick t) =>
        new(t.DealNumber, t.TrickNumber, JsonNames.Of(t.Leader), t.Plays.Select(Played).ToArray(), JsonNames.Of(t.Winner));

    private static BidEntryDto Bid(BidEntry entry) => entry.Action switch
    {
        PassAction => new BidEntryDto(JsonNames.Of(entry.Seat), "pass", null),
        BidAction b => new BidEntryDto(JsonNames.Of(entry.Seat), "bid", JsonNames.Of(b.Kind)),
        ContraAction => new BidEntryDto(JsonNames.Of(entry.Seat), "contra", null),
        RecontraAction => new BidEntryDto(JsonNames.Of(entry.Seat), "recontra", null),
        _ => new BidEntryDto(JsonNames.Of(entry.Seat), entry.Action.GetType().Name, null),
    };
}

/// <summary>Converts a <see cref="PlayerAction"/> into the wire intent shape.</summary>
public static class LegalActionMapper
{
    public static IntentDto ToDto(Seat seat, PlayerAction action)
    {
        string name = JsonNames.Of(seat);
        return action switch
        {
            PassAction => new IntentDto("pass", name),
            BidAction b => new IntentDto("bid", name, Contract: JsonNames.Of(b.Kind)),
            ContraAction => new IntentDto("contra", name),
            RecontraAction => new IntentDto("recontra", name),
            AnnounceAction a => new IntentDto("announce", name, Announcement: SeatViewMapper.Spec(a.Spec)),
            PlayCardAction p => new IntentDto("play", name, Card: p.Card.ToString()),
            _ => throw new ArgumentException($"Unsupported action {action.GetType().Name}.", nameof(action)),
        };
    }
}
