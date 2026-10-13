# Research: Rooms and Multiplayer

No `NEEDS CLARIFICATION` markers remained after `/speckit-clarify`; this document records the
design decisions behind the plan, each with alternatives considered.

## 1. Where does room logic live?

- **Decision**: a new `Room` actor in `Belote.Table` (namespace `Belote.Table.Rooms`), with the
  SignalR-facing `RoomRegistry`/`RoomHub` in `Belote.Server`.
- **Rationale**: `Belote.Table` already owns live-play state and has no ASP.NET dependency, so
  room timing can be tested with `FakeTimeProvider` exactly like the existing pause/pacing tests.
  `Belote.Core` must stay pure (Principle I).
- **Alternatives**: put it in `Belote.Server` (harder to unit test, ties logic to SignalR);
  a new `Belote.Rooms` project (no second consumer yet - extra project cost for nothing).

## 2. How are seats controlled dynamically?

- **Decision**: replace the fixed `_owners` seeding (South = creator, others = bots) with a
  controller map mutated by a `SetControllerWork` item in the table's actor loop; `BotDriver`
  plays every seat whose controller is currently a `BotPrincipal` and creates bot instances lazily
  per seat.
- **Rationale**: the actor loop already serialises intents; routing handover through it makes the
  swap atomic with respect to moves. `SmartBot` calls `CardMemory.From(view)` per decision, so it
  holds no cross-move state and can take over mid-hand.
- **Alternatives**: recreate the table on every change (loses state, breaks version numbers);
  keep a bot "shadow" per human seat (wasteful and still needs the same swap).
- **Detail**: today the driver treats any non-`Paused` rejection of a bot move as a table fault.
  After a handover a bot's in-flight intent is legitimately rejected `NotOwner`; the driver must
  treat `NotOwner` for a seat it no longer controls as stale and re-evaluate.

## 3. Pause ownership and the "missing players" model

- **Decision**: the table's pause flag stays a simple boolean set by the room via
  `SetPausedAsync(bool)` (the existing creator-only guard is removed). The room keeps a set of
  *missing* participants each with a deadline, and sets `paused = missing.Count > 0` while the
  game is in progress.
- **Rationale**: matches FR-017/FR-020 (multi-missing stays paused until all returned or were
  replaced) and keeps `LiveTable` ignorant of connections (Principle III).
- **Alternatives**: per-seat pause inside the table (leaks room concepts into the table).

## 4. Countdown correctness across reloads and devices

- **Decision**: absolute `deadlineUtc` plus `serverNowUtc` in every room push; the client stores
  `offset = serverNow - clientNow` on each message and renders `deadline - (clientNow + offset)`.
  A rejoin does not restart anything because the room, not the connection, owns the timer.
- **Rationale**: satisfies "countdown is the same on every device and not reset by reloads".
- **Alternatives**: push a tick every second (chatty, drifts under throttled background tabs);
  send "seconds remaining" only (wrong after any delay or tab throttling).

## 5. Detecting disconnects promptly

- **Decision**: rely on SignalR's connection-closed callback for clean closes (reload, tab
  close: immediate) and tighten server keep-alive to `KeepAliveInterval = 5 s`,
  `ClientTimeoutInterval = 15 s` for silent loss (phone sleep, network cut).
- **Rationale**: default 30 s timeout would delay the pause start noticeably. The client side
  `ServerTimeout` must be at least 2x keep-alive; its `KeepAliveInterval` is aligned to 5 s.
- **Alternatives**: app-level heartbeats (duplicates what SignalR already does).
- **Spec note**: SC-003 is measured on connection close/loss events; undetectable-until-timeout
  silent drops are bounded by 15 s. The spec's wording was clarified accordingly.

## 6. Identity and reconnect without a database

- **Decision**: client creates a 128-bit random token (`crypto.getRandomValues`) on first use and
  stores it in `localStorage` with display name and last room/seat. Server maps
  `token -> participant` inside each room (the token is hashed in memory, compared in constant
  time). Every hub connection (initial or SignalR-auto-reconnected) calls `Rejoin` with the token;
  connection ids are never identity.
