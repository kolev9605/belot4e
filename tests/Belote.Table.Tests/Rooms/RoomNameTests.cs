using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;

namespace Belote.Table.Tests;

public class RoomNameTests
{
    [Fact]
    public async Task Renaming_in_the_lobby_is_visible_to_everyone_with_an_event()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));

        var result = await rig.Room.SetNameAsync(mira.Token, "  Mimi ");

        Assert.True(result.Ok);
        var state = ivo.Sink.LastRoom!;
        Assert.Equal("Mimi", state.Seats.Single(s => s.Seat == "north").Name);
        Assert.Equal("nameChanged", state.LastEvent!.Kind);
        Assert.Equal("Mimi", state.LastEvent.Name);
    }

    [Fact]
    public async Task Renaming_in_a_running_game_works_too()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        await rig.SeatAsync((ivo, Seat.South));
        await rig.Room.StartGameAsync(ivo.Token);

        var result = await rig.Room.SetNameAsync(ivo.Token, "Ivo K");

        Assert.True(result.Ok);
        Assert.Equal("Ivo K", ivo.Sink.LastRoom!.Seats.Single(s => s.Seat == "south").Name);
    }

    [Theory]
    [InlineData("", "NameInvalid")]
    [InlineData("    ", "NameInvalid")]
    [InlineData("0123456789012345678901", "NameInvalid")]
    [InlineData("IVO", "NameTaken")]
    public async Task Invalid_and_duplicate_names_are_refused_with_a_code(string name, string code)
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));

        var result = await rig.Room.SetNameAsync(mira.Token, name);

        Assert.False(result.Ok);
        Assert.Equal(code, result.Rejection!.Code);
        Assert.Equal("Mira", ivo.Sink.LastRoom!.Seats.Single(s => s.Seat == "north").Name);
    }

    [Fact]
    public async Task Keeping_your_own_name_is_not_a_duplicate()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        await rig.JoinAsync(ivo);

        var result = await rig.Room.SetNameAsync(ivo.Token, "ivo");

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task Bots_always_carry_the_plain_bot_name()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        await rig.SeatAsync((ivo, Seat.South));
        await rig.Room.StartGameAsync(ivo.Token);

        Assert.All(
            ivo.Sink.LastRoom!.Seats.Where(s => s.Kind == "bot"),
            s => Assert.Equal("Bot", s.Name));
    }
}
