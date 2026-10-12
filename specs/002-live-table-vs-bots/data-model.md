# Data Model: Live Table vs Bots

Reuses the 001 types unchanged (`GameState`, `SeatView`, `PlayerAction`, `GameEvent`,
`GameLog`, `RuleSet`, `Seat`, `Card`). Only the table-level and wire-level types below are
new. Shapes are semantic; exact C# is settled during implementation.

## Table layer (`Belote.Table`)

| Entity | Fields | Notes |
|--------|--------|-------|
| `LiveTable` | `Id`, `Options`, `Lifecycle`, `Paused`, current `TableSnapshot`, event list, seat-to-principal map | Single writer (actor). At most one active per server. |
| `TableOptions` | `RuleSet` (default `Bulgarian`), `Seed` (ulong), `BotDelay` (TimeSpan, 0 to 5 s), `TimeProvider` | Seed is chosen by the server when the client omits it, then fixed for the table's life. |
| `Principal` | `ConnectionPrincipal(connectionId)` or `BotPrincipal(seat)` | Compared by equality only; the table never inspects the kind. |
| `Intent` | `Seat`, `PlayerAction`, `Principal` | Built from `IntentDto` after parsing. |
| `IntentOutcome` | `Accepted { Version }` or `Rejected { Rejection }` | Exactly one per submitted intent. |
| `Rejection` | `Category`, `Code`, `Detail` | See categories below. |
| `TableSnapshot` | `Version` (int, starts 0), `State` (`GameState`), `LastCompletedTrick?`, `Lifecycle` | Immutable; published atomically after each accepted intent. |
| `CompletedTrick` | `DealNumber`, `TrickNumber`, `Leader`, `Plays[4]`, `Winner` | Retained until the next card is played; survives hand rollover (FR-027). |
| `FinishedGame` | `GameLog`, `Winner`, `FinalScore` | Held server-side only (FR-025). |

### Lifecycle

```text
AwaitingSeat --TakeSeat(South)--> InProgress --engine Phase=Finished--> Finished
                                      |  ^
                           Detach     |  |  Attach
                                      v  |
                                   (Paused flag; Lifecycle stays InProgress)
InProgress --unexpected bot rejection/exception--> Faulted
```

- `Paused` is orthogonal to lifecycle: while true, no bot moves and no state changes occur
  (FR-026).
- `Finished` and `Faulted` tables accept no intents (`gameNotInProgress`). A client may then
  create a new table; the previous one is closed and its `FinishedGame` retained (FR-002).
- `Faulted` exists only to make an internal bug observable (a bot's own legal choice being
  rejected). It must never occur in the 100-game acceptance run.

### State transition rules

- A state change is exactly one accepted intent (engine auto-advance such as trick, hand, and
  match boundaries is part of the same `Accepted` result). Each bumps `Version` by 1 and
  produces exactly one pushed `TableUpdate`.
- Seating produces `Version 0` (initial deal and any engine start events).
- A rejected intent changes nothing: no `Version` bump, no event, no update (FR-013).

### Rejection categories and codes

| Category | Code | Raised when |
|----------|------|-------------|
| `malformed` | `Malformed` | Unknown kind, missing/invalid field, unparsable card or seat |
| `notSeated` | `NotSeated` | Sender is not the table creator or a bot seat principal (or has no table at all) |
| `paused` | `TablePaused` | Client is detached; every intent is refused until `Attach` (internal) |
| `gameNotInProgress` | `GameNotStarted`, `GameFinished`, `TableFaulted` | Table not seated yet, finished, or faulted |
| `notOwner` | `NotOwner` | `seat` is not owned by the sender's principal |
| `outOfTurn` | `NotYourTurn` | Engine reports the seat is not to act |
| `illegal` | engine `RejectionReason` (e.g. `MustFollowSuit`, `BidNotHigher`, `WrongPhase`, `CardNotInHand`) | Rules reject the action |
| `tableExists` | `TableExists`, `SeatTaken` | `CreateTable` while a game is unfinished; `TakeSeat` on a taken seat (not intent rejections; no game state involved) |

Check order: `malformed` -> `notSeated` -> `gameNotInProgress` -> `paused` -> `notOwner` -> engine.

## Wire layer (documented in [contracts/hub-protocol.md](contracts/hub-protocol.md))

| Entity | Fields |
|--------|--------|
| `IntentDto` | `kind` (`bid`/`pass`/`contra`/`recontra`/`announce`/`play`), `seat`, plus `contract` (bid), `announcement` (announce), or `card` (play) |
| `SeatViewDto` | `seat`, `phase`, `dealer`, `toAct`, `dealNumber`, `ownHand[]`, `biddingHistory[]`, `standingBid`, `standingBidder`, `contra`, `recontra`, `contract`, `leader`, `currentTrick[]`, `lastCompletedTrick?`, `completedTricks[]`, `announcements[]`, `ownDeclared[]`, `belotes[]`, `matchScore`, `heldOverPoints`, `winner?`, `legalActions[]` |
| `TableUpdateDto` | `protocolVersion`, `version`, `view`, `finished` (bool; true when `view.phase` is `finished`) |
| `CreateTableRequest` | `seed?`, `botDelayMs?` |
| `IntentResultDto` | `accepted`, `version?`, `rejection?` |

### Visibility rules (what may appear in `SeatViewDto`)

| Allowed | Never present |
|---------|---------------|
| `ownHand` (only the cards dealt so far to that seat, as in `Projection.For`) | any other seat's hand |
| `currentTrick`, `lastCompletedTrick`, `completedTricks` (all played, hence public) | undealt cards, deck order |
| `biddingHistory`, contract, contra/recontra flags | the seed, the full log, rejected-intent history |
| announcements as declared (kind/length; spec and points only once revealed) | other seats' undeclared holdings |
| `legalActions` only when `toAct == seat` | legal actions for other seats |

Privacy oracle for tests: the set of cards appearing anywhere in any message must be a subset
of (the seat's own current hand and cards it has been dealt) union (cards played so far,
derived from the omniscient log).
