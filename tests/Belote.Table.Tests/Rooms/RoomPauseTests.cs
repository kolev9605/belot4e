using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;

namespace Belote.Table.Tests;

public class RoomPauseTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromSeconds(60);

    private static async Task<(RoomRig Rig, Person Ivo, Person Mira)> StartedAsync()
    {
        var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));
        await rig.Room.StartGameAsync(ivo.Token);
        await rig.Room.SyncAsync();
        return (rig, ivo, mira);
    }

    [Fact]
    public async Task A_disconnect_pauses_the_table_with_a_sixty_second_deadline()
    {
        var (rig, ivo, mira) = await StartedAsync();
        await using var _ = rig;
        var before = rig.Time.GetUtcNow();

        await rig.DisconnectAsync(mira);

        var info = await rig.Room.InfoAsync();
        Assert.True(info.TablePaused);
        var state = ivo.Sink.LastRoom!;
        var missing = Assert.Single(state.Missing);
        Assert.Equal("Mira", missing.Name);
        Assert.Equal("north", missing.Seat);
        Assert.Equal(before + Minute, missing.DeadlineUtc);
        Assert.Equal("reconnecting", state.Seats.Single(s => s.Seat == "north").Connection);
    }

    [Fact]
    public async Task Returning_before_the_deadline_lifts_the_pause_without_touching_the_game()
    {
        var (rig, ivo, mira) = await StartedAsync();
        await using var _ = rig;
        var versionBefore = (await rig.Room.InfoAsync()).TableVersion;
        await rig.DisconnectAsync(mira);
        await rig.AdvanceAsync(TimeSpan.FromSeconds(30));

        var back = await rig.RejoinAsync(mira);

        Assert.True(back.Ok);
        var info = await rig.Room.InfoAsync();
        Assert.False(info.TablePaused);
        Assert.Equal(versionBefore, info.TableVersion);
        Assert.Empty(ivo.Sink.LastRoom!.Missing);
        Assert.Equal("north", back.State!.You.Seat);
        Assert.NotNull(back.Update);
        Assert.Equal("north", back.Update!.View.Seat);
    }

    [Fact]
    public async Task The_table_stays_paused_until_every_missing_human_is_back_or_replaced()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        var pavel = new Person("Pavel", 'c');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North), (pavel, Seat.East));
        await rig.Room.StartGameAsync(ivo.Token);
        await rig.DisconnectAsync(mira);
        await rig.DisconnectAsync(pavel);
        Assert.Equal(2, ivo.Sink.LastRoom!.Missing.Count);

        await rig.RejoinAsync(mira);
        Assert.True((await rig.Room.InfoAsync()).TablePaused);
        Assert.Single(ivo.Sink.LastRoom!.Missing);

        await rig.AdvanceAsync(Minute + TimeSpan.FromSeconds(1));
        Assert.False((await rig.Room.InfoAsync()).TablePaused);
        Assert.Equal("bot", ivo.Sink.LastRoom!.Seats.Single(s => s.Seat == "east").Kind);
    }

    [Fact]
    public async Task Every_disconnection_gets_its_own_fresh_minute()
    {
        var (rig, ivo, mira) = await StartedAsync();
        await using var _ = rig;

        await rig.DisconnectAsync(mira);
        await rig.AdvanceAsync(TimeSpan.FromSeconds(50));
        await rig.RejoinAsync(mira);
        await rig.DisconnectAsync(mira);
        await rig.AdvanceAsync(TimeSpan.FromSeconds(59));

        Assert.Equal("human", ivo.Sink.LastRoom!.Seats.Single(s => s.Seat == "north").Kind);
        Assert.True((await rig.Room.InfoAsync()).TablePaused);

        await rig.AdvanceAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("bot", ivo.Sink.LastRoom!.Seats.Single(s => s.Seat == "north").Kind);
    }

    [Fact]
    public async Task A_lobby_disconnect_never_pauses_anything()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));

        await rig.DisconnectAsync(mira);

        var info = await rig.Room.InfoAsync();
        Assert.False(info.TablePaused);
        Assert.Empty(ivo.Sink.LastRoom!.Missing);
        Assert.Equal("lobby", ivo.Sink.LastRoom.Lifecycle);
    }

    [Fact]
    public async Task With_nobody_connected_the_table_stays_paused_and_the_first_to_return_resumes()
    {
        var (rig, ivo, mira) = await StartedAsync();
        await using var _ = rig;
        await rig.DisconnectAsync(ivo);
        await rig.DisconnectAsync(mira);

        await rig.AdvanceAsync(TimeSpan.FromMinutes(10));
        Assert.True((await rig.Room.InfoAsync()).TablePaused);

        var back = await rig.RejoinAsync(mira);

        Assert.Equal("north", back.State!.You.Seat);
        Assert.Equal("human", back.State.Seats.Single(s => s.Seat == "north").Kind);
        // The other person's minute has long run out, so a bot has taken that seat now.
        Assert.Equal("bot", back.State.Seats.Single(s => s.Seat == "south").Kind);
        Assert.False((await rig.Room.InfoAsync()).TablePaused);
    }
}
