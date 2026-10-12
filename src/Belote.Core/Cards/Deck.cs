using System.Collections.Immutable;

namespace Belote.Core;

public static class Deck
{
    /// <summary>The 32-card deck (7 through Ace in four suits), ordered by suit then rank.</summary>
    public static ImmutableArray<Card> Standard32 { get; } = Build();

    private static ImmutableArray<Card> Build()
    {
        var builder = ImmutableArray.CreateBuilder<Card>(32);
        for (int s = 0; s < 4; s++)
        {
            for (int r = 0; r < 8; r++)
            {
                builder.Add(new Card((Suit)s, (Rank)r));
            }
        }

        return builder.MoveToImmutable();
    }
}
