using System.Collections.Immutable;
using Microsoft.Extensions.Time.Testing;
using Belote.Bots;
using Belote.Core;
using Belote.Simulation;
using Belote.Table;

namespace Belote.Table.Tests;

public class ControllerSwapTests
{
    private static readonly Principal Human = new HumanPrincipal(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private static LiveTable StartedTable(
        ulong seed,
        IReadOnlyDictionary<Seat, Principal> controllers,
        TimeSpan? delay = null,
        TimeProvider? time = null)
    {
        var options = new TableOptions
        {
            Seed = seed,
            BotDelay = delay ?? TimeSpan.Zero,
            TimeProvider = time ?? TimeProvider.System,
        };
        return LiveTable.CreateStarted(options, controllers);
    }

    [Fact]
    public async Task Swapping_a_seat_controller_never_changes_state_or_version()
    {
        var time = new FakeTimeProvider();
        await using var table = StartedTable(
            3, new Dictionary<Seat, Principal> { [Seat.South] = Human }, TimeSpan.FromMilliseconds(500), time);

        var before = table.Snapshot;
        for (int i = 0; i < 5; i++)
        {
            await table.SetControllerAsync(Seat.South, new BotPrincipal(Seat.South));
            await table.SetControllerAsync(Seat.South, Human);
        }

        await table.SetControllerAsync(Seat.North, Human);
        var after = table.Snapshot;
        Assert.Equal(before.Version, after.Version);
        Assert.Same(before.State, after.State);
        Assert.Equal(TableLifecycle.InProgress, after.Lifecycle);
        Assert.Equal(Human, table.ControllerOf(Seat.North));
    }

    [Fact]
    public async Task A_bot_intent_for_a_seat_that_was_handed_to_a_human_is_not_owner_and_not_a_fault()
    {
        await using var table = StartedTable(
            5, new Dictionary<Seat, Principal> { [Seat.South] = Human }, TimeSpan.FromHours(1), new FakeTimeProvider());

        var stale = await table.SubmitAsync(new Intent(Seat.South, new PassAction(), new BotPrincipal(Seat.South)));

        var rejected = Assert.IsType<IntentRejected>(stale);
        Assert.Equal(RejectionCategory.NotOwner, rejected.Rejection.Category);
        Assert.Equal(TableLifecycle.InProgress, table.Snapshot.Lifecycle);
    }

    [Fact]
    public async Task Pausing_on_behalf_of_a_room_blocks_every_seat_and_resuming_restores_play()
    {
        await using var table = StartedTable(
            7, new Dictionary<Seat, Principal> { [Seat.South] = Human }, TimeSpan.FromHours(1), new FakeTimeProvider());

        await table.SetPausedAsync(true);
        Assert.True(table.Snapshot.Paused);
        var seat = table.Snapshot.State!.ToAct;
        var blocked = await table.SubmitAsync(new Intent(seat, new PassAction(), table.ControllerOf(seat)));
        Assert.Equal(RejectionCategory.Paused, Assert.IsType<IntentRejected>(blocked).Rejection.Category);

        await table.SetPausedAsync(false);
        Assert.False(table.Snapshot.Paused);
    }

    [Theory]
    [InlineData(11UL, 0)]
    [InlineData(11UL, 9)]
    [InlineData(23UL, 40)]
    [InlineData(23UL, 133)]
    [InlineData(37UL, 250)]
    public async Task A_seat_handed_from_a_human_to_a_bot_at_any_point_leaves_the_game_unchanged(ulong seed, int cut)
    {
        // Reference: every seat is a bot (deterministic SmartBot) from the start.
        await using var reference = StartedTable(seed, new Dictionary<Seat, Principal>());
        var expected = await reference.Completion.WaitAsync(TimeSpan.FromSeconds(60));
        Assert.NotNull(expected);

        // Subject: South is a "human" who plays exactly what SmartBot would, until the cut, then a bot takes over.
        await using var subject = StartedTable(seed, new Dictionary<Seat, Principal> { [Seat.South] = Human });
        var player = new SmartBot();
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!subject.Completion.IsCompleted)
        {
            Assert.True(DateTime.UtcNow < deadline, "The subject game did not finish in time.");
            var snap = subject.Snapshot;
            if (snap.Lifecycle != TableLifecycle.InProgress)
            {
                await Task.Delay(1);
                continue;
            }

            if (snap.Version >= cut && subject.ControllerOf(Seat.South) is HumanPrincipal)
            {
                await subject.SetControllerAsync(Seat.South, new BotPrincipal(Seat.South));
                continue;
            }

            if (snap.State!.ToAct == Seat.South && subject.ControllerOf(Seat.South) is HumanPrincipal)
            {
                var view = Projection.For(snap.State, Seat.South);
                var action = await player.DecideAsync(view, CancellationToken.None);
                var outcome = await subject.SubmitAsync(new Intent(Seat.South, action, Human));
                Assert.IsType<IntentAccepted>(outcome);
                continue;
            }

            await Task.Delay(1);
        }

        var actual = await subject.Completion;
        Assert.NotNull(actual);
        Assert.Equal(expected!.Winner, actual!.Winner);
        Assert.Equal(expected.NorthSouth, actual.NorthSouth);
        Assert.Equal(expected.EastWest, actual.EastWest);
        Assert.Equal(
            expected.Log.Events.Select(GameLog.EventLine).ToArray(),
            actual.Log.Events.Select(GameLog.EventLine).ToArray());
    }
}
