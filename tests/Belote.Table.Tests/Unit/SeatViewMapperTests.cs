using Belote.Core;
using Belote.Simulation;
using Belote.Table;

namespace Belote.Table.Tests;

public class SeatViewMapperTests
{
    [Theory]
    [InlineData(5UL)]
    [InlineData(6UL)]
    public async Task MappedViewsUseLogVocabularyAndEngineOrderedLegalActions(ulong seed)
    {
        await using var table = Harness.NewTable(seed);
        var recorder = new Recorder();
        using var _ = table.Subscribe(recorder);
        await Harness.PlayToEndAsync(table, recorder, Policies.First);

        var snapshotEvents = table.Snapshot.Events;
        Assert.NotEmpty(recorder.Updates);
        foreach (var update in recorder.Updates)
        {
            var view = update.View;
            Assert.Equal("south", view.Seat);
            Assert.Contains(view.Phase, new[] { "bidding", "playing", "finished" });
            Assert.All(view.OwnHand, c => Assert.True(Card.TryParse(c, out var card) && card.ToString() == c));
            if (view.ToAct == "south" && !update.Finished)
            {
                Assert.NotEmpty(view.LegalActions);
            }
            else
            {
                Assert.Empty(view.LegalActions);
            }

            Assert.All(view.LegalActions, a => Assert.Equal("south", a.Seat));
        }

        Assert.NotEmpty(snapshotEvents);
    }

    [Fact]
    public void LegalActionsFollowLegalActionsForOrderAndOwnHandMatches()
    {
        var start = GameEngine.Start(RuleSets.Bulgarian, 99);
        var state = start.State;
        var seat = state.ToAct;
        var view = Projection.For(state, seat);

        var dto = SeatViewMapper.Map(view, null);

        var expected = LegalActions.For(view).Select(a => LegalActionMapper.ToDto(seat, a)).ToArray();
        Assert.Equal(expected, dto.LegalActions);
        Assert.Equal(view.OwnHand.Select(c => c.ToString()).OrderBy(c => c), dto.OwnHand.OrderBy(c => c));
        Assert.Equal(JsonNames.Of(seat), dto.Seat);
        Assert.Equal("bidding", dto.Phase);
    }

    [Fact]
    public void NotMyTurnHasNoLegalActions()
    {
        var state = GameEngine.Start(RuleSets.Bulgarian, 99).State;
        var other = state.ToAct.Next();
        var dto = SeatViewMapper.Map(Projection.For(state, other), null);
        Assert.Empty(dto.LegalActions);
    }
}
