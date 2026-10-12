using System;

namespace Belote.Core;

public enum Suit
{
    Clubs = 0,
    Diamonds = 1,
    Hearts = 2,
    Spades = 3,
}

/// <summary>Ranks in natural (sequence) order: 7 8 9 10 J Q K A.</summary>
public enum Rank
{
    Seven = 0,
    Eight = 1,
    Nine = 2,
    Ten = 3,
    Jack = 4,
    Queen = 5,
    King = 6,
    Ace = 7,
}

public readonly record struct Card(Suit Suit, Rank Rank) : IComparable<Card>
{
    public int CompareTo(Card other)
    {
        int c = Suit.CompareTo(other.Suit);
        return c != 0 ? c : Rank.CompareTo(other.Rank);
    }

    public override string ToString() => RankText(Rank) + SuitLetter(Suit);

    public static string RankText(Rank rank) => rank switch
    {
        Rank.Seven => "7",
        Rank.Eight => "8",
        Rank.Nine => "9",
        Rank.Ten => "10",
        Rank.Jack => "J",
        Rank.Queen => "Q",
        Rank.King => "K",
        Rank.Ace => "A",
        _ => throw new ArgumentOutOfRangeException(nameof(rank)),
    };

    public static string SuitLetter(Suit suit) => suit switch
    {
        Suit.Clubs => "C",
        Suit.Diamonds => "D",
        Suit.Hearts => "H",
        Suit.Spades => "S",
        _ => throw new ArgumentOutOfRangeException(nameof(suit)),
    };

    public static bool TryParse(string? text, out Card card)
    {
        card = default;
        if (text is null || text.Length < 2 || text.Length > 3)
        {
            return false;
        }

        Suit suit;
        switch (char.ToUpperInvariant(text[^1]))
        {
            case 'C': suit = Suit.Clubs; break;
            case 'D': suit = Suit.Diamonds; break;
            case 'H': suit = Suit.Hearts; break;
            case 'S': suit = Suit.Spades; break;
            default: return false;
        }

        Rank rank;
        switch (text[..^1].ToUpperInvariant())
        {
            case "7": rank = Rank.Seven; break;
            case "8": rank = Rank.Eight; break;
            case "9": rank = Rank.Nine; break;
            case "10": rank = Rank.Ten; break;
            case "J": rank = Rank.Jack; break;
            case "Q": rank = Rank.Queen; break;
            case "K": rank = Rank.King; break;
            case "A": rank = Rank.Ace; break;
            default: return false;
        }

        card = new Card(suit, rank);
        return true;
    }

    public static Card Parse(string text) =>
        TryParse(text, out var card) ? card : throw new FormatException($"Not a card: '{text}'.");
}
