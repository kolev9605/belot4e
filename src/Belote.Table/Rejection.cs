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

    Room,
    Seat,
    Permission,
    Name,
    State,
    Connection,
    Identity,
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
        RejectionCategory.Room => "room",
        RejectionCategory.Seat => "seat",
        RejectionCategory.Permission => "permission",
        RejectionCategory.Name => "name",
        RejectionCategory.State => "state",
        RejectionCategory.Connection => "connection",
        RejectionCategory.Identity => "identity",
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

/// <summary>The room-level rejection vocabulary (see contracts/hub-protocol.md).</summary>
public static class RoomRejections
{
    public static Rejection NotFound() => new(RejectionCategory.Room, "RoomNotFound", "There is no such room.");

    public static Rejection Ended() => new(RejectionCategory.Room, "RoomEnded", "This room has ended.");

    public static Rejection Full() => new(RejectionCategory.Room, "RoomFull", "Every seat in this room is taken.");

    public static Rejection SeatTaken() => new(RejectionCategory.Seat, "SeatTaken", "That seat is not available.");

    public static Rejection AlreadySeated() =>
        new(RejectionCategory.Seat, "AlreadySeated", "You already hold a seat in the running game.");

    public static Rejection NotHost() => new(RejectionCategory.Permission, "NotHost", "Only the host can do that.");

    public static Rejection NameInvalid(string detail = "The name is not allowed.") =>
        new(RejectionCategory.Name, "NameInvalid", detail);

    public static Rejection NameTaken() =>
        new(RejectionCategory.Name, "NameTaken", "Someone in this room already uses that name.");

    public static Rejection GameInProgress() =>
        new(RejectionCategory.State, "GameInProgress", "The game is already in progress.");

    public static Rejection GameNotStarted() =>
        new(RejectionCategory.GameNotInProgress, "GameNotStarted", "The game has not started.");

    public static Rejection NotMissing() =>
        new(RejectionCategory.State, "NotMissing", "Nobody is being waited for.");

    public static Rejection BadToken() =>
        new(RejectionCategory.Identity, "BadToken", "The identity token is not valid.");

    public static Rejection Superseded() =>
        new(RejectionCategory.Connection, "Superseded", "This tab was taken over by another tab or device.");

    public static Rejection NotInRoom() =>
        new(RejectionCategory.Identity, "NotInRoom", "This connection is not in a room.");
}
