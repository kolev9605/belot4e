using System;
using Belote.Core;

namespace Belote.Simulation;

/// <summary>Stable, language-neutral names used in logs, rule-set files and reports.</summary>
public static class JsonNames
{
    public static string Of(Seat seat) => seat switch
    {
        Seat.North => "north",
        Seat.East => "east",
        Seat.South => "south",
        Seat.West => "west",
        _ => throw new ArgumentOutOfRangeException(nameof(seat)),
    };

    public static Seat ParseSeat(string text) => text switch
    {
        "north" => Seat.North,
        "east" => Seat.East,
        "south" => Seat.South,
        "west" => Seat.West,
        _ => throw new FormatException($"Unknown seat '{text}'."),
    };

    public static string Of(Team team) => team == Team.NorthSouth ? "northSouth" : "eastWest";

    public static Team ParseTeam(string text) => text switch
    {
        "northSouth" => Team.NorthSouth,
        "eastWest" => Team.EastWest,
        _ => throw new FormatException($"Unknown team '{text}'."),
    };

    public static string Of(ContractKind kind) => kind switch
    {
        ContractKind.Clubs => "clubs",
        ContractKind.Diamonds => "diamonds",
        ContractKind.Hearts => "hearts",
        ContractKind.Spades => "spades",
        ContractKind.NoTrump => "noTrump",
        ContractKind.AllTrump => "allTrump",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static ContractKind ParseKind(string text) => text switch
    {
        "clubs" => ContractKind.Clubs,
        "diamonds" => ContractKind.Diamonds,
        "hearts" => ContractKind.Hearts,
        "spades" => ContractKind.Spades,
        "noTrump" => ContractKind.NoTrump,
        "allTrump" => ContractKind.AllTrump,
        _ => throw new FormatException($"Unknown contract '{text}'."),
    };

    public static string Of(Suit suit) => suit switch
    {
        Suit.Clubs => "clubs",
        Suit.Diamonds => "diamonds",
        Suit.Hearts => "hearts",
        Suit.Spades => "spades",
        _ => throw new ArgumentOutOfRangeException(nameof(suit)),
    };

    public static Suit ParseSuit(string text) => text switch
    {
        "clubs" => Suit.Clubs,
        "diamonds" => Suit.Diamonds,
        "hearts" => Suit.Hearts,
        "spades" => Suit.Spades,
        _ => throw new FormatException($"Unknown suit '{text}'."),
    };

    public static Rank ParseRank(string text) =>
        Card.TryParse(text + "C", out var card) ? card.Rank : throw new FormatException($"Unknown rank '{text}'.");

    public static string Of(RejectionReason reason) => reason.ToString();
}
