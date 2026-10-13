using Belote.Bots;
using Belote.Core;
using Belote.Simulation;
using Xunit;

namespace Belote.Simulation.Tests;

public class SmartPresetTests
{
    private static string Label(IReadOnlyDictionary<Seat, SeatController> seats, Seat seat) => seats[seat].Name;

    [Theory]
    [InlineData("all-smart")]
    [InlineData("smart-ns")]
    [InlineData("smart-ew")]
    [InlineData("smart-table")]
    public void New_presets_are_listed_and_valid(string preset)
    {
        Assert.Contains(preset, SeatPresets.Names);
        Assert.True(SeatPresets.IsValid(preset));
    }

    [Fact]
    public void All_smart_seats_every_controller_as_a_smart_bot()
    {
        var seats = SeatPresets.Create(SeatPresets.AllSmart, 7);
        Assert.All(Seats.All, s =>
        {
            Assert.Equal("SmartBot", Label(seats, s));
            Assert.IsType<SmartBot>(seats[s].Player);
        });
    }

    [Fact]
    public void Smart_ns_and_smart_ew_are_mirrors_with_random_bots_on_the_other_team()
    {
        var ns = SeatPresets.Create(SeatPresets.SmartNs, 7);
        Assert.Equal("SmartBot", Label(ns, Seat.North));
        Assert.Equal("SmartBot", Label(ns, Seat.South));
        Assert.Equal("RandomLegalBot", Label(ns, Seat.East));
        Assert.Equal("RandomLegalBot", Label(ns, Seat.West));

        var ew = SeatPresets.Create(SeatPresets.SmartEw, 7);
        Assert.Equal("RandomLegalBot", Label(ew, Seat.North));
        Assert.Equal("RandomLegalBot", Label(ew, Seat.South));
        Assert.Equal("SmartBot", Label(ew, Seat.East));
        Assert.Equal("SmartBot", Label(ew, Seat.West));
    }

    [Fact]
    public void Smart_table_has_a_random_south_and_smart_everywhere_else()
    {
        var seats = SeatPresets.Create(SeatPresets.SmartTable, 7);
        Assert.Equal("RandomLegalBot", Label(seats, Seat.South));
        Assert.All(new[] { Seat.North, Seat.East, Seat.West }, s => Assert.Equal("SmartBot", Label(seats, s)));
    }

    [Fact]
    public async Task Random_seats_in_new_presets_use_the_same_seed_derivation_as_all_bot()
    {
        // The random South of smart-table plays exactly what the all-bot South would from the same views.
        const ulong seed = 9;
        var smart = SeatPresets.Create(SeatPresets.SmartTable, seed)[Seat.South].Player;
        var reference = new RandomLegalBot(Seeds.ForSeat(seed, Seat.South));
        var state = GameEngine.Start(RuleSets.Bulgarian, seed).State;
        while (state.ToAct != Seat.South)
        {
            var view = Projection.For(state, state.ToAct);
            state = ((Accepted)GameEngine.Apply(state, state.ToAct, LegalActions.For(view)[0])).State;
        }

        var southView = Projection.For(state, Seat.South);
        Assert.Equal(
            await reference.DecideAsync(southView, default),
            await smart.DecideAsync(southView, default));
    }

    [Fact]
    public void Existing_presets_are_unchanged()
    {
        var allBot = SeatPresets.Create(SeatPresets.AllBot, 3);
        Assert.All(Seats.All, s => Assert.Equal("RandomLegalBot", Label(allBot, s)));
        var mixed = SeatPresets.Create("mixed-2", 3);
        Assert.Equal(2, mixed.Values.Count(c => c.Name == "ScriptedHuman"));
    }
}
