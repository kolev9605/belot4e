using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;

namespace Belote.Table.Tests;

public class RoomHousekeepingTests
{
    [Fact]
    public async Task A_player_can_switch_seats_or_stand_up_in_the_lobby()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));

        await rig.ClaimAsync(mira, Seat.East);
        var afterSwitch = ivo.Sink.LastRoom!;
        Assert.Equal("free", afterSwitch.Seats.Single(s => s.Seat == "north").Kind);
        Assert.Equal("Mira", afterSwitch.Seats.Single(s => s.Seat == "east").Name);

        var stood = await rig.Room.ReleaseSeatAsync(mira.Token);
        Assert.True(stood.Ok);
        Assert.Equal("free", ivo.Sink.LastRoom!.Seats.Single(s => s.Seat == "east").Kind);
    }

    [Fact]
    public async Task When_the_host_leaves_the_host_role_passes_to_another_human()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));

        var left = await rig.Room.LeaveAsync(ivo.Token);

        Assert.True(left.Ok);
        Assert.Equal("left", ivo.Sink.ClosedReason);
        var state = mira.Sink.LastRoom!;
        Assert.True(state.You.IsHost);
        Assert.Equal("free", state.Seats.Single(s => s.Seat == "south").Kind);
        var started = await rig.Room.StartGameAsync(mira.Token);
        Assert.True(started.Ok);
    }

    [Fact]
    public async Task Leaving_a_running_game_hands_the_seat_to_a_bot_at_once_without_a_pause()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));
        await rig.Room.StartGameAsync(ivo.Token);

        await rig.Room.LeaveAsync(mira.Token);

        var north = ivo.Sink.LastRoom!.Seats.Single(s => s.Seat == "north");
        Assert.Equal("bot", north.Kind);
        Assert.Equal("left", north.Origin);
        Assert.Null(north.BotFor);
        Assert.False((await rig.Room.InfoAsync()).TablePaused);
    }

    [Fact]
    public async Task A_lobby_participant_who_never_returns_is_released_after_a_minute()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));

        await rig.DisconnectAsync(mira);
        await rig.AdvanceAsync(TimeSpan.FromSeconds(59));
        Assert.Equal("human", ivo.Sink.LastRoom!.Seats.Single(s => s.Seat == "north").Kind);

        await rig.AdvanceAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("free", ivo.Sink.LastRoom!.Seats.Single(s => s.Seat == "north").Kind);
        Assert.Equal(1, (await rig.Room.InfoAsync()).Members);
    }

    [Fact]
    public async Task Play_again_starts_a_new_game_with_the_same_seats_and_names()
    {
        await using var rig = new RoomRig(TimeSpan.Zero, seed: 12);
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));
        await rig.Room.StartGameAsync(ivo.Token);
        await rig.PlayOutAsync(new[] { ivo, mira });
        await rig.Room.SyncAsync();

        var notHost = await rig.Room.PlayAgainAsync(mira.Token);
        Assert.Equal("NotHost", notHost.Rejection!.Code);

        var again = await rig.Room.PlayAgainAsync(ivo.Token);
        await rig.Room.SyncAsync();

        Assert.True(again.Ok);
        var state = ivo.Sink.LastRoom!;
        Assert.Equal("ingame", state.Lifecycle);
        Assert.Equal("Ivo", state.Seats.Single(s => s.Seat == "south").Name);
        Assert.Equal("Mira", state.Seats.Single(s => s.Seat == "north").Name);
        Assert.False(ivo.Sink.LastUpdate!.Finished);
        Assert.Null(ivo.Sink.LastUpdate.View.Winner);
    }

    [Fact]
    public async Task Play_again_is_refused_while_a_game_is_running()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        await rig.SeatAsync((ivo, Seat.South));
        await rig.Room.StartGameAsync(ivo.Token);

        var result = await rig.Room.PlayAgainAsync(ivo.Token);

        Assert.False(result.Ok);
        Assert.Equal("GameInProgress", result.Rejection!.Code);
    }

    [Fact]
    public async Task A_room_nobody_is_connected_to_ends_after_the_idle_period()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        await rig.SeatAsync((ivo, Seat.South));
        await rig.DisconnectAsync(ivo);

        await rig.AdvanceAsync(TimeSpan.FromMinutes(59));
        Assert.Equal(RoomLifecycle.Lobby, (await rig.Room.InfoAsync()).Lifecycle);

        await rig.AdvanceAsync(TimeSpan.FromMinutes(2));
        await rig.Room.SyncAsync();

        Assert.True(rig.Room.Ended.IsCompleted);
        var tryAgain = await rig.Room.RejoinAsync(ivo.Token, "ivo-9", new TestSink());
        Assert.Equal("RoomEnded", tryAgain.Rejection!.Code);
    }

    [Fact]
    public async Task Someone_returning_cancels_the_idle_expiry()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        await rig.SeatAsync((ivo, Seat.South));
        await rig.Room.StartGameAsync(ivo.Token);
        await rig.DisconnectAsync(ivo);
        await rig.AdvanceAsync(TimeSpan.FromMinutes(30));

        var back = await rig.RejoinAsync(ivo);
        Assert.True(back.Ok);
        await rig.AdvanceAsync(TimeSpan.FromMinutes(90));

        Assert.False(rig.Room.Ended.IsCompleted);
    }
}
