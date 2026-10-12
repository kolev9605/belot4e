using Belote.Core;
using Belote.Table;

namespace Belote.Table.Tests;

public class IntentParserTests
{
    [Fact]
    public void EveryLegalActionShapeRoundTrips()
    {
        var state = GameEngine.Start(RuleSets.Bulgarian, 1234).State;
        // Walk a game with the first legal action and check every offered action parses back unchanged.
        int checkedActions = 0;
        for (int i = 0; i < 400 && state.Phase != Phase.Finished; i++)
        {
            var seat = state.ToAct;
            var legal = LegalActions.For(Projection.For(state, seat));
            foreach (var action in legal)
            {
                var dto = LegalActionMapper.ToDto(seat, action);
                Assert.True(IntentParser.TryParse(dto, out var parsedSeat, out var parsed, out var rejection), rejection?.Detail);
                Assert.Equal(seat, parsedSeat);
                Assert.Equal(action, parsed);
                checkedActions++;
            }

            var result = GameEngine.Apply(state, seat, legal[legal.Count - 1]);
            state = ((Belote.Core.Accepted)result).State;
        }

        Assert.True(checkedActions > 100);
    }

    [Theory]
    [InlineData(null, "south", null, null)]
    [InlineData("", "south", null, null)]
    [InlineData("teleport", "south", null, null)]
    [InlineData("pass", null, null, null)]
    [InlineData("pass", "up", null, null)]
    [InlineData("pass", "South", null, null)]
    [InlineData("bid", "south", null, null)]
    [InlineData("bid", "south", "diamondz", null)]
    [InlineData("play", "south", null, null)]
    [InlineData("play", "south", null, "1S")]
    [InlineData("play", "south", null, "as")]
    [InlineData("play", "south", null, "AX")]
    public void MalformedIntentsAreRejectedNotThrown(string? kind, string? seat, string? contract, string? card)
    {
        var dto = new IntentDto(kind, seat, contract, null, card);

        var ok = IntentParser.TryParse(dto, out _, out _, out var rejection);

        Assert.False(ok);
        Assert.Equal(RejectionCategory.Malformed, rejection!.Category);
    }

    [Fact]
    public void NullIntentAndBadAnnouncementsAreMalformed()
    {
        Assert.False(IntentParser.TryParse(null, out _, out _, out var r1));
        Assert.Equal(RejectionCategory.Malformed, r1!.Category);

        foreach (var spec in new AnnouncementSpecDto?[]
        {
            null,
            new("sequence", null, "Q", 3),
            new("sequence", "hearts", "Z", 3),
            new("sequence", "hearts", "Q", 0),
            new("carre", null, "J", 3),
            new("run", "hearts", "Q", 3),
        })
        {
            Assert.False(IntentParser.TryParse(new IntentDto("announce", "south", Announcement: spec), out _, out _, out var r));
            Assert.Equal(RejectionCategory.Malformed, r!.Category);
        }
    }
}
