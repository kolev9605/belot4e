using System.Threading;
using System.Threading.Tasks;

namespace Belote.Core;

/// <summary>An intent submitted by a seat. The engine validates every action.</summary>
public abstract record PlayerAction;

public sealed record PassAction : PlayerAction;

public sealed record BidAction(ContractKind Kind) : PlayerAction;

public sealed record ContraAction : PlayerAction;

public sealed record RecontraAction : PlayerAction;

public sealed record AnnounceAction(AnnouncementSpec Spec) : PlayerAction;

public sealed record PlayCardAction(Card Card) : PlayerAction;

/// <summary>
/// The seat interface. Humans, scripted controllers and bots all implement it and receive only a
/// <see cref="SeatView"/>: never full game state.
/// </summary>
public interface IPlayer
{
    ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken);
}

/// <summary>Language-neutral reason codes for rejected actions.</summary>
public enum RejectionReason
{
    NotYourTurn,
    WrongPhase,
    BidNotHigher,
    BidNotAllowed,
    ContraNotAllowed,
    RecontraNotAllowed,
    CardNotInHand,
    MustFollowSuit,
    MustTrump,
    MustOvertrump,
    AnnouncementNotInHand,
    AnnouncementNotAllowed,
    AnnouncementWindowClosed,
    GameFinished,
}
