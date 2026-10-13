using Belote.Core;
using Belote.Table;
using Belote.Table.Rooms;

namespace Belote.Table.Tests;

public class RoomLateJoinTests
{
    /// <summary>Plays for the given people until the room's deal number is above <paramref name="deal"/>.</summary>
    private static async Task PlayUntilDealAsync(RoomRig rig, IReadOnlyList<Person> humans, int deal)
    {
        var until = DateTime.UtcNow.AddSeconds(60);
        var submitted = humans.ToDictionary(h => h, _ => -1);
        while ((await rig.Room.InfoAsync()).DealNumber <= deal)
        {
            Assert.True(DateTime.UtcNow < until, "The next hand did not start in time.");
            foreach (var human in humans)
            {
                var update = human.Sink.LastUpdate;
                if (update is null || update.Finished || update.View.LegalActions.Count == 0
                    || submitted[human] == update.Version)
                {
                    continue;
                }

                submitted[human] = update.Version;
                await rig.Room.SubmitIntentAsync(human.Token, update.View.LegalActions[0]);
            }

            await Task.Delay(2);
        }

        await rig.Room.SyncAsync();
    }

    [Fact]
    public async Task A_newcomer_takes_a_bot_seat_at_the_start_of_the_next_hand()
    {
        await using var rig = new RoomRig(TimeSpan.Zero, seed: 5);
        var ivo = new Person("Ivo", 'a');
        var pavel = new Person("Pavel", 'c');
        await rig.SeatAsync((ivo, Seat.South));
        await rig.Room.StartGameAsync(ivo.Token);
        await rig.JoinAsync(pavel);

        var claim = await rig.Room.ClaimSeatAsync(pavel.Token, Seat.East);

        Assert.True(claim.Ok);
        Assert.True(claim.PendingNextHand);
        Assert.Equal("bot", pavel.Sink.LastRoom!.Seats.Single(s => s.Seat == "east").Kind);
        Assert.Equal("east", Assert.Single(pavel.Sink.LastRoom.PendingTakeovers).Seat);
        Assert.Equal("joiningNextHand", ivo.Sink.LastRoom!.LastEvent!.Kind);

        int deal = (await rig.Room.InfoAsync()).DealNumber;
        await PlayUntilDealAsync(rig, new[] { ivo }, deal);

        var east = ivo.Sink.LastRoom!.Seats.Single(s => s.Seat == "east");
        Assert.Equal("human", east.Kind);
        Assert.Equal("Pavel", east.Name);
        Assert.Empty(ivo.Sink.LastRoom.PendingTakeovers);
        Assert.Equal("east", pavel.Sink.LastUpdate!.View.Seat);
    }

    [Fact]
    public async Task The_first_claimant_wins_and_a_second_is_told_the_seat_is_taken()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var pavel = new Person("Pavel", 'c');
        var dani = new Person("Dani", 'd');
        await rig.SeatAsync((ivo, Seat.South));
        await rig.Room.StartGameAsync(ivo.Token);
        await rig.JoinAsync(pavel);
        await rig.JoinAsync(dani);

        var first = await rig.Room.ClaimSeatAsync(pavel.Token, Seat.East);
        var second = await rig.Room.ClaimSeatAsync(dani.Token, Seat.East);

        Assert.True(first.Ok);
        Assert.False(second.Ok);
        Assert.Equal("SeatTaken", second.Rejection!.Code);
    }

    [Fact]
    public async Task A_seat_left_by_a_human_is_open_to_anyone_including_the_leaver()
    {
        await using var rig = new RoomRig();
        var ivo = new Person("Ivo", 'a');
        var mira = new Person("Mira", 'b');
        await rig.SeatAsync((ivo, Seat.South), (mira, Seat.North));
        await rig.Room.StartGameAsync(ivo.Token);
        await rig.Room.LeaveAsync(mira.Token);

        var back = new Person("Mira", 'b');
        var joined = await rig.JoinAsync(back);
        Assert.True(joined.Ok);
        var claim = await rig.Room.ClaimSeatAsync(back.Token, Seat.North);

        Assert.True(claim.Ok);
        Assert.True(claim.PendingNextHand);
    }

    [Fact]
    public async Task A_late_claim_never_changes_scores_or_game_state()
    {
        await using var rig = new RoomRig(TimeSpan.FromHours(1), seed: 5);
        var ivo = new Person("Ivo", 'a');
        var pavel = new Person("Pavel", 'c');
        await rig.SeatAsync((ivo, Seat.South));
        await rig.Room.StartGameAsync(ivo.Token);
        await rig.JoinAsync(pavel);
        var before = await rig.Room.InfoAsync();

        await rig.Room.ClaimSeatAsync(pavel.Token, Seat.West);
        var after = await rig.Room.InfoAsync();

        Assert.Equal(before.TableVersion, after.TableVersion);
        Assert.Equal(before.DealNumber, after.DealNumber);
    }
}
