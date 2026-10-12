using System.Collections.Immutable;

namespace Belote.Core;

/// <summary>Seats in play (clockwise) order. North/South and East/West are partners.</summary>
public enum Seat
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

public enum Team
{
    NorthSouth = 0,
    EastWest = 1,
}

public static class Seats
{
    public static ImmutableArray<Seat> All { get; } =
        ImmutableArray.Create(Seat.North, Seat.East, Seat.South, Seat.West);

    public static ImmutableArray<Team> Teams { get; } =
        ImmutableArray.Create(Team.NorthSouth, Team.EastWest);

    public static Seat Next(this Seat seat) => (Seat)(((int)seat + 1) % 4);

    public static Seat Partner(this Seat seat) => (Seat)(((int)seat + 2) % 4);

    public static Team GetTeam(this Seat seat) => (Team)((int)seat % 2);

    public static Team Opponent(this Team team) => (Team)(1 - (int)team);

    public static Seat After(this Seat seat, int steps) => (Seat)((((int)seat + steps) % 4 + 4) % 4);
}
