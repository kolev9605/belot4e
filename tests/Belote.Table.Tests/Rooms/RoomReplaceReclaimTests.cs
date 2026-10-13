using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;

namespace Belote.Table.Tests;

public class RoomReplaceReclaimTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromSeconds(60);

    private static async Task<(RoomRig Rig, Person Ivo, Person Mira)> StartedAsync(TimeSpan? botDelay = null)
    {
        var rig = new RoomRig(botDelay);
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));
        await rig.Room.StartGameAsync(ivo.Token);
        await rig.Room.SyncAsync();
        return (rig, ivo, mira);
    }

    [Fact]
    public async Task At_the_deadline_a_bot_takes_the_seat_and_play_resumes_for_everyone()
    {
        var (rig, ivo, mira) = await StartedAsync();
        await using var _ = rig;
        await rig.DisconnectAsync(mira);

        await rig.AdvanceAsync(Minute + TimeSpan.FromSeconds(1));

        var state = ivo.Sink.LastRoom!;
        var north = state.Seats.Single(s => s.Seat == "north");
        Assert.Equal("bot", north.Kind);
        Assert.Equal("replaced", north.Origin);
        Assert.Equal("Mira", north.BotFor);
        Assert.Empty(state.Missing);
        Assert.Equal("replaced", state.LastEvent!.Kind);
        Assert.False((await rig.Room.InfoAsync()).TablePaused);
    }

    [Fact]
    public async Task Another_human_can_skip_the_wait_and_the_event_names_who_did_it()
    {
        var (rig, ivo, mira) = await StartedAsync();
        await using var _ = rig;
        await rig.DisconnectAsync(mira);

        var skipped = await rig.Room.SkipWaitAsync(ivo.Token, null);

        Assert.True(skipped.Ok);
        var state = ivo.Sink.LastRoom!;
        Assert.Equal("bot", state.Seats.Single(s => s.Seat == "north").Kind);
        Assert.Equal("skipped", state.LastEvent!.Kind);
        Assert.Equal("Ivo", state.LastEvent.ByName);
        Assert.Equal("Mira", state.LastEvent.Name);
        Assert.False((await rig.Room.InfoAsync()).TablePaused);
    }

    [Fact]
    public async Task The_missing_player_cannot_skip_their_own_wait_and_skipping_needs_someone_missing()
    {
        var (rig, ivo, mira) = await StartedAsync();
        await using var _ = rig;

        var nobodyMissing = await rig.Room.SkipWaitAsync(ivo.Token, null);
        Assert.Equal("NotMissing", nobodyMissing.Rejection!.Code);

        await rig.DisconnectAsync(mira);
        var self = await rig.Room.SkipWaitAsync(mira.Token, null);
        Assert.False(self.Ok);
        Assert.Equal("NotMissing", self.Rejection!.Code);
    }

    [Fact]
    public async Task A_replaced_player_who_returns_takes_the_seat_back_with_a_recap()
    {
        var (rig, ivo, mira) = await StartedAsync();
        await using var _ = rig;
        await rig.DisconnectAsync(mira);
        await rig.AdvanceAsync(Minute + TimeSpan.FromSeconds(1));
        var versionBefore = (await rig.Room.InfoAsync()).TableVersion;

        var back = await rig.RejoinAsync(mira);

        Assert.True(back.Ok);
        Assert.NotNull(back.AwayMoves);
        Assert.Equal("north", back.State!.You.Seat);
        var north = back.State.Seats.Single(s => s.Seat == "north");
        Assert.Equal("human", north.Kind);
        Assert.Equal("Mira", north.Name);
        Assert.Equal("north", back.Update!.View.Seat);
        Assert.Equal(versionBefore, (await rig.Room.InfoAsync()).TableVersion);
        Assert.Equal("reclaimed", ivo.Sink.LastRoom!.LastEvent!.Kind);
    }

    [Fact]
    public async Task Bots_that_played_for_a_replaced_human_are_counted_in_the_recap()
    {
        var rig = new RoomRig(TimeSpan.Zero, seed: 3);
        await using var _ = rig;
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));
        await rig.Room.StartGameAsync(ivo.Token);

        // Let Ivo play so the game moves along while Mira is away.
        await rig.DisconnectAsync(mira);
        await rig.Room.SkipWaitAsync(ivo.Token, null);
        var humans = new[] { ivo };
        var until = DateTime.UtcNow.AddSeconds(20);
        int moves = 0;
        while (DateTime.UtcNow < until && moves < 12)
        {
            var update = ivo.Sink.LastUpdate;
            if (update is { Finished: false } && update.View.LegalActions.Count > 0)
            {
                var outcome = await rig.Room.SubmitIntentAsync(ivo.Token, update.View.LegalActions[0]);
                if (outcome is IntentAccepted)
                {
                    moves++;
                }
            }

            await Task.Delay(3);
        }

        var back = await rig.RejoinAsync(mira);

        Assert.True(back.Ok);
        Assert.True(back.AwayMoves >= 1, $"expected a recap of at least one move, got {back.AwayMoves}");
        Assert.NotEmpty(humans);
    }

    [Fact]
    public async Task A_stranger_cannot_claim_the_seat_held_for_a_replaced_player()
    {
        var (rig, ivo, mira) = await StartedAsync();
        await using var _ = rig;
        var pavel = new Person("Pavel", 'c');
        await rig.JoinAsync(pavel);
        await rig.DisconnectAsync(mira);
        await rig.AdvanceAsync(Minute + TimeSpan.FromSeconds(1));

        var claim = await rig.Room.ClaimSeatAsync(pavel.Token, Seat.North);

        Assert.False(claim.Ok);
        Assert.Equal("SeatTaken", claim.Rejection!.Code);
        Assert.NotNull(ivo.Sink.LastRoom);
    }
}
