using System;
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

public sealed record TeamHandDto(
    IReadOnlyList<string> WonCards,
    int CardPoints,
    int LastTrickBonus,
    int AnnouncementPoints,
    int BelotePoints,
    int AllTricksBonus,
    int RawTotal,
    int GamePoints);

public sealed record TeamHandsDto(TeamHandDto NorthSouth, TeamHandDto EastWest);

public sealed record HeldOverDto(int Before, int Awarded, string? AwardedTo, int NewlyHeld, int After);

/// <summary>The most recently scored hand; played cards and engine-computed numbers only.</summary>
public sealed record HandSummaryDto(
    int DealNumber,
    ContractDto Contract,
    string DeclaringTeam,
    string Outcome,
    TeamHandsDto Teams,
    ScoreDto MatchScore,
    HeldOverDto HeldOver,
    string? Winner);

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

/// <summary>Public per-seat card counts (never card identities).</summary>
public sealed record HandSizesDto(int North, int East, int South, int West);

/// <summary>Pushed to a connection whose table was replaced by another connection's new table.</summary>
public sealed record TableClosedDto(int ProtocolVersion, string Reason);

public sealed record SeatViewDto(
    string Seat,
    string Phase,
    string Dealer,
    string ToAct,
    int DealNumber,
    IReadOnlyList<string> OwnHand,
    HandSizesDto HandSizes,
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
    IReadOnlyList<IntentDto> LegalActions,
    HandSummaryDto? LastHand = null);

public sealed record TableUpdateDto(int ProtocolVersion, int Version, bool Finished, SeatViewDto View)
{
    public const int CurrentProtocolVersion = 2;
}

public sealed record RejectionDto(string Category, string Code, string Detail);

public sealed record IntentResultDto(bool Accepted, int? Version, RejectionDto? Rejection);

public sealed record CreateTableRequest(ulong? Seed = null, int? BotDelayMs = null);

public sealed record CreateTableResultDto(bool Ok, string? TableId, RejectionDto? Rejection);

public sealed record TakeSeatRequest(string? Seat);

public sealed record TakeSeatResultDto(bool Ok, int? Version, RejectionDto? Rejection);

public sealed record GetViewResultDto(TableUpdateDto? Update, RejectionDto? Rejection);

// ---- Rooms (protocol v2) ------------------------------------------------------------------------

/// <summary>One seat of a room as everyone in the room sees it. Never carries hands or tokens.</summary>
public sealed record RoomSeatDto(
    string Seat,
    string Kind,
    string? ParticipantId,
    string? Name,
    string? Connection,
    DateTimeOffset? DeadlineUtc,
    string? BotFor,
    string? Origin);

public sealed record MissingDto(string ParticipantId, string Seat, string Name, DateTimeOffset DeadlineUtc);

public sealed record PendingTakeoverDto(string Seat, string Name);

public sealed record RoomEventDto(string Kind, string? Seat, string? Name, string? ByName, int Revision);

public sealed record RoomYouDto(string ParticipantId, string? Seat, bool IsHost);

/// <summary>Pushed to every connected participant on each room change (<c>Room</c> message).</summary>
public sealed record RoomStateDto(
    int ProtocolVersion,
    string RoomId,
    int Revision,
    DateTimeOffset ServerNowUtc,
    string Lifecycle,
    string HostId,
    RoomYouDto You,
    IReadOnlyList<RoomSeatDto> Seats,
    IReadOnlyList<MissingDto> Missing,
    IReadOnlyList<PendingTakeoverDto> PendingTakeovers,
    RoomEventDto? LastEvent);

/// <summary>Pushed to a connection whose participant was taken over by a newer connection.</summary>
public sealed record SupersededDto(int ProtocolVersion);

public sealed record CreateRoomRequest(string? Token, string? Name, ulong? Seed = null, int? BotDelayMs = null);

public sealed record JoinRoomRequest(string? RoomId, string? Token, string? Name);

public sealed record RejoinRequest(string? RoomId, string? Token);

public sealed record SetNameRequest(string? Name);

public sealed record ClaimSeatRequest(string? Seat);

public sealed record SkipWaitRequest(string? ParticipantId = null);

/// <summary>The one result shape of every room method; unused members are null.</summary>
public sealed record RoomResultDto(
    bool Ok,
    string? RoomId,
    RoomStateDto? State,
    RejectionDto? Rejection,
    TableUpdateDto? Update = null,
    int? AwayMoves = null,
    bool? PendingNextHand = null);
