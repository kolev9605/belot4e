using Belote.Core;
using Belote.Simulation;
using Belote.Table;

namespace Belote.Table.Tests;

/// <summary>Feature 005 contract: OwnHand is ordered strongest-first within each suit for the active contract.</summary>
public class HandOrderTests
{
    private static Card[] Hand(string cards) => cards.Split(' ').Select(Card.Parse).ToArray();

    private static string Order(ContractKind? kind, string cards) =>
        string.Join(' ', SeatViewMapper.OrderHand(RuleSets.Bulgarian, kind, Hand(cards)).Select(c => c.ToString()));

    [Fact]
    public void SuitContractOrdersTrumpSuitByTrumpPowerAndOthersByPlainPower()
    {
        // Hearts are trumps: J 9 A 10 K Q 8 7. Other suits: A 10 K Q J 9 8 7.
        var ordered = SeatViewMapper.OrderHand(
            RuleSets.Bulgarian,
            ContractKind.Hearts,
            Hand("7H JH 9H 9S AS JS AH 10H 10S")).Select(c => c.ToString()).ToArray();

        var hearts = ordered.Where(c => c.EndsWith('H')).ToArray();
        var spades = ordered.Where(c => c.EndsWith('S')).ToArray();
        Assert.Equal(new[] { "JH", "9H", "AH", "10H", "7H" }, hearts);
        Assert.Equal(new[] { "AS", "10S", "JS", "9S" }, spades);
    }

    [Fact]
    public void NoTrumpUsesPlainOrder()
    {
        var spades = Order(ContractKind.NoTrump, "9S JS AS 10S").Split(' ');
        Assert.Equal(new[] { "AS", "10S", "JS", "9S" }, spades);
    }

    [Fact]
    public void AllTrumpUsesTrumpOrderInEverySuit()
    {
        var ordered = Order(ContractKind.AllTrump, "AS 9S JS 10S AD JD 9D").Split(' ');
        Assert.Equal(new[] { "JS", "9S", "AS", "10S" }, ordered.Where(c => c.EndsWith('S')).ToArray());
        Assert.Equal(new[] { "JD", "9D", "AD" }, ordered.Where(c => c.EndsWith('D')).ToArray());
    }

    [Fact]
    public void NoContractYetUsesTheNoTrumpOrder()
    {
        Assert.Equal(Order(ContractKind.NoTrump, "9S JS AS 10S"), Order(null, "9S JS AS 10S"));
    }

    [Fact]
    public void OrderIsDeterministicAndAPermutation()
    {
        var hand = Hand("7H JH 9S AS JS AH 10H 10S");
        var first = SeatViewMapper.OrderHand(RuleSets.Bulgarian, ContractKind.Spades, hand);
        var second = SeatViewMapper.OrderHand(RuleSets.Bulgarian, ContractKind.Spades, hand.Reverse());
        Assert.Equal(first, second);
        Assert.Equal(hand.OrderBy(c => c.ToString()), first.OrderBy(c => c.ToString()));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    public void MappedHandsAreStrengthOrderedWithinSuitAndKeepEverythingElse(ulong seed)
    {
        var rules = RuleSets.Bulgarian;
        var state = GameEngine.Start(rules, seed).State;
        for (var step = 0; step < 40 && state.Phase != Phase.Finished; step++)
        {
            var seat = state.ToAct;
            var view = Projection.For(state, seat);
            var dto = SeatViewMapper.Map(view, null);

            var mapped = dto.OwnHand.Select(Card.Parse).ToArray();
            Assert.Equal(
                view.OwnHand.Select(c => c.ToString()).OrderBy(s => s),
                dto.OwnHand.OrderBy(s => s));
            var kind = view.Contract?.Kind ?? ContractKind.NoTrump;
            foreach (var suit in mapped.Select(c => c.Suit).Distinct())
            {
                var strengths = mapped
                    .Where(c => c.Suit == suit)
                    .Select(c => CardStrength.Of(rules, kind, suit, c))
                    .ToArray();
                Assert.Equal(strengths.OrderByDescending(s => s), strengths);
            }

            var expectedLegal = LegalActions.For(view).Select(a => LegalActionMapper.ToDto(seat, a)).ToArray();
            Assert.Equal(expectedLegal, dto.LegalActions);

            var action = LegalActions.For(view).First();
            state = Assert.IsType<Accepted>(GameEngine.Apply(state, seat, action)).State;
        }
    }
}
