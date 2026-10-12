# Contract: `Belote.Core` public API

The stable surface other projects (bots, simulation, and later the server) rely on.
Signatures show shape and semantics; exact C# syntax is settled during implementation.
Everything here is pure: no I/O, no clock, no ambient randomness.

## Engine

```text
GameEngine.Start(RuleSet ruleSet, ulong seed) -> GameStart { GameState State, IReadOnlyList<GameEvent> Events }

GameEngine.Apply(GameState state, Seat seat, PlayerAction action)
    -> ApplyResult = Accepted { GameState State, IReadOnlyList<GameEvent> Events }
                   | Rejected { RejectionReason Reason, string Detail }

LegalActions.Check(SeatView view, PlayerAction action) -> RejectionReason?   // null when legal
```

Guarantees:
- **Deterministic**: same `(ruleSet, seed, sequence of (seat, action))` gives identical
  states and events.
- **Atomic**: `Rejected` leaves the caller's `state` untouched (it is immutable) and emits no
  events.
- **Seat-blind**: the engine takes a `Seat` and an action; it never knows what controls the
  seat.
- **Turn-checked**: an action from a seat that is not `ToAct`, or invalid in the current
  phase, is `Rejected` (`NotYourTurn`, `WrongPhase`).
- **Auto-advance**: when a trick, hand, or match boundary is reached, the returned state is
  already advanced (next hand dealt, or `Phase = Finished`), and the transition events are
  included in the same `Accepted` result.

`RejectionReason` values (language-neutral codes): `NotYourTurn`, `WrongPhase`,
`BidNotHigher`, `BidNotAllowed`, `ContraNotAllowed`, `RecontraNotAllowed`, `CardNotInHand`,
`MustFollowSuit`, `MustTrump`, `MustOvertrump`, `AnnouncementNotInHand`,
`AnnouncementNotAllowed`, `AnnouncementWindowClosed`, `GameFinished`.

## Views and legal actions

```text
Projection.For(GameState state, Seat seat) -> SeatView
LegalActions.For(SeatView view) -> IReadOnlyList<PlayerAction>   // empty iff view.ToAct != view.Seat
```

- `SeatView` contains only information the seat is entitled to see (see
  [data-model.md](../data-model.md)).
- `LegalActions.For` is the same function the engine uses to validate; therefore
  *accepted if and only if in the legal set* for every action kind.
- Announcement actions are enumerated as the concrete set of valid announcements the seat may
  declare at that moment (possibly empty, since declaring is optional). They are offered before
  the card actions and can be taken any number of times (disjoint cards) before playing a card.
- `GamePointConverter.Pool/Split` expose the game-point rounding used by scoring.

## Seat interface

```text
interface IPlayer {
    ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken ct)
}
```

Contract for implementers (bots, scripted controllers, later human/network seats):
- Receives **only** a `SeatView`; must not require or obtain full game state.
- Returns exactly one action per call; the runner re-calls with the same (or updated) view
  after a rejection.
- Must be safe to call from any thread, one call at a time per seat instance.

## Randomness

```text
SeededRandom(ulong seed)           // deterministic; algorithm fixed and documented
SeededRandom.NextInt(int bound)    // unbiased
SeededRandom.Derive(ulong salt)    // child generator for independent streams
Seeds.ForGame(ulong baseSeed, int gameIndex) -> ulong
Seeds.ForHand(ulong gameSeed, int dealNumber) -> ulong
Seeds.ForSeat(ulong gameSeed, Seat seat) -> ulong
Seeds.ForDealer(ulong gameSeed) -> ulong
```

`SeededRandom` output sequences are part of the compatibility contract: changing the
algorithm is a breaking change and invalidates golden logs.

## Rule sets

```text
RuleSets.Bulgarian : RuleSet
RuleSetValidator.Validate(RuleSet) -> IReadOnlyList<RuleSetIssue>   // empty when valid
RuleSetValidator.ThrowIfInvalid(RuleSet)                            // ArgumentException listing all issues
```

See [rule-set.md](rule-set.md). `GameEngine.Start` throws `ArgumentException` for an invalid
rule set.

## Non-goals of this API (this feature)

Serialization, persistence, networking, timers, and UI. JSON is handled in
`Belote.Simulation`.
