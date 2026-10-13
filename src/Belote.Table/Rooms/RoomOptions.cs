using System;

namespace Belote.Table.Rooms;

/// <summary>Timing and limits for rooms. Bound from the <c>Room</c> configuration section.</summary>
public sealed class RoomOptions
{
    public const string SectionName = "Room";

    /// <summary>How long a table waits for a missing human before a bot takes the seat (FR-017/018).</summary>
    public int GraceSeconds { get; set; } = 60;

    /// <summary>How long a room with no connected human is kept (FR-024).</summary>
    public int IdleExpiryMinutes { get; set; } = 60;

    /// <summary>How long a lobby seat or participant whose holder is gone is kept (FR-025).</summary>
    public int LobbySeatReleaseSeconds { get; set; } = 60;

    /// <summary>Longest accepted display name, in characters (FR-010).</summary>
    public int MaxNameLength { get; set; } = 20;

    /// <summary>Clock for every timer; tests replace it.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    public TimeSpan Grace => TimeSpan.FromSeconds(Math.Max(0, GraceSeconds));

    public TimeSpan IdleExpiry => TimeSpan.FromMinutes(Math.Max(0, IdleExpiryMinutes));

    public TimeSpan LobbySeatRelease => TimeSpan.FromSeconds(Math.Max(0, LobbySeatReleaseSeconds));
}
