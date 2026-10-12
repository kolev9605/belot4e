using Microsoft.Extensions.Time.Testing;
using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

public class PacingAndPauseTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "Condition was not reached in time.");
            await Task.Delay(2);
        }
    }

    private static async Task Settle() => await Task.Delay(40);

    private static async Task<(LiveTable Table, Recorder Recorder, FakeTimeProvider Time)> SeatedAtBotTurnAsync()
    {
        for (ulong seed = 1; seed < 60; seed++)
        {
            var time = new FakeTimeProvider();
            var table = Harness.NewTable(seed, Delay, time);
            var recorder = new Recorder();
            table.Subscribe(recorder);
            await table.TakeSeatAsync(Harness.Client);
            if (table.Snapshot.State!.ToAct != Seat.South)
            {
                return (table, recorder, time);
            }

            await table.DisposeAsync();
        }

        throw new InvalidOperationException("No seed starts with a bot to act.");
    }

    [Fact]
    public async Task SC007_BotMovesWaitForTheConfiguredDelay()
    {
        var (table, recorder, time) = await SeatedAtBotTurnAsync();
        await using var _ = table;

        int botMoves = 0;
        while (table.Snapshot.State!.ToAct != Seat.South && botMoves < 6)
        {
            int version = table.Snapshot.Version;
            await Settle();
            Assert.Equal(version, table.Snapshot.Version);

            time.Advance(Delay - TimeSpan.FromMilliseconds(1));
            await Settle();
            Assert.Equal(version, table.Snapshot.Version);

            time.Advance(TimeSpan.FromMilliseconds(1));
            await WaitForAsync(() => table.Snapshot.Version == version + 1);
            botMoves++;
        }

        Assert.True(botMoves >= 1);
    }

    [Fact]
    public async Task ZeroDelayFinishesAWholeGameWithoutTheClockMoving()
    {
        var time = new FakeTimeProvider();
        await using var table = Harness.NewTable(3, TimeSpan.Zero, time);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);
        var start = time.GetUtcNow();

        var outcome = await Harness.PlayToEndAsync(table, recorder, Policies.First);

        Assert.NotNull(outcome.Finished);
        Assert.Equal(start, time.GetUtcNow());
    }

    [Fact]
    public void BotDelayIsClampedToZeroToFiveSeconds()
    {
        Assert.Equal(TimeSpan.Zero, new TableOptions { BotDelay = TimeSpan.FromSeconds(-3) }.BotDelay);
        Assert.Equal(TimeSpan.FromSeconds(5), new TableOptions { BotDelay = TimeSpan.FromMinutes(2) }.BotDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(250), new TableOptions { BotDelay = TimeSpan.FromMilliseconds(250) }.BotDelay);
    }

    [Fact]
    public async Task FR026_DetachedTableDoesNotAdvanceAndResumesFromTheSameState()
    {
        var (table, recorder, time) = await SeatedAtBotTurnAsync();
        await using var _ = table;
        var before = table.Snapshot;
        int updates = recorder.Updates.Count;

        await table.DetachAsync(Harness.Client);
        time.Advance(TimeSpan.FromSeconds(30));
        await Settle();

        var paused = table.Snapshot;
        Assert.True(paused.Paused);
        Assert.Equal(before.Version, paused.Version);
        Assert.Same(before.State, paused.State);
        Assert.Equal(updates, recorder.Updates.Count);

        var refused = await table.SubmitAsync(Harness.Client, new IntentDto("pass", "south"));
        Assert.Equal(RejectionCategory.Paused, Assert.IsType<IntentRejected>(refused).Rejection.Category);

        await table.AttachAsync(Harness.Client);

        // The driver registers its delay asynchronously after resuming, so keep advancing the clock.
        var until = DateTime.UtcNow.AddSeconds(5);
        while (table.Snapshot.Version == before.Version)
        {
            Assert.True(DateTime.UtcNow < until, "The table did not resume.");
            time.Advance(Delay);
            await Task.Delay(5);
        }

        Assert.False(table.Snapshot.Paused);
    }

    [Fact]
    public async Task FR026_PauseDuringADelayedMoveNeverAppliesIt()
    {
        var (table, recorder, time) = await SeatedAtBotTurnAsync();
        await using var _ = table;
        var before = table.Snapshot;

        // The delay elapses at the same moment the client detaches.
        await table.DetachAsync(Harness.Client);
        time.Advance(Delay);
        await Settle();

        Assert.Equal(before.Version, table.Snapshot.Version);
        Assert.Single(recorder.Updates);
    }

    [Fact]
    public async Task FR026_PausingMidGameDoesNotChangeTheGame()
    {
        const ulong seed = 21;

        // Baseline: uninterrupted, zero delay.
        await using var baseline = Harness.NewTable(seed);
        var baselineRecorder = new Recorder();
        using var s1 = baseline.Subscribe(baselineRecorder);
        await Harness.PlayToEndAsync(baseline, baselineRecorder, Policies.SeededSouth(seed));
        var expected = Lines.Of(baseline.Snapshot.Events);

        // Same game paced by a fake clock, detached and re-attached in the middle.
        var time = new FakeTimeProvider();
        await using var table = Harness.NewTable(seed, TimeSpan.FromMilliseconds(100), time);
        var recorder = new Recorder();
        using var s2 = table.Subscribe(recorder);
        var policy = Policies.SeededSouth(seed);
        await table.TakeSeatAsync(Harness.Client);

        int steps = 0;
        bool paused = false;
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!table.Completion.IsCompleted)
        {
            Assert.True(DateTime.UtcNow < deadline, "Game did not finish.");
            time.Advance(TimeSpan.FromMilliseconds(100));
            while (recorder.Reader.TryRead(out var update))
            {
                if (update.View.LegalActions.Count > 0)
                {
                    var outcome = await table.SubmitAsync(Harness.Client, policy(update));
                    Assert.IsType<IntentAccepted>(outcome);
                }
            }

            await Task.Delay(1);
            if (++steps == 40 && !paused && table.Snapshot.Lifecycle == TableLifecycle.InProgress)
            {
                paused = true;
                var frozen = table.Snapshot;
                await table.DetachAsync(Harness.Client);
                time.Advance(TimeSpan.FromSeconds(20));
                await Task.Delay(60);
                Assert.Equal(frozen.Version, table.Snapshot.Version);
                await table.AttachAsync(Harness.Client);
            }
        }

        Assert.True(paused, "The pause was never exercised.");
        Assert.Equal(expected, Lines.Of(table.Snapshot.Events));
    }
}
