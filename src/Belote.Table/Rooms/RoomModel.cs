using System;
using Belote.Core;

namespace Belote.Table.Rooms;

public enum RoomLifecycle
{
    Lobby,
    InGame,
    Finished,
    Ended,
}

/// <summary>Why a bot holds a seat; decides who may take it over.</summary>
public enum BotOrigin
{
    /// <summary>Never human (filled at start); open to late join.</summary>
    Default,

    /// <summary>A human left deliberately; open to late join, no reservation.</summary>
    Left,

    /// <summary>A human was replaced after a disconnect; only that human may reclaim it.</summary>
    Replaced,
}

/// <summary>What currently sits on one seat of a room.</summary>
public abstract record SeatSlot
{
    public static readonly SeatSlot Free = new FreeSlot();

    public sealed record FreeSlot : SeatSlot;

    public sealed record Human(Guid ParticipantId) : SeatSlot;

    public sealed record Bot(BotOrigin Origin, Guid? ReplacedParticipantId = null) : SeatSlot;
}

/// <summary>Where a participant's connection stands.</summary>
public enum ParticipantStatus
{
    Connected,

    /// <summary>Seated in a running game and gone: the table is paused until the deadline.</summary>
    Reconnecting,

    /// <summary>Replaced by a bot after the deadline; may reclaim by returning.</summary>
    Away,

    /// <summary>Not seated in a running game and not connected; released after a short while.</summary>
    Offline,
}

/// <summary>A person waiting for a missing human.</summary>
public sealed record PauseEntry(Guid ParticipantId, Seat Seat, DateTimeOffset DeadlineUtc);

/// <summary>Receives the room's pushes for one connection. Implementations must not block.</summary>
public interface IRoomSink
{
    void PushRoom(Belote.Table.RoomStateDto state);

    void PushUpdate(Belote.Table.TableUpdateDto update);

    void PushSuperseded();

    void PushClosed(string reason);
}

/// <summary>The outcome of a room operation. <see cref="State"/> is the caller's view of the room.</summary>
public sealed record RoomResult(
    bool Ok,
    Rejection? Rejection = null,
    RoomStateDto? State = null,
    TableUpdateDto? Update = null,
    int? AwayMoves = null,
    bool? PendingNextHand = null)
{
    public static RoomResult Fail(Rejection rejection) => new(false, rejection);
}

/// <summary>Per-room settings chosen by the caller (the engine never picks a seed).</summary>
public sealed record RoomSettings(Func<ulong> SeedSource, TimeSpan BotDelay);
