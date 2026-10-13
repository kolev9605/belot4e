using System.Collections.Immutable;
using Belote.Core;
using FsCheck.Xunit;
using Xunit;

namespace Belote.Bots.Tests;

public class DeterminismAndLegalityTests
{
    [Fact]
    public async Task Equal_views_give_equal_actions_on_the_same_and_on_fresh_instances()
    {
        var shared = new SmartBot();
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var state = GameEngine.Start(RuleSets.Bulgarian, seed).State;
            while (state.Phase != Phase.Finished)
            {
                var seat = state.ToAct;
                var view = Projection.For(state, seat);
                var first = await shared.DecideAsync(view, default);
                Assert.Equal(first, await shared.DecideAsync(view, default));
                Assert.Equal(first, await new SmartBot().DecideAsync(view, default));
                state = Assert.IsType<Accepted>(GameEngine.Apply(state, seat, first)).State;
            }
        }
    }

    [Fact]
    public async Task Smart_bots_in_every_seat_finish_a_hundred_games_legally()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var final = await GameDriver.PlayAsync(RuleSets.Bulgarian, seed, _ => new SmartBot());
            Assert.NotNull(final.Winner);
        }
    }

    public static IEnumerable<object[]> Variants()
    {
        var b = RuleSets.Bulgarian;
        yield return new object[] { "no-doubling", b with { Bidding = b.Bidding with { Contra = false, Recontra = false } } };
        yield return new object[] { "no-announcements-in-suit", b with { Announcements = b.Announcements with { AllowedInSuit = false } } };
        yield return new object[] { "free-cutting", b with { Play = b.Play with { MustOvertrump = false, CutOnlyWhenOpponentWinning = false } } };
        yield return new object[]
        {
            "reversed-ranking",
            b with
            {
                Bidding = b.Bidding with
                {
                    Ranking = ImmutableArray.Create(
                        ContractKind.AllTrump, ContractKind.NoTrump, ContractKind.Spades,
                        ContractKind.Hearts, ContractKind.Diamonds, ContractKind.Clubs),
                },
            },
        };
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task Legality_holds_on_rule_set_variants(string name, RuleSet rules)
    {
        RuleSetValidator.ThrowIfInvalid(rules);
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var final = await GameDriver.PlayAsync(rules, seed, _ => new SmartBot());
            Assert.NotNull(final.Winner);
        }

        Assert.NotNull(name);
    }

    [Property(MaxTest = 40)]
    public void A_smart_seat_is_never_rejected_by_the_engine(ulong seed)
    {
        GameDriver.PlayAsync(RuleSets.Bulgarian, seed, _ => new SmartBot()).GetAwaiter().GetResult();
    }
}