- **Rationale**: only mechanism that works with no accounts and no database; survives reload,
  tab-restore and network flaps; no personal data.
- **Alternatives**: cookie session (needs server-side session store or signed cookies; also less
  explicit on the hub); SignalR "stateful reconnect" (only covers brief drops on the same page
  load, not reloads); URL-embedded seat secret (leaks via sharing/history).
- **Limitation (accepted)**: clearing site data or switching device loses the seat's claim; the
  person joins as a new participant (spec assumption: no accounts).

## 7. Same identity in two tabs

- **Decision**: last-connection-wins. The new connection becomes the participant's connection;
  the previous one gets a `Superseded` push. That client stops auto-rejoining and shows "This game
  is open in another tab - Take back". Taking back is an explicit user action that supersedes the
  other tab in turn.
- **Rationale**: FR-022; avoids two tabs ping-ponging the connection forever.

## 8. Room ids and the "code"

- **Decision**: room id is a random GUID (122 bits) in `D` format
  (`xxxxxxxx-xxxx-...`); the shareable link is `<origin>/room/<guid>`. The join box accepts a
  bare GUID (with or without dashes/braces) or a pasted full link and extracts the GUID.
- **Rationale**: matches the request ("place the guid inside a text box"); unguessable, so no
  rate limiting or enumeration concerns.
- **Alternatives**: short 6-character codes (nicer to read aloud but guessable, needs rate
  limiting and a collision policy) - deferred; the Copy and native Share buttons cover the
  realistic sharing cases. Can be added later as an alias without changing the link.

## 9. Late join and play again at "hand boundaries"

- **Decision**: a pending claim on a bot seat is recorded by the room and applied when the
  table's `DealNumber` increases (new hand) - or immediately if the game is between hands
  (finished lobby/play-again). Deliberate leaves convert the seat to a plain bot seat with no
  reservation; disconnect-replaced seats carry the replaced participant's id so only that
  participant may reclaim, until the game ends.
- **Rationale**: Clarification Q1/Q2. Hand boundary is the cleanest point to introduce a person
  who has seen none of the hand.
- **Alternatives**: allow mid-hand takeover (rejected in clarification); queueing multiple
  claimants on one seat (first claimant wins; later ones are told the seat is taken).

## 10. Routing on the client

- **Decision**: a ~40-line History-API router handling `/` and `/room/:id`; the server already
  has `MapFallbackToFile("index.html")` for the built client and Vite's dev server falls back to
  `index.html` for unknown paths.
- **Rationale**: two routes; a router dependency is not warranted.
- **Alternatives**: React Router (extra dependency, more surface than needed).

## 11. Hub surface and backwards compatibility

- **Decision**: keep the single `TableHub` at `/table` and add the room methods to it, so the
  client holds one connection per tab; the legacy methods become a shim over rooms. Protocol
  version is raised to 2 and `TableUpdate` keeps its shape (its `view.seat` now varies by
  recipient).
- **Rationale**: one connection per tab; minimal churn for the 002-era tests that talk to the hub.
- **Alternatives**: second hub path (two connections per tab, double reconnect logic); delete the
  legacy methods (forces rewriting every existing server test in this feature).
- **Retired behaviour**: "a different connection's `CreateTable` replaces the active table"
  (`ReplaceOnCreateTests`); rooms are independent, so those tests are replaced by room-isolation
  tests.

## 12. Notifications for accessibility and UX

- **Decision**: room state carries a monotonically increasing `revision` and a `lastEvent`
  (`replaced`, `reclaimed`, `skipped`, `hostChanged`, `joiningNextHand`, `nameChanged`) with the
  acting participant's name; the client announces transitions through an `aria-live` region and
  shows a toast. A returning replaced player also gets `awayRecap` (number of moves a bot played
  for them) once.
- **Rationale**: FR-031 and Story 5 scenario 5, without a separate event channel.
