using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

public class FullGameTests
{
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(42UL)]
    public async Task FR005_FR006_FR007_SeatedClientPlaysSeededGameToAWinner(ulong seed)
    {
        await using var table = Harness.NewTable(seed);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);

        var outcome = await Harness.PlayToEndAsync(table, recorder, Policies.First);

        Assert.Empty(outcome.Rejections);
        Assert.NotNull(outcome.Finished);
        var snap = table.Snapshot;
        Assert.Equal(TableLifecycle.Finished, snap.Lifecycle);
        Assert.Equal(Phase.Finished, snap.State!.Phase);
        Assert.NotNull(snap.State.Winner);
        int target = snap.State.RuleSet.Match.TargetScore;
        Assert.True(Math.Max(snap.State.MatchScore[0], snap.State.MatchScore[1]) >= target);
        Assert.Equal(snap.State.Winner, outcome.Finished!.Winner);

        // Once finished nothing more is accepted.
        var late = await table.SubmitAsync(Harness.Client, new IntentDto("pass", "south"));
        var rejected = Assert.IsType<IntentRejected>(late);
        Assert.Equal(RejectionCategory.GameNotInProgress, rejected.Rejection.Category);
        Assert.Equal("GameFinished", rejected.Rejection.Code);
    }

    [Fact]
    public async Task FR005_TakeSeatStartsTheGameAtVersionZero()
    {
        await using var table = Harness.NewTable(7);
        Assert.Equal(TableLifecycle.AwaitingSeat, table.Snapshot.Lifecycle);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);

        var seated = await table.TakeSeatAsync(Harness.Client);

        Assert.True(seated.Ok);
        Assert.Equal(0, seated.Version);
        Assert.Equal(TableLifecycle.InProgress, table.Snapshot.Lifecycle);
        var first = await recorder.Reader.ReadAsync();
        Assert.Equal(0, first.Version);
        Assert.Equal("south", first.View.Seat);
    }
}
