using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

public class GetViewAndOrderTests
{
    [Fact]
    public async Task FR018_GetViewEqualsTheLatestUpdateAndChangesNothing()
    {
        await using var table = Harness.NewTable(5, delay: TimeSpan.Zero);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);
        await table.TakeSeatAsync(Harness.Client);

        // Walk a few moves, checking GetView against the update for the same version each time.
        for (int step = 0; step < 30; step++)
        {
            var update = await recorder.Reader.ReadAsync();
            if (update.Finished)
            {
                break;
            }

            // The table may already have moved on (bots act with zero delay); only a view of the
            // same version is comparable with the pushed update.
            var view = table.GetView(Harness.Client);
            Assert.Null(view.Rejection);
            if (view.Update!.Version == update.Version)
            {
                Assert.Equal(Harness.Json(update), Harness.Json(view.Update));
            }
            else
            {
                Assert.True(view.Update.Version > update.Version);
            }

            if (update.View.LegalActions.Count > 0)
            {
                await table.SubmitAsync(Harness.Client, update.View.LegalActions[0]);
            }
        }
    }

    [Fact]
    public async Task FR018_RepeatedGetViewNeverChangesState()
    {
        await using var table = Harness.NewTable(5);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);
        await table.TakeSeatAsync(Harness.Client);
        var first = await recorder.Reader.ReadAsync();
        while (first.View.LegalActions.Count == 0)
        {
            first = await recorder.Reader.ReadAsync();
        }

        var before = table.Snapshot;
        int updates = recorder.Updates.Count;
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(Harness.Json(first), Harness.Json(table.GetView(Harness.Client).Update));
        }

        Assert.Same(before, table.Snapshot);
        Assert.Equal(updates, recorder.Updates.Count);
    }

    [Fact]
    public async Task GetView_IsRefusedForAStrangerAndAvailableAfterFinish()
    {
        await using var table = Harness.NewTable(6);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);
        Assert.Equal("GameNotStarted", table.GetView(Harness.Client).Rejection!.Code);
        Assert.Equal("NotSeated", table.GetView(new ConnectionPrincipal("other")).Rejection!.Code);

        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        var final = table.GetView(Harness.Client);
        Assert.Null(final.Rejection);
        Assert.True(final.Update!.Finished);
        Assert.Equal("finished", final.Update.View.Phase);
        Assert.NotNull(final.Update.View.Winner);
        Assert.Empty(final.Update.View.LegalActions);
    }

    [Fact]
    public async Task FR019_VersionsAreContiguousEvenWithASlowObserver()
    {
        await using var table = Harness.NewTable(7);
        var recorder = new Recorder(callbackDelay: TimeSpan.FromMilliseconds(1));
        using var _ = table.Subscribe(recorder);

        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        var versions = recorder.Updates.Select(u => u.Version).ToArray();
        Assert.Equal(Enumerable.Range(0, versions.Length), versions);
        Assert.Equal(table.Snapshot.Version, versions[^1]);
        Assert.All(recorder.Updates, u => Assert.Equal(TableUpdateDto.CurrentProtocolVersion, u.ProtocolVersion));
        Assert.Single(recorder.Updates, u => u.Finished);
    }

    [Fact]
    public async Task LegalActionsAppearExactlyOnSouthsTurn()
    {
        await using var table = Harness.NewTable(9);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);
        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        Assert.All(recorder.Updates, u =>
        {
            bool southToAct = u.View.ToAct == "south" && !u.Finished;
            Assert.Equal(southToAct, u.View.LegalActions.Count > 0);
        });
    }
}
