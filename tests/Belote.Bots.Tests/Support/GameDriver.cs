using Belote.Core;
using Xunit;

namespace Belote.Bots.Tests;

internal static class GameDriver
{
    /// <summary>Plays a whole game, asserting every chosen action is legal; returns the final state.</summary>
    public static async Task<GameState> PlayAsync(RuleSet rules, ulong seed, Func<Seat, IPlayer> controllers)
    {
        var state = GameEngine.Start(rules, seed).State;
        var players = Seats.All.ToDictionary(s => s, controllers);
        int steps = 0;
        while (state.Phase != Phase.Finished)
        {
            Assert.True(++steps < 20000, $"game {seed} did not finish");
            var seat = state.ToAct;
            var view = Projection.For(state, seat);
            var action = await players[seat].DecideAsync(view, default);
            Assert.Contains(action, LegalActions.For(view));
            state = Assert.IsType<Accepted>(GameEngine.Apply(state, seat, action)).State;
        }

        return state;
    }
}
