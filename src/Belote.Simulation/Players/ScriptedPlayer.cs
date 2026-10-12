using System;
using System.Threading;
using System.Threading.Tasks;
using Belote.Core;

namespace Belote.Simulation;

/// <summary>
/// A controller driven by a caller-supplied function; stands in for a "human" seat in headless tests.
/// It receives only the seat's <see cref="SeatView"/>, exactly like a bot.
/// </summary>
public sealed class ScriptedPlayer : IPlayer
{
    private readonly Func<SeatView, PlayerAction> _choose;

    public ScriptedPlayer(Func<SeatView, PlayerAction> choose)
    {
        _choose = choose;
    }

    public ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken) =>
        new(_choose(view));
}

/// <summary>
/// Decorator that submits one clearly illegal action before each real decision, to prove the engine
/// rejects it without changing state and that the controller is simply asked again.
/// </summary>
public sealed class IllegalFirstPlayer : IPlayer
{
    private readonly IPlayer _inner;
    private bool _illegalSent;

    public IllegalFirstPlayer(IPlayer inner)
    {
        _inner = inner;
    }

    public ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken)
    {
        if (!_illegalSent)
        {
            _illegalSent = true;
            return new ValueTask<PlayerAction>(IllegalFor(view));
        }

        _illegalSent = false;
        return _inner.DecideAsync(view, cancellationToken);
    }

    private static PlayerAction IllegalFor(SeatView view)
    {
        if (view.Phase == Phase.Bidding)
        {
            // Contra is illegal before any bid; otherwise the lowest bid cannot outrank a standing bid.
            return view.StandingBid is null
                ? new ContraAction()
                : new BidAction(view.RuleSet.Bidding.Ranking[0]);
        }

        foreach (var card in Deck.Standard32)
        {
            if (!view.OwnHand.Contains(card))
            {
                return new PlayCardAction(card);
            }
        }

        throw new InvalidOperationException("Hand cannot hold the whole deck.");
    }
}
