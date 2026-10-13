using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;

namespace Belote.Table.Tests;

public class RoomLobbyTests
{
    [Fact]
    public async Task Creating_a_room_seats_the_creator_at_south_as_host()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');

        var result = await rig.JoinAsync(ivo);

        Assert.True(result.Ok);
        var state = result.State!;
        Assert.Equal("lobby", state.Lifecycle);
        Assert.Equal("south", state.You.Seat);
        Assert.True(state.You.IsHost);
        Assert.Equal("human", state.Seats.Single(s => s.Seat == "south").Kind);
        Assert.All(state.Seats.Where(s => s.Seat != "south"), s => Assert.Equal("free", s.Kind));
        Assert.Equal(2, state.ProtocolVersion);
    }

    [Fact]
    public async Task Only_the_host_can_start()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));

        var result = await rig.Room.StartGameAsync(mira.Token);

        Assert.False(result.Ok);
        Assert.Equal("NotHost", result.Rejection!.Code);
    }

    [Fact]
    public async Task Starting_fills_every_empty_seat_with_a_default_bot_and_deals_to_humans()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        await rig.SeatAsync((ivo, Seat.South));

        var started = await rig.Room.StartGameAsync(ivo.Token);
        await rig.Room.SyncAsync();

        Assert.True(started.Ok);
        var state = ivo.Sink.LastRoom!;
        Assert.Equal("ingame", state.Lifecycle);
        foreach (var seat in state.Seats.Where(s => s.Seat != "south"))
        {
            Assert.Equal("bot", seat.Kind);
            Assert.Equal("default", seat.Origin);
        }

        Assert.NotNull(ivo.Sink.LastUpdate);
        Assert.Equal("south", ivo.Sink.LastUpdate!.View.Seat);
        Assert.NotEmpty(ivo.Sink.LastUpdate.View.OwnHand);
    }

    [Fact]
    public async Task Starting_twice_is_rejected()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        await rig.SeatAsync((ivo, Seat.South));
        await rig.Room.StartGameAsync(ivo.Token);

        var again = await rig.Room.StartGameAsync(ivo.Token);

        Assert.False(again.Ok);
        Assert.Equal("GameInProgress", again.Rejection!.Code);
    }

    [Fact]
    public async Task Starting_needs_at_least_one_seated_human()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        await rig.SeatAsync((ivo, Seat.South));
        await rig.Room.ReleaseSeatAsync(ivo.Token);

        var result = await rig.Room.StartGameAsync(ivo.Token);

        Assert.False(result.Ok);
        Assert.Equal("GameNotStarted", result.Rejection!.Code);
    }
}
