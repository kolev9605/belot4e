using System;
using System.Threading;
using System.Threading.Tasks;
using Belote.Core;

namespace Belote.Bots;

/// <summary>
/// Baseline bot: at every decision picks uniformly at random from the legal actions of its seat.
/// It sees only its <see cref="SeatView"/> and draws all randomness from its own seeded generator.
/// </summary>
public sealed class RandomLegalBot : IPlayer
{
    private readonly SeededRandom _random;

    public RandomLegalBot(ulong seed)
    {
        _random = new SeededRandom(seed);
    }

    public ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken)
    {
        var legal = LegalActions.For(view);
        if (legal.Count == 0)
        {
            throw new InvalidOperationException($"{view.Seat} was asked to act but has no legal action.");
        }

        return new ValueTask<PlayerAction>(legal[_random.NextInt(legal.Count)]);
    }
}
