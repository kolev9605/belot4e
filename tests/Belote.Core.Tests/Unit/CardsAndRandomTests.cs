using System.Linq;
using Belote.Core;
using Xunit;

namespace Belote.Core.Tests;

public class CardsTests
{
    [Fact]
    public void R1_Deck_has_32_unique_cards_in_four_suits_of_eight()
    {
        var deck = Deck.Standard32;
        Assert.Equal(32, deck.Length);
        Assert.Equal(32, deck.Distinct().Count());
        Assert.All(Enum.GetValues<Suit>(), s => Assert.Equal(8, deck.Count(c => c.Suit == s)));
    }

    [Theory]
    [InlineData("JH", Suit.Hearts, Rank.Jack)]
    [InlineData("10S", Suit.Spades, Rank.Ten)]
    [InlineData("7C", Suit.Clubs, Rank.Seven)]
    [InlineData("AD", Suit.Diamonds, Rank.Ace)]
    public void Card_notation_round_trips(string text, Suit suit, Rank rank)
    {
        var card = Card.Parse(text);
        Assert.Equal(new Card(suit, rank), card);
        Assert.Equal(text, card.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1H")]
    [InlineData("JX")]
    [InlineData("JHH")]
    public void Invalid_card_text_is_rejected(string text)
    {
        Assert.False(Card.TryParse(text, out _));
        Assert.Throws<FormatException>(() => Card.Parse(text));
    }

    [Fact]
    public void R1_Partners_sit_opposite_and_teams_alternate_around_the_table()
    {
        Assert.Equal(Seat.South, Seat.North.Partner());
        Assert.Equal(Seat.West, Seat.East.Partner());
        Assert.Equal(Team.NorthSouth, Seat.North.GetTeam());
        Assert.Equal(Team.NorthSouth, Seat.South.GetTeam());
        Assert.Equal(Team.EastWest, Seat.East.GetTeam());
        Assert.Equal(Team.EastWest, Seat.West.GetTeam());
        Assert.Equal(Seat.North, Seat.West.Next());
        Assert.Equal(Seat.West, Seat.North.After(-1));
    }
}

public class SeededRandomTests
{
    [Fact]
    public void SplitMix64_matches_the_published_reference_vector_for_seed_zero()
    {
        var sm = new SplitMix64(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, sm.Next());
        Assert.Equal(0x6E789E6AA1B965F4UL, sm.Next());
        Assert.Equal(0x06C45D188009454FUL, sm.Next());
    }

    [Fact]
    public void Output_sequence_is_pinned_so_algorithm_drift_breaks_golden_logs_loudly()
    {
        var rng = new SeededRandom(42);
        var actual = Enumerable.Range(0, 4).Select(_ => rng.NextULong()).ToArray();
        Assert.Equal(
            new ulong[]
            {
                PinnedFirst,
                PinnedSecond,
                PinnedThird,
                PinnedFourth,
            },
            actual);
    }

    // Verified against an independent implementation of xoshiro256** seeded through SplitMix64 (seed 42),
    // which itself reproduces the published reference outputs for state {1, 2, 3, 4}.
    private const ulong PinnedFirst = 1546998764402558742UL;
    private const ulong PinnedSecond = 6990951692964543102UL;
    private const ulong PinnedThird = 12544586762248559009UL;
    private const ulong PinnedFourth = 17057574109182124193UL;

    [Fact]
    public void Same_seed_gives_same_sequence_and_different_seeds_differ()
    {
        var a = new SeededRandom(7);
        var b = new SeededRandom(7);
        var c = new SeededRandom(8);
        var left = Enumerable.Range(0, 20).Select(_ => a.NextULong()).ToArray();
        var same = Enumerable.Range(0, 20).Select(_ => b.NextULong()).ToArray();
        var other = Enumerable.Range(0, 20).Select(_ => c.NextULong()).ToArray();
        Assert.Equal(left, same);
        Assert.NotEqual(left, other);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(32)]
    public void NextInt_stays_in_range_and_covers_every_value(int bound)
    {
        var rng = new SeededRandom(123);
        var seen = new HashSet<int>();
        for (int i = 0; i < 5000; i++)
        {
            int v = rng.NextInt(bound);
            Assert.InRange(v, 0, bound - 1);
            seen.Add(v);
        }

        Assert.Equal(bound, seen.Count);
    }

    [Fact]
    public void NextInt_is_roughly_uniform()
    {
        var rng = new SeededRandom(99);
        var counts = new int[6];
        for (int i = 0; i < 60000; i++)
        {
            counts[rng.NextInt(6)]++;
        }

        Assert.All(counts, c => Assert.InRange(c, 9000, 11000));
    }

    [Fact]
    public void Derive_gives_independent_repeatable_streams_without_advancing_the_parent()
    {
        var rng = new SeededRandom(5);
        var first = rng.Derive(1).NextULong();
        var again = rng.Derive(1).NextULong();
        var other = rng.Derive(2).NextULong();
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.Equal(new SeededRandom(5).NextULong(), rng.NextULong());
    }

    [Fact]
    public void Shuffle_is_a_deterministic_permutation()
    {
        var deck = Deck.Standard32.ToArray();
        var one = (Card[])deck.Clone();
        var two = (Card[])deck.Clone();
        new SeededRandom(11).Shuffle(one);
        new SeededRandom(11).Shuffle(two);
        Assert.Equal(one, two);
        Assert.NotEqual(deck, one);
        Assert.Equal(deck.OrderBy(c => c), one.OrderBy(c => c));
    }

    [Fact]
    public void Seeds_are_stable_and_distinct_per_game_deal_and_seat()
    {
        Assert.Equal(Seeds.ForGame(1, 0), Seeds.ForGame(1, 0));
        Assert.NotEqual(Seeds.ForGame(1, 0), Seeds.ForGame(1, 1));
        Assert.NotEqual(Seeds.ForGame(1, 0), Seeds.ForGame(2, 0));
        Assert.NotEqual(Seeds.ForHand(9, 1), Seeds.ForHand(9, 2));
        Assert.Equal(4, Seats.All.Select(s => Seeds.ForSeat(9, s)).Distinct().Count());
    }
}
