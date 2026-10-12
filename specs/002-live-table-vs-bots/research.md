# Phase 0 Research: Live Table vs Bots

No `NEEDS CLARIFICATION` items remain in the spec; the questions below are design choices the
spec deliberately deferred to planning.

## R1. Persistent-connection transport

- **Decision**: ASP.NET Core SignalR hub over the WebSocket transport, JSON protocol
  (System.Text.Json, camelCase, string enums).
- **Rationale**: Mandated by the constitution (ASP.NET Core + SignalR). Gives server push,
  invocation results, and ordered delivery per connection. JSON keeps the wire human-readable
  and aligned with the log vocabulary.
- **Alternatives**: raw WebSockets (rebuilds framing, invocation, and reconnect later);
  gRPC streaming (not in the approved stack); MessagePack protocol (opaque to debug, no
  benefit at this scale).

## R2. Serialising intents ("one at a time")

- **Decision**: A single-reader `Channel<WorkItem>` consumed by one loop per table (actor).
  Client intents, bot intents, attach/detach, and seating all enqueue work. The loop applies
  work in FIFO order and publishes the resulting immutable snapshot.
- **Rationale**: One writer makes state transitions trivially race-free, gives FIFO fairness
  (a burst is processed in arrival order), and lets "evaluate each against the state left by
  the previous one" (FR-014) be true by construction.
- **Alternatives**: `SemaphoreSlim`/`lock` around apply+publish (simpler but unordered
  waiters and publishing inside the lock invites reentrancy); optimistic version check with
  retry (adds a retry protocol with no benefit for one table).

## R3. Reading state without enqueueing

- **Decision**: The actor publishes an immutable `TableSnapshot` (version, `GameState`,
  last completed trick, lifecycle) with `Volatile.Write`. `GetView` and update mapping read
  the snapshot lock-free.
- **Rationale**: Views are pure projections of an immutable state, so reads are always
  consistent and never change state (FR-018). A requested view at version *n* equals the
  update for version *n*.
- **Alternatives**: routing reads through the queue (needless latency, and a slow bot delay
  would not block it anyway).

## R4. Bots through the same path

- **Decision**: `BotDriver` runs one loop per table. It waits until it is a bot seat's turn
  and the client is attached, waits the configured delay (via `TimeProvider`), obtains a
  `SeatView` via `Projection.For`, asks that seat's `RandomLegalBot`, and calls
  `LiveTable.SubmitAsync(intent, BotPrincipal(seat))` - the same method the hub calls with
  `ConnectionPrincipal`. Seat ownership is a map `Seat -> Principal`; nothing else
  distinguishes bots.
- **Rationale**: Satisfies FR-020/021 and constitution III; a future "bot takeover" is only
  a change of the principal map.
- **Alternatives**: bots applying moves directly to state (bypasses validation; violates
  FR-020).

## R5. Reproducibility versus the 001 simulation

- **Decision**: Bot seats use `RandomLegalBot(Seeds.ForSeat(seed, seat))`; the deal uses
  `GameEngine.Start(ruleSet, seed)`. The test client plays South with
  `SeededRandom(Seeds.ForSeat(seed, South))` selecting uniformly among the server-reported
  `legalActions`. The server guarantees `legalActions` is in `LegalActions.For` order.
- **Rationale**: This is exactly what `SeatPresets.AllBot` does in 001, so the live event
  sequence equals the 001 run for that seed (clarification Q1). Comparison is over the event
  sequence; the log header differs only in the recorded South controller name
  (`RemoteClient`), so the whole-file `LogHash` is not expected to match, but a hash over
  events alone is.
- **Alternatives**: matching only the deal (rejected by clarification).

## R6. What an update contains

- **Decision**: `TableUpdate { protocolVersion, version, view }` where `view` is a
  `SeatViewDto` mapped from `SeatView` plus `lastCompletedTrick` (kept by the table so the
  fourth card of the 8th trick is still visible after the engine auto-advances to the next
  hand, FR-027) and `legalActions` (only when it is that seat's turn). No events are sent.
- **Rationale**: Events such as `HandDealt` carry all four hands, so streaming the log to the
  client would violate FR-017/FR-025; a view per state change is the contract. A strictly
  increasing `version` makes order/gaps/duplicates (FR-019) checkable by the client.
- **Alternatives**: sending filtered events (needs per-event redaction rules, higher leak
  risk, and a client reducer).

## R7. Disconnect = pause

- **Decision**: The hub reports connection loss to the table (`Detach`). While detached the
  actor rejects bot-origin work with an internal `Paused` outcome and `BotDriver` blocks on a
  gate until `Attach`. State is not otherwise touched. Reconnect is not exposed on the hub
  (out of scope) but `Attach` is covered by table-level tests so FR-026 is verifiable.
- **Rationale**: Matches clarification Q2 with no timers and no cleanup logic.
- **Alternatives**: discard on disconnect (rejected by clarification).

## R8. Pacing and testing time

- **Decision**: Bot delay comes from server configuration (`Table:BotDelayMs`, default
  600 ms) and may be overridden per table in `CreateTable` (clamped 0-5000 ms). All delays go
  through an injected `TimeProvider`.
- **Rationale**: Tests set 0 for speed and use `FakeTimeProvider` to verify spacing
  deterministically (SC-007) without sleeping.
- **Alternatives**: real `Task.Delay` with wall-clock assertions (flaky).

## R9. Rejection model

- **Decision**: Rejections carry `category` (`malformed`, `notSeated`, `notOwner`,
  `gameNotInProgress`, `outOfTurn`, `illegal`), a stable `code` (the 001
  `RejectionReason` name for engine rejections, otherwise the category name), and an English
  `detail` for developers. Check order: malformed, notSeated, gameNotInProgress, notOwner,
  then engine (`NotYourTurn` -> `outOfTurn`, `GameFinished` -> `gameNotInProgress`, other
  reasons -> `illegal`).
- **Rationale**: Ownership is checked before turn so an intent for another seat is always
  "not owner" regardless of whose turn it is; machine codes keep a later localized UI
  independent of English text (constitution VI).

## R10. Test hosting and ports

- **Decision**: End-to-end tests build the real server through `ServerHost.Build` bound to
  `http://127.0.0.1:0`, read the assigned address, and connect a real SignalR client over
  WebSockets. In-process tests use `LiveTable` directly.
- **Rationale**: Exercises the true persistent connection without a heavier test-host
  package, and parallel test classes cannot collide on ports.
- **Alternatives**: `WebApplicationFactory`/`TestServer` (extra package, in-memory transport
  that hides socket-level behaviour).

## R11. Finished-game logs

- **Decision**: On `GameFinished` the table builds a `GameLog` (header: seed, full rule set,
  seat names with South recorded as `RemoteClient`) and hands it to an in-memory store held by
  `TableRegistry`; an optional `Table:LogDirectory` writes `*.jsonl` for developers. Tests
  read it via the registry, never through the hub.
- **Rationale**: Reuses 001's `GameLog`/`GameAuditor` for replay verification (FR-023/024)
  with no new format.
