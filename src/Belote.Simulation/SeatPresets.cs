using System;
using System.Collections.Generic;
using System.Linq;
using Belote.Bots;
using Belote.Core;

namespace Belote.Simulation;

/// <summary>Named seat setups: all bots, or k scripted "human" seats among bots.</summary>
public static class SeatPresets
{
    public const string AllBot = "all-bot";
    public const string ScriptedIllegal = "scripted-illegal";

    /// <summary>Smart bots in every seat.</summary>
    public const string AllSmart = "all-smart";

    /// <summary>Smart bots as North/South against random bots as East/West.</summary>
    public const string SmartNs = "smart-ns";

    /// <summary>Smart bots as East/West against random bots as North/South.</summary>
    public const string SmartEw = "smart-ew";

    /// <summary>Random South (the live table's baseline client) with smart North, East and West.</summary>
    public const string SmartTable = "smart-table";

    public static IReadOnlyList<string> Names { get; } = new[]
    {
        AllBot, "mixed-1", "mixed-2", "mixed-3", "mixed-4", ScriptedIllegal, AllSmart, SmartNs, SmartEw, SmartTable,
    };

    public static bool IsValid(string preset) => Names.Contains(preset);

    /// <summary>
    /// Builds the seat controllers for one game. Scripted seats make the same random-legal choices a bot
    /// would from the same seed, so a mixed game must match the all-bot game exactly (the engine is
    /// seat-blind); the <c>scripted-illegal</c> preset additionally injects illegal attempts.
    /// </summary>
    public static IReadOnlyDictionary<Seat, SeatController> Create(string preset, ulong gameSeed)
    {
        if (preset is AllSmart or SmartNs or SmartEw or SmartTable)
        {
            return CreateSmart(preset, gameSeed);
        }

        int scripted = preset switch
        {
            AllBot => 0,
            "mixed-1" => 1,
            "mixed-2" => 2,
            "mixed-3" => 3,
            "mixed-4" => 4,
            ScriptedIllegal => 2,
            _ => throw new ArgumentException($"Unknown seat preset '{preset}'.", nameof(preset)),
        };

        var result = new Dictionary<Seat, SeatController>();
        int index = 0;
        foreach (var seat in Seats.All)
        {
            var bot = new RandomLegalBot(Seeds.ForSeat(gameSeed, seat));
            if (index++ >= scripted)
            {
                result[seat] = new SeatController("RandomLegalBot", bot);
                continue;
            }

            IPlayer human = new ScriptedPlayer(view => bot.DecideAsync(view, default).Result);
            if (preset == ScriptedIllegal)
            {
                human = new IllegalFirstPlayer(human);
            }

            result[seat] = new SeatController("ScriptedHuman", human);
        }

        return result;
    }

    private static IReadOnlyDictionary<Seat, SeatController> CreateSmart(string preset, ulong gameSeed)
    {
        bool IsSmart(Seat seat) => preset switch
        {
            AllSmart => true,
            SmartNs => seat.GetTeam() == Team.NorthSouth,
            SmartEw => seat.GetTeam() == Team.EastWest,
            _ => seat != Seat.South, // SmartTable
        };

        var result = new Dictionary<Seat, SeatController>();
        foreach (var seat in Seats.All)
        {
            result[seat] = IsSmart(seat)
                ? new SeatController(nameof(SmartBot), new SmartBot())
                : new SeatController(nameof(RandomLegalBot), new RandomLegalBot(Seeds.ForSeat(gameSeed, seat)));
        }

        return result;
    }
}
