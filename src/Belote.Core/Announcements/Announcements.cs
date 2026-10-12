using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Belote.Core;

public enum AnnouncementKind
{
    Sequence,
    Carre,
}

/// <summary>
/// A concrete combination. For a sequence: <see cref="Suit"/> and top <see cref="Rank"/> with
/// <see cref="Length"/> consecutive cards going down from the top. For a four-of-a-kind: the rank
/// (the suit is meaningless and fixed to Clubs so equal combinations compare equal).
/// </summary>
public readonly record struct AnnouncementSpec(AnnouncementKind Kind, Suit Suit, Rank Rank, int Length)
{
    public static AnnouncementSpec Sequence(Suit suit, Rank top, int length) =>
        new(AnnouncementKind.Sequence, suit, top, length);

    public static AnnouncementSpec Carre(Rank rank) => new(AnnouncementKind.Carre, Suit.Clubs, rank, 4);

    public IReadOnlyList<Card> Cards()
    {
        var cards = new List<Card>(Length);
        if (Kind == AnnouncementKind.Sequence)
        {
            for (int i = 0; i < Length; i++)
            {
                cards.Add(new Card(Suit, (Rank)((int)Rank - i)));
            }
        }
        else
        {
            for (int s = 0; s < 4; s++)
            {
                cards.Add(new Card((Suit)s, Rank));
            }
        }

        return cards;
    }

    public int Points(AnnouncementRules rules)
    {
        if (Kind == AnnouncementKind.Carre)
        {
            return rules.CarrePoints[(int)Rank];
        }

        int points = 0;
        int best = -1;
        foreach (var value in rules.Sequences)
        {
            if (Length >= value.MinLength && value.MinLength > best)
            {
                best = value.MinLength;
                points = value.Points;
            }
        }

        return points;
    }

    public override string ToString() => Kind == AnnouncementKind.Carre
        ? $"carre-{Card.RankText(Rank)}"
        : $"seq{Length}-{Card.RankText(Rank)}{Card.SuitLetter(Suit)}";
}

public static class AnnouncementDetector
{
    /// <summary>All announceable combinations in a hand: maximal sequences and four-of-a-kinds.</summary>
    public static ImmutableArray<AnnouncementSpec> Detect(AnnouncementRules rules, IEnumerable<Card> hand)
    {
        var cards = hand.ToList();
        var result = ImmutableArray.CreateBuilder<AnnouncementSpec>();
        int minLength = rules.Sequences.IsDefaultOrEmpty ? int.MaxValue : rules.Sequences.Min(s => s.MinLength);

        for (int s = 0; s < 4; s++)
        {
            var suit = (Suit)s;
            var ranks = new bool[8];
            foreach (var card in cards)
            {
                if (card.Suit == suit)
                {
                    ranks[(int)card.Rank] = true;
                }
            }

            int runStart = -1;
            for (int r = 0; r <= 8; r++)
            {
                bool has = r < 8 && ranks[r];
                if (has && runStart < 0)
                {
                    runStart = r;
                }
                else if (!has && runStart >= 0)
                {
                    int length = r - runStart;
                    if (length >= minLength)
                    {
                        result.Add(AnnouncementSpec.Sequence(suit, (Rank)(r - 1), length));
                    }

                    runStart = -1;
                }
            }
        }

        for (int r = 0; r < 8; r++)
        {
            if (rules.CarrePoints[r] <= 0)
            {
                continue;
            }

            int count = cards.Count(c => (int)c.Rank == r);
            if (count == 4)
            {
                result.Add(AnnouncementSpec.Carre((Rank)r));
            }
        }

        return result.ToImmutable();
    }
}

/// <summary>Which team (if any) counts in each announcement category.</summary>
public readonly record struct AnnouncementOutcome(Team? SequenceTeam, Team? CarreTeam);

public static class AnnouncementComparer
{
    /// <summary>
    /// Sequences are compared among sequences (longer wins, then higher top card; identical means
    /// neither team counts) and four-of-a-kinds among four-of-a-kinds (by the configured order).
    /// The winning team of a category scores all of its announcements in that category.
    /// </summary>
    public static AnnouncementOutcome Compare(
        AnnouncementRules rules,
        IEnumerable<(Seat Seat, AnnouncementSpec Spec)> declared)
    {
        var list = declared.ToList();

        Team? sequenceTeam = Winner(
            list.Where(d => d.Spec.Kind == AnnouncementKind.Sequence),
            spec => spec.Length * 16 + (int)spec.Rank);

        Team? carreTeam = Winner(
            list.Where(d => d.Spec.Kind == AnnouncementKind.Carre),
            spec => 100 - rules.CarreOrder.IndexOf(spec.Rank));

        return new AnnouncementOutcome(sequenceTeam, carreTeam);
    }

    private static Team? Winner(
        IEnumerable<(Seat Seat, AnnouncementSpec Spec)> items,
        System.Func<AnnouncementSpec, int> key)
    {
        int? best0 = null;
        int? best1 = null;
        foreach (var (seat, spec) in items)
        {
            int k = key(spec);
            if (seat.GetTeam() == Team.NorthSouth)
            {
                best0 = best0 is null ? k : System.Math.Max(best0.Value, k);
            }
            else
            {
                best1 = best1 is null ? k : System.Math.Max(best1.Value, k);
            }
        }

        if (best0 is null && best1 is null)
        {
            return null;
        }

        if (best1 is null)
        {
            return Team.NorthSouth;
        }

        if (best0 is null)
        {
            return Team.EastWest;
        }

        if (best0 == best1)
        {
            return null;
        }

        return best0 > best1 ? Team.NorthSouth : Team.EastWest;
    }
}
