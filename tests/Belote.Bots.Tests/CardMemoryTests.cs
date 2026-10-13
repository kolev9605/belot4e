using System.Collections.Immutable;
using Belote.Core;
using Xunit;

namespace Belote.Bots.Tests;

public class CardMemoryTests
{
    private static readonly RuleSet Rules = RuleSets.Bulgarian;

    [Fact]
    public async Task Unseen_played_and_own_cards_always_partition_the_deck()
    {
        var bot = new SmartBot();
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var state = GameEngine.Start(Rules, seed).State;
            while (state.Phase != Phase.Finished)
            {
                var seat = state.ToAct;
                var view = Projection.For(state, seat);
                if (view.Phase == Phase.Playing)
                {
                    var memory = CardMemory.From(view);
                    Assert.Equal(32, memory.Unseen.Length + memory.Played.Count + view.OwnHand.Length);
                    Assert.Empty(memory.Unseen.Intersect(view.OwnHand));
                    Assert.Empty(memory.Unseen.Where(memory.Played.Contains));
                }

                var action = await bot.DecideAsync(view, default);
                state = Assert.IsType<Accepted>(GameEngine.Apply(state, seat, action)).State;
            }
        }
    }

    [Fact]
    public void A_seat_that_discards_off_suit_is_void_in_the_led_suit_only()
    {
        var trick = V.Trick(Rules, ContractKind.Hearts,
            (Seat.North, "7S"), (Seat.East, "8D"), (Seat.South, "AS"), (Seat.West, "9S"));
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "7C 8C 9C 10C JC QC KC AC",
            completed: ImmutableArray.Create(trick));

        var memory = CardMemory.From(view);

        Assert.True(memory.IsVoid(Seat.East, Suit.Spades));
        Assert.False(memory.IsVoid(Seat.East, Suit.Diamonds));
        Assert.False(memory.IsVoid(Seat.East, Suit.Hearts));
        Assert.False(memory.IsVoid(Seat.West, Suit.Spades));
    }

    [Fact]
    public void Master_follows_the_contracts_strength_order_and_the_played_cards()
    {
        // Suit contract hearts: plain order A 10 K ...; trump order J 9 A 10 ...
        Assert.True(CardMemory.From(V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "AS 7C 8C 9C 10C JC QC KC"))
            .IsMaster(Card.Parse("AS")));
        var notMaster = CardMemory.From(V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "10S 7C 8C 9C 10C JC QC KC"));
        Assert.False(notMaster.IsMaster(Card.Parse("10S")));

        var jack = CardMemory.From(V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "JH 9H 7C 8C 9C 10C JC QC"));
        Assert.True(jack.IsMaster(Card.Parse("JH")));
        var nineBehindJack = CardMemory.From(V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "9H 7C 8C 9C 10C JC QC KC"));
        Assert.False(nineBehindJack.IsMaster(Card.Parse("9H")));

        // After the ace of spades has been played the ten becomes the master.
        var trick = V.Trick(Rules, ContractKind.NoTrump,
            (Seat.North, "AS"), (Seat.East, "7S"), (Seat.South, "8S"), (Seat.West, "9S"));
        var after = CardMemory.From(V.Playing(Seat.North, ContractKind.NoTrump, Seat.East, "10S 7C 8C 9C 10C JC QC KC",
            completed: ImmutableArray.Create(trick)));
        Assert.True(after.IsMaster(Card.Parse("10S")));

        // All trump: the jack is the master of its suit, the ace is not.
        Assert.True(CardMemory.From(V.Playing(Seat.South, ContractKind.AllTrump, Seat.East, "JS 7C 8C 9C 10C JC QC KC"))
            .IsMaster(Card.Parse("JS")));
        Assert.False(CardMemory.From(V.Playing(Seat.South, ContractKind.AllTrump, Seat.East, "AS 7C 8C 9C 10C JC QC KC"))
            .IsMaster(Card.Parse("AS")));
    }

    [Fact]
    public void A_master_is_not_safe_when_an_opponent_is_void_and_trumps_remain()
    {
        var trick = V.Trick(Rules, ContractKind.Hearts,
            (Seat.South, "7D"), (Seat.West, "8S"), (Seat.North, "8D"), (Seat.East, "9D"));
        var view = V.Playing(Seat.South, ContractKind.Hearts, Seat.East, "AD 7C 8C 9C 10C JC QC KC",
            completed: ImmutableArray.Create(trick));
        var memory = CardMemory.From(view);

        Assert.True(memory.IsMaster(Card.Parse("AD")));
        Assert.True(memory.IsVoid(Seat.West, Suit.Diamonds));
        Assert.False(memory.IsSafeMaster(Card.Parse("AD")));
    }
}
