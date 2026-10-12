using System;
using Belote.Core;
using Belote.Simulation;

namespace Belote.Table;

public enum RejectionCategory
{
    Malformed,
    NotSeated,
    GameNotInProgress,
    NotOwner,
    OutOfTurn,
    Illegal,
    TableExists,

    /// <summary>Internal: the table is paused because the client is not connected.</summary>
    Paused,
}

/// <summary>Why an intent (or a table operation) was refused. <c>Code</c> is stable and machine-readable.</summary>
public sealed record Rejection(RejectionCategory Category, string Code, string Detail)
{
    public static string CategoryName(RejectionCategory category) => category switch
    {
        RejectionCategory.Malformed => "malformed",
        RejectionCategory.NotSeated => "notSeated",
        RejectionCategory.GameNotInProgress => "gameNotInProgress",
        RejectionCategory.NotOwner => "notOwner",
        RejectionCategory.OutOfTurn => "outOfTurn",
        RejectionCategory.Illegal => "illegal",
        RejectionCategory.TableExists => "tableExists",
        RejectionCategory.Paused => "paused",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    public static Rejection Malformed(string detail) => new(RejectionCategory.Malformed, "Malformed", detail);

    public static Rejection NotSeated(string detail) => new(RejectionCategory.NotSeated, "NotSeated", detail);

    public static Rejection NotOwner(Seat seat) =>
        new(RejectionCategory.NotOwner, "NotOwner", $"This client does not control the {seat} seat.");

    public static Rejection Paused() =>
        new(RejectionCategory.Paused, "TablePaused", "The table is paused while the client is disconnected.");

    /// <summary>Maps an engine rejection onto the table's category and code vocabulary.</summary>
    public static Rejection FromEngine(RejectionReason reason, string detail) => reason switch
    {
        RejectionReason.NotYourTurn => new(RejectionCategory.OutOfTurn, "NotYourTurn", detail),
        RejectionReason.GameFinished => new(RejectionCategory.GameNotInProgress, "GameFinished", detail),
        _ => new(RejectionCategory.Illegal, JsonNames.Of(reason), detail),
    };
}

public abstract record IntentOutcome;

public sealed record IntentAccepted(int Version) : IntentOutcome;

public sealed record IntentRejected(Rejection Rejection) : IntentOutcome;
