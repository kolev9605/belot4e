using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;

namespace Belote.Table.Tests;

public class RoomPlayTests
{
    [Fact]
    public async Task Each_human_receives_only_their_own_seat_and_cannot_act_for_another()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));
        await rig.Room.StartGameAsync(ivo.Token);
        await rig.Room.SyncAsync();

        Assert.Equal("south", ivo.Sink.LastUpdate!.View.Seat);
        Assert.Equal("north", mira.Sink.LastUpdate!.View.Seat);
        Assert.Empty(ivo.Sink.LastUpdate.View.OwnHand.Intersect(mira.Sink.LastUpdate.View.OwnHand));

        var forged = new IntentDto("pass", "south");
        var outcome = await rig.Room.SubmitIntentAsync(mira.Token, forged);

        var rejected = Assert.IsType<IntentRejected>(outcome);
        Assert.Equal(RejectionCategory.NotOwner, rejected.Rejection.Category);
    }

    [Fact]
    public async Task Two_humans_and_two_bots_play_a_full_game_to_a_winner()
    {
        await using var rig = new RoomRig(botDelay: TimeSpan.Zero, seed: 12);
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));
        await rig.Room.StartGameAsync(ivo.Token);

        await rig.PlayOutAsync(new[] { ivo, mira });
        await rig.Room.SyncAsync();

        var info = await rig.Room.InfoAsync();
        Assert.Equal(RoomLifecycle.Finished, info.Lifecycle);
        Assert.True(ivo.Sink.LastUpdate!.Finished);
        Assert.NotNull(ivo.Sink.LastUpdate.View.Winner);
        Assert.Equal("finished", ivo.Sink.LastRoom!.Lifecycle);

        var versions = ivo.Sink.Updates.Select(u => u.Version).ToArray();
        Assert.Equal(versions.OrderBy(v => v).Distinct().ToArray(), versions);
    }

    [Fact]
    public async Task Intents_before_the_game_starts_are_refused()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        await rig.JoinAsync(ivo);

        var outcome = await rig.Room.SubmitIntentAsync(ivo.Token, new IntentDto("pass", "south"));

        Assert.IsType<IntentRejected>(outcome);
    }
}
