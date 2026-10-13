using System;
using System.Collections.Generic;
using System.Linq;
using Belote.Core;
using Belote.Simulation;

namespace Belote.Table;

/// <summary>Projects a <see cref="SeatView"/> (already free of hidden information) onto the wire.</summary>
public static class SeatViewMapper
{
    public static TableUpdateDto ToUpdate(
        int version, SeatView view, CompletedTrick? lastCompleted, HandSummary? lastHand = null) =>
        new(
            TableUpdateDto.CurrentProtocolVersion,
            version,
            view.Phase == Phase.Finished,
            Map(view, lastCompleted, lastHand));

    public static SeatViewDto Map(SeatView view, CompletedTrick? lastCompleted, HandSummary? lastHand = null)
    {
        var legal = LegalActions.For(view).Select(a => LegalActionMapper.ToDto(view.Seat, a)).ToArray();
        return new SeatViewDto(
            Seat: JsonNames.Of(view.Seat),
            Phase: PhaseName(view.Phase),
            Dealer: JsonNames.Of(view.Dealer),
            ToAct: JsonNames.Of(view.ToAct),
            DealNumber: view.DealNumber,
            OwnHand: OrderHand(view.RuleSet, view.Contract?.Kind, view.OwnHand).Select(c => c.ToString()).ToArray(),
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
            LegalActions: legal,
            LastHand: lastHand is null ? null : Summary(lastHand));
    }

    /// <summary>
    /// Orders a hand grouped by suit and strongest-first within each suit for the contract in force
    /// (no-trump order while none is decided). Card power comes from the rule set, never from the client.
    /// </summary>
    public static IReadOnlyList<Card> OrderHand(RuleSet rules, ContractKind? contract, IEnumerable<Card> hand)
    {
        var kind = contract ?? ContractKind.NoTrump;
        return hand
            .OrderBy(c => c.Suit)
            .ThenByDescending(c => CardStrength.Of(rules, kind, c.Suit, c))
            .ThenByDescending(c => c.Rank)
            .ToArray();
    }

    private static HandSummaryDto Summary(HandSummary s)
    {
        var r = s.Result;
        TeamHandDto TeamHand(Team team) => new(
            s.WonCards[(int)team].Select(c => c.ToString()).ToArray(),
            r.CardPoints[(int)team],
            r.LastTrickTeam == team ? r.LastTrickBonus : 0,
            r.AnnouncementPoints[(int)team],
            r.BelotePoints[(int)team],
            r.AllTricksTeam == team ? r.AllTricksBonus : 0,
            r.RawTotals[(int)team],
            r.GamePoints[(int)team]);

        return new HandSummaryDto(
            s.DealNumber,
            new ContractDto(JsonNames.Of(s.Contract.Kind), JsonNames.Of(s.Contract.Declarer), s.Contract.Multiplier),
            JsonNames.Of(r.DeclaringTeam),
            r.Outcome switch
            {
                HandOutcome.Made => "made",
                HandOutcome.Failed => "failed",
                _ => "tied",
            },
            new TeamHandsDto(TeamHand(Team.NorthSouth), TeamHand(Team.EastWest)),
            new ScoreDto(s.MatchScore[0], s.MatchScore[1]),
            new HeldOverDto(
                r.HeldOverBefore,
                r.AwardedFromHeldOver,
                r.HeldOverAwardedTo is { } to ? JsonNames.Of(to) : null,
                r.NewlyHeld,
                r.HeldOverAfter),
            s.Winner is { } w ? JsonNames.Of(w) : null);
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
