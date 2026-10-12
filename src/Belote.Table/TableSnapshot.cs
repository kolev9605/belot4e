using System.Collections.Immutable;
using Belote.Core;

namespace Belote.Table;

public enum TableLifecycle
{
    AwaitingSeat,
    InProgress,
    Finished,
    Faulted,
}

/// <summary>A trick whose fourth card has been played, kept visible until the next card is played.</summary>
public sealed record CompletedTrick(
    int DealNumber,
    int TrickNumber,
    Seat Leader,
    ImmutableArray<PlayedCard> Plays,
    Seat Winner);

/// <summary>
/// The immutable state of a table, published atomically after every change. <see cref="Version"/>
/// increases by exactly one per accepted intent; pausing changes <see cref="Paused"/> only.
/// </summary>
public sealed record TableSnapshot(
    int Version,
    GameState? State,
    CompletedTrick? LastCompletedTrick,
    TableLifecycle Lifecycle,
    bool Paused,
    ImmutableList<GameEvent> Events);
