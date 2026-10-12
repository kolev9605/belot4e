using Belote.Bots;
using Belote.Core;
using Belote.Simulation;
using Xunit;

namespace Belote.Simulation.Tests;

public class RandomLegalBotTests
{
    [Fact]
    public async Task The_bot_only_ever_chooses_legal_actions_and_never_gets_rejected()
    {
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var state = GameEngine.Start(RuleSets.Bulgarian, seed).State;
            var bots = Seats.All.ToDictionary(s => s, s => new RandomLegalBot(Seeds.ForSeat(seed, s)));
            int steps = 0;
            while (state.Phase != Phase.Finished)
            {
                Assert.True(++steps < 20000);
                var seat = state.ToAct;
                var view = Projection.For(state, seat);
                var action = await bots[seat].DecideAsync(view, default);

                Assert.Contains(action, LegalActions.For(view));
                state = Assert.IsType<Accepted>(GameEngine.Apply(state, seat, action)).State;
            }
        }
    }

    [Fact]
    public async Task The_same_bot_seed_and_views_give_the_same_choices_and_other_seeds_differ()
    {
        var state = GameEngine.Start(RuleSets.Bulgarian, 5).State;
        var a = new RandomLegalBot(11);
        var b = new RandomLegalBot(11);
        var c = new RandomLegalBot(12);
        var fromA = new List<PlayerAction>();
        var fromB = new List<PlayerAction>();
        var fromC = new List<PlayerAction>();

        for (int i = 0; i < 40 && state.Phase != Phase.Finished; i++)
        {
            var view = Projection.For(state, state.ToAct);
            fromA.Add(await a.DecideAsync(view, default));
            fromB.Add(await b.DecideAsync(view, default));
            fromC.Add(await c.DecideAsync(view, default));
            state = Assert.IsType<Accepted>(GameEngine.Apply(state, state.ToAct, fromA[^1])).State;
        }

        Assert.Equal(fromA, fromB);
        Assert.NotEqual(fromA, fromC);
    }

    [Fact]
    public async Task The_bot_covers_every_kind_of_decision_over_many_games()
    {
        var seen = new HashSet<Type>();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var run = await Sim.RunGameAsync(seed);
            foreach (var e in run.Log.Events)
            {
                switch (e)
                {
                    case BidMade: seen.Add(typeof(BidAction)); break;
                    case PassMade: seen.Add(typeof(PassAction)); break;
                    case ContraMade: seen.Add(typeof(ContraAction)); break;
                    case RecontraMade: seen.Add(typeof(RecontraAction)); break;
                    case AnnouncementDeclared: seen.Add(typeof(AnnounceAction)); break;
                    case CardPlayed: seen.Add(typeof(PlayCardAction)); break;
                }
            }
        }

        Assert.Equal(6, seen.Count);
    }

    [Fact]
    public void The_bot_assembly_depends_only_on_the_core_interface()
    {
        var references = typeof(RandomLegalBot).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.Contains("Belote.Core", references);
        Assert.DoesNotContain("Belote.Simulation", references);
    }

    [Fact]
    public async Task A_bot_asked_when_it_is_not_its_turn_has_nothing_to_choose()
    {
        var state = GameEngine.Start(RuleSets.Bulgarian, 5).State;
        var notToAct = state.ToAct.Next();
        var bot = new RandomLegalBot(1);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await bot.DecideAsync(Projection.For(state, notToAct), default));
    }
}
