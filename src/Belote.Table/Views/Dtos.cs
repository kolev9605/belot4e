using System.Collections.Generic;

namespace Belote.Table;

public sealed record PlayedCardDto(string Seat, string Card);

public sealed record CompletedTrickDto(
    int DealNumber,
    int TrickNumber,
    string Leader,
    IReadOnlyList<PlayedCardDto> Plays,
    string Winner);

public sealed record BidEntryDto(string Seat, string Action, string? Contract);

public sealed record ContractDto(string Kind, string Declarer, int Multiplier);

public sealed record AnnouncementSpecDto(string Kind, string? Suit, string Rank, int Length);

public sealed record AnnouncementDto(
    string Seat,
    string Kind,
    int Length,
    AnnouncementSpecDto? Revealed,
    int? Points);

public sealed record BeloteDto(string Seat, string Suit);

public sealed record ScoreDto(int NorthSouth, int EastWest);

/// <summary>
/// One intent on the wire. <c>kind</c> is one of bid, pass, contra, recontra, announce, play;
/// legal actions in a view use exactly this shape so a client can echo one back unchanged.
/// </summary>
public sealed record IntentDto(
    string? Kind,
    string? Seat,
    string? Contract = null,
    AnnouncementSpecDto? Announcement = null,
    string? Card = null);

public sealed record SeatViewDto(
    string Seat,
    string Phase,
    string Dealer,
    string ToAct,
    int DealNumber,
    IReadOnlyList<string> OwnHand,
    IReadOnlyList<BidEntryDto> BiddingHistory,
    string? StandingBid,
    string? StandingBidder,
    bool Contra,
    bool Recontra,
    ContractDto? Contract,
    string Leader,
    IReadOnlyList<PlayedCardDto> CurrentTrick,
    CompletedTrickDto? LastCompletedTrick,
    IReadOnlyList<CompletedTrickDto> CompletedTricks,
    IReadOnlyList<AnnouncementDto> Announcements,
    IReadOnlyList<AnnouncementSpecDto> OwnDeclared,
    IReadOnlyList<BeloteDto> Belotes,
    ScoreDto MatchScore,
    int HeldOverPoints,
    string? Winner,
    IReadOnlyList<IntentDto> LegalActions);

public sealed record TableUpdateDto(int ProtocolVersion, int Version, bool Finished, SeatViewDto View)
{
    public const int CurrentProtocolVersion = 1;
}

public sealed record RejectionDto(string Category, string Code, string Detail);

public sealed record IntentResultDto(bool Accepted, int? Version, RejectionDto? Rejection);

public sealed record CreateTableRequest(ulong? Seed = null, int? BotDelayMs = null);

public sealed record CreateTableResultDto(bool Ok, string? TableId, RejectionDto? Rejection);

public sealed record TakeSeatRequest(string? Seat);

public sealed record TakeSeatResultDto(bool Ok, int? Version, RejectionDto? Rejection);

public sealed record GetViewResultDto(TableUpdateDto? Update, RejectionDto? Rejection);
