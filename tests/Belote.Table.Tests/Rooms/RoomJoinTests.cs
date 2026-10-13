using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;

namespace Belote.Table.Tests;

public class RoomJoinTests
{
    [Fact]
    public async Task Joining_adds_a_participant_who_then_picks_a_seat()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.JoinAsync(ivo);

        var joined = await rig.JoinAsync(mira);
        Assert.True(joined.Ok);
        Assert.Null(joined.State!.You.Seat);
        Assert.False(joined.State.You.IsHost);

        await rig.ClaimAsync(mira, Seat.North);

        var north = ivo.Sink.LastRoom!.Seats.Single(s => s.Seat == "north");
        Assert.Equal("human", north.Kind);
        Assert.Equal("Mira", north.Name);
    }

    [Theory]
    [InlineData("", "NameInvalid")]
    [InlineData("   ", "NameInvalid")]
    [InlineData("abcdefghijklmnopqrstu", "NameInvalid")]
    [InlineData("ivo", "NameTaken")]
    public async Task A_name_that_breaks_the_rules_is_refused(string name, string code)
    {
        await using var rig = new RoomRig();
        await rig.JoinAsync(new Person("Ivo", 'a'));

        var result = await rig.Room.JoinAsync(new string('b', 32), name, "c-1", new TestSink());

        Assert.False(result.Ok);
        Assert.Equal(code, result.Rejection!.Code);
    }

    [Fact]
    public async Task Two_people_claiming_the_same_seat_get_exactly_one_winner()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        var pavel = new Person("Pavel", 'c');
        await rig.JoinAsync(ivo);
        await rig.JoinAsync(mira);
        await rig.JoinAsync(pavel);

        var results = await Task.WhenAll(
            rig.Room.ClaimSeatAsync(mira.Token, Seat.East),
            rig.Room.ClaimSeatAsync(pavel.Token, Seat.East));

        Assert.Single(results, r => r.Ok);
        var loser = results.Single(r => !r.Ok);
        Assert.Equal("SeatTaken", loser.Rejection!.Code);
    }

    [Fact]
    public async Task A_room_with_four_humans_is_full()
    {
        await using var rig = new RoomRig();
        await rig.SeatAsync(
            (new Person("A", 'a'), Seat.South),
            (new Person("B", 'b'), Seat.North),
            (new Person("C", 'c'), Seat.East),
            (new Person("D", 'd'), Seat.West));

        var fifth = await rig.JoinAsync(new Person("E", 'e'));

        Assert.False(fifth.Ok);
        Assert.Equal("RoomFull", fifth.Rejection!.Code);
    }

    [Fact]
    public async Task Joining_again_with_the_same_token_is_idempotent_and_supersedes_the_old_connection()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var first = await rig.JoinAsync(ivo);
        var oldSink = ivo.Sink;

        var second = await rig.JoinAsync(ivo);

        Assert.True(second.Ok);
        Assert.Equal(first.State!.You.ParticipantId, second.State!.You.ParticipantId);
        Assert.Equal(1, oldSink.SupersededCount);
        var info = await rig.Room.InfoAsync();
        Assert.Equal(1, info.Members);
    }

    [Fact]
    public async Task Rejoining_without_having_joined_is_refused_and_a_short_token_is_bad()
    {
        await using var rig = new RoomRig();
        await rig.JoinAsync(new Person("Ivo", 'a'));

        var stranger = await rig.Room.RejoinAsync(new string('z', 32), "z-1", new TestSink());
        var bad = await rig.Room.JoinAsync("short", "Mira", "m-1", new TestSink());

        Assert.Equal("NotInRoom", stranger.Rejection!.Code);
        Assert.Equal("BadToken", bad.Rejection!.Code);
    }
}
