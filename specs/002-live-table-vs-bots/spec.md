# Feature Specification: Live Table vs Bots

**Feature Branch**: `002-live-table-vs-bots`

**Created**: 2026-10-12

**Status**: Draft

**Input**: User description: "A single live Belote table that one connected player can play against three bots, in real time, over a persistent connection. Headless: the 'player' is an automated test client, no browser UI. Scope: connect, create one table, take the South seat; three bots fill the remaining seats; game starts automatically and runs to a winner using the 001 rules engine; client sends intents only (bid, play card, declare announcement) which the server validates; illegal/out-of-turn/not-owned intents rejected with a clear reason and no state change; after every state change the server pushes that seat's view only; client can request its current view at any time; bots act through the same path as a human seat with a configurable delay; one intent per table processed at a time; every game recorded in the 001 event log format. Out of scope: browser UI, accounts, multiple tables, other humans joining, reconnect handling, turn timers, bot takeover, persistence to a database, smarter bots."

## Clarifications

### Session 2026-10-12

- Q: Should the client see a completed trick (all four cards) in an update before the table clears? → A: Yes — the update after the fourth card shows all four cards and the trick winner; the table shows that completed trick until the next card is played.
- Q: After the game finishes, may the client create a new table on the same connection? → A: Yes — the finished table is closed, its event log is kept for replay, and a new table may be created.
- Q: When the client's connection drops mid-game, what should the table do? → A: Pause — the table stays in memory and nothing advances until the client returns.
- Q: Should bot and test-client choices be reproducible from the game seed exactly as in a 001 four-bot simulation? → A: Yes — each seat's choices are derived from the game seed exactly as in 001, and the test client plays South with the same baseline policy, so a live game equals the 001 simulation of that seed move for move.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Play a full game against three bots (Priority: P1)

A connected client creates a table and takes the South seat. The other three seats (North,
East, West) are filled by baseline random-move bots from feature 001, with North as the
client's partner. As soon as the client is seated the game starts on its own, and the client
plays its seat — placing bids, playing cards, declaring announcements — until the game
reaches a final winner. Bots move on their own turns without any action from the client.

**Why this priority**: A complete, live, interactive game is the core deliverable; every
other story supports or verifies it.

**Independent Test**: With an automated client, connect, create a table, take South, then
repeatedly act on every turn using the legal moves the server reports, until the server
announces a final winner. Verify the game ended with exactly one winning team and a score that
reached the target.

**Acceptance Scenarios**:

1. **Given** a connected client with no table, **When** it creates a table and takes a seat,
   **Then** it is seated as South, the three other seats are occupied by bots, and the game
   starts without any further action.
2. **Given** a running game, **When** it is a bot's turn, **Then** the bot acts on its own
   and the client receives an update reflecting that move.
3. **Given** a running game, **When** it is the client's turn and the client submits one of
   its reported legal moves, **Then** the move is applied and the game advances.
4. **Given** the game has played through all hands, **When** a team reaches the target score,
   **Then** the client is told the game is finished, who won, and the final score, and no
   further moves are accepted.

---

### User Story 2 - Server rejects invalid intents without changing state (Priority: P1)

The client only ever sends intents. The server checks every intent against the rules engine
and the table's current state. An intent that is illegal under the rules, sent out of turn,
or addressed to a seat the client does not own is refused with a clear reason, and the game
is left exactly as it was.

**Why this priority**: Server authority is what makes the live table trustworthy; without it
the client could corrupt or cheat the game.

**Independent Test**: In a scripted session, submit a catalogue of bad intents (illegal bid,
illegal card, out-of-turn action, action for another seat, action before the game starts or
after it ends) and verify each is rejected with a reason and that the table's state before
and after is identical.

**Acceptance Scenarios**:

1. **Given** it is the client's turn, **When** it plays a card that is not legal, **Then** the
   intent is rejected with a reason naming why, and the state is unchanged.
2. **Given** it is a bot's turn, **When** the client sends any intent for its own seat,
   **Then** the intent is rejected as out of turn, and the state is unchanged.
3. **Given** a client seated as South, **When** it sends an intent on behalf of North, East,
   or West, **Then** the intent is rejected as not owned by the client, and the state is
   unchanged.
4. **Given** the game is finished, **When** the client sends any intent, **Then** it is
   rejected as the game being over, and the state is unchanged.
5. **Given** a rejected intent, **When** the client then submits a legal intent on its turn,
   **Then** the game proceeds normally.

---

### User Story 3 - Receive only my seat's view, and request it any time (Priority: P1)

After every state change the server sends the client an update containing only what South is
entitled to see: its own hand, the cards currently on the table, the bids made, announcements
that have already become public, the scores, whose turn it is, and — when it is the client's
turn — the exact set of legal moves. The client can also ask for its current view at any time
and receive the same kind of view.

**Why this priority**: Hidden-information safety is a core guarantee, and the on-demand view
is the foundation for the later UI and reconnect work.

**Independent Test**: Record every update and every on-demand view across many games and
check each against the true game state: nothing shown may be outside South's own hand or
already public on the table.

**Acceptance Scenarios**:

1. **Given** any state change at the table, **When** the server pushes an update, **Then** the
   update contains South's own hand and public information only, and never another seat's
   hand, undealt cards, or the shuffle seed.
2. **Given** it is the client's turn, **When** the update is pushed, **Then** it lists exactly
   the moves that are legal for the client at that moment.
3. **Given** it is not the client's turn, **When** the update is pushed, **Then** it lists no
   legal moves.
4. **Given** announcements that have not yet become public, **When** an update is pushed,
   **Then** it reveals only the client's own announcements and nothing about other seats'
   undeclared holdings.
5. **Given** a running game, **When** the client requests its current view, **Then** it
   receives a view equal to the one it would have received in the most recent update, with
   no change to game state.
6. **Given** the fourth card of a trick has just been played, **When** the update is pushed,
   **Then** it shows all four cards and the trick winner, and that completed trick remains
   on the table in later views until the next card is played.

---

### User Story 4 - Bots behave like any other seat, at a human pace (Priority: P2)

Bots take their turns through the same path as a human seat: they see only their own seat's
view and send intents that are validated exactly like the client's. A configurable pause
between bot moves lets a human follow the game; automated tests set the pause to zero so a
game finishes as fast as possible.

**Why this priority**: Required for the later UI, and it keeps bots honest, but a game can
be played end to end without the pacing control.

**Independent Test**: Run a game with a non-zero delay and verify bot moves are spaced by at
least that delay; run with zero delay and verify games complete without waiting. Verify via
the game record that every bot move was legal for the view that seat had.

**Acceptance Scenarios**:

1. **Given** a bot delay of N milliseconds, **When** consecutive bot moves occur, **Then**
   they are separated by at least N milliseconds.
2. **Given** a bot delay of zero, **When** a game runs, **Then** bot moves are applied
   without any deliberate waiting.
3. **Given** a bot's turn, **When** it chooses a move, **Then** the choice is based only on
   that seat's own view, and the move is validated by the same checks as the client's.

---

### User Story 5 - Concurrency safety and a replayable record (Priority: P2)

Only one intent per table is processed at a time. Even if the client sends several intents
at once, or a bot and the client act simultaneously, each is handled in sequence against the
latest state and the state is never corrupted. Every game is recorded in the event log
format defined in feature 001, and replaying that log reproduces the game's final state.

**Why this priority**: Correctness under concurrent input and reproducibility are what make
the live table testable and debuggable.

**Independent Test**: Fire a burst of simultaneous intents at one table and verify every
intent got a definite outcome and the final state equals some valid sequential ordering. After
a game, replay its log and compare the resulting state to the live game's final state.

**Acceptance Scenarios**:

1. **Given** several intents submitted at the same moment, **When** they are processed,
   **Then** each is handled one at a time against the state left by the previous one, and each
   receives its own accepted or rejected result.
2. **Given** two identical intents for the same card, **When** both are submitted at once,
   **Then** exactly one is accepted and the other is rejected, never both applied.
3. **Given** a completed game, **When** its event log is replayed from the start, **Then** the
   resulting final state and score are identical to the live game's.
4. **Given** a game with a fixed seed, **When** it is played live to completion with South
   played by the same baseline policy and seed-derived choices as in a 001 simulation,
   **Then** every move, the event log, and the final scores equal the 001 simulation of that
   seed move for move.

---

### Edge Cases

- A client tries to create a second table while its current game is unfinished, or take a
  seat at a table that is already occupied: refused with a clear reason; the existing table
  is unaffected.
- A client sends an intent before it has created a table or taken a seat: rejected as not
  seated.
- A client sends an intent while the game is starting or between hands (dealing in progress):
  rejected as out of turn or not currently accepting moves, state unchanged.
- A client sends a malformed or unrecognised intent: rejected with a reason, state unchanged.
- A hand in which all four players pass: the hand is voided and redealt per the 001 rules;
  the client receives updates for the redeal and the game continues.
- The client sends an intent at the same moment a bot is about to act: they are serialized;
  at most one is applied per processing step, and the other is evaluated against the new
  state.
- The client's connection drops mid-game: the table pauses and stays in memory in its
  current state; no bot move or other state change occurs while the client is absent, and
  the host is not affected. Restoring the client to the table (reconnect handling) is out
  of scope for this feature.
- The view is requested before the game has started: it is refused with `GameNotStarted`, since no
  game state exists until the client takes its seat. After the game has finished the final view
  is returned.

## Requirements *(mandatory)*

### Functional Requirements

**Table and seating**

- **FR-001**: The system MUST allow one client to connect and keep a persistent, two-way
  connection over which it sends intents and receives pushed updates.
- **FR-002**: The system MUST allow a connected client to create one table at a time. Once
  that table's game has finished, the system MUST close it, keep its event log for replay,
  and allow the same client to create a new table on the same connection.
- **FR-003**: The system MUST seat the creating client as South, with North as its partner,
  and fill North, East, and West with baseline random-move bots from feature 001.
- **FR-004**: The system MUST NOT allow seat assignment to differ from this fixed layout, and
  MUST NOT allow additional humans to join the table.
- **FR-005**: The system MUST start the game automatically once the client is seated, with no
  further start action required.

**Game flow**

- **FR-006**: The system MUST run the full game from the first deal to a final winner using
  the feature 001 rules engine and the default rule set, with no rule logic duplicated outside
  that engine.
- **FR-007**: The system MUST notify the client when the game has finished, including the
  winning team and final score, and MUST refuse all further intents afterwards.
- **FR-008**: The system MUST accept a deterministic seed for a table so that a game can be
  reproduced exactly. Given a seed, the deal and each bot seat's choices MUST be derived
  from it exactly as in a feature 001 simulation of that seed, so that a live game whose
  South seat follows the 001 baseline policy is identical, move for move, to that
  simulation.

**Intents and validation**

- **FR-009**: The client MUST only be able to express three kinds of intent: place a bid,
  play a card, and declare an announcement. The client MUST NOT be able to set game state
  directly.
- **FR-010**: The system MUST validate every intent against the rules engine and the table's
  current state before applying it.
- **FR-011**: The system MUST reject any intent that is illegal under the rules, out of turn,
  for a seat the client does not own, sent when no game is in progress, or malformed.
- **FR-012**: Every rejection MUST return to the client a clear, specific reason that
  identifies which of the rejection categories applied.
- **FR-013**: A rejected intent MUST leave all table state identical to what it was before
  the intent, and MUST NOT add anything to the game record.
- **FR-014**: The system MUST process at most one intent per table at a time, evaluating each
  against the state left by the previous one, so that simultaneous intents never corrupt
  state and each receives exactly one definite outcome.

**Views and updates**

- **FR-015**: After every state change, the system MUST push to the client an update
  containing the client seat's view only.
- **FR-016**: A seat's view MUST contain: that seat's own hand, the cards currently on the
  table, the bids made, announcements already made public, the scores, whose turn it is, and
  — only when it is that seat's turn — the legal moves available to it.
- **FR-017**: A pushed update or requested view MUST NOT contain any card that is not in the
  client seat's own hand or already public on the table, and MUST NOT contain other seats'
  hands, the undealt cards, the deck order, or the shuffle seed.
- **FR-018**: The client MUST be able to request its current view at any time, receiving the
  same content it would have in a pushed update, without that request changing game state.
- **FR-019**: The system MUST push updates to the client in the order the state changes
  occurred, with no update missing and none duplicated.
- **FR-027**: When the fourth card of a trick is played, the resulting update MUST show all
  four cards of that trick and the trick's winner, and every view MUST continue to show that
  completed trick on the table until the next card is played.

**Bots**

- **FR-020**: Bots MUST take their turns through the same intent path and the same validation
  as a human seat; no table or engine rule may branch on whether a seat is a human or a bot.
- **FR-021**: Each bot MUST decide using only its own seat's view.
- **FR-022**: The system MUST support a configurable delay between bot moves, and MUST allow
  it to be set to zero.
- **FR-026**: While the client is not connected, the system MUST NOT advance the game: no
  bot moves are made and no state changes occur, and the table's state is preserved in
  memory unchanged until the client returns or the server stops.

**Recording**

- **FR-023**: The system MUST record every game in the same event log format defined by
  feature 001, including every accepted move by humans and bots, and the seed and rule set
  needed to replay it.
- **FR-024**: Replaying a recorded game's event log MUST reproduce the same final state and
  final score as the live game.
- **FR-025**: The recorded event log MUST remain on the server side and MUST NOT be sent to
  the client during play, since it contains all hands.

### Key Entities

- **Table**: The single live game room. Has one seated human, three bot seats, a rule set, a
  seed, a current game state, and a lifecycle (waiting for seat, in progress, finished).
- **Seat**: One of North, East, South, West. Belongs to a team (North–South, East–West) and
  is controlled either by the client (South) or by a bot (the others). Owned seats determine
  which intents are accepted.
- **Intent**: A request from a client to place a bid, play a card, or declare an
  announcement, addressed to a seat. May be accepted or rejected; never changes state if
  rejected.
- **Rejection**: The reply to an unacceptable intent, carrying a category (illegal, out of
  turn, not owner, game not in progress, malformed) and a human-readable reason.
- **Seat View**: The projection of the table for one seat: own hand, public table cards,
  bids, public announcements, scores, whose turn, and legal moves when it is that seat's
  turn. Never contains hidden information.
- **Update**: A pushed message containing the client seat's current view after a state
  change, delivered in order.
- **Game Record**: The ordered event log of the game in the format from feature 001, held on
  the server, sufficient to replay the game.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An automated client plays 100 complete seeded games end to end against three
  bots with zero rejected legal moves, zero stuck games (every game reaches a final winner),
  and event logs and final scores that match the feature 001 simulation of the same seeds,
  move for move, in 100% of the games.
- **SC-002**: Across all of those games, 0 updates or requested views sent to the client
  contain a card that is neither in that seat's own hand nor already public on the table.
- **SC-003**: In a scripted test set covering each rejection category (illegal, out of turn,
  not owner, game not in progress, malformed), 100% of illegal or out-of-turn intents are
  rejected with a stated reason, and the table state is identical before and after in 100% of
  cases.
- **SC-004**: Replaying the event log of each of the 100 games reproduces the same final
  state and score in 100% of cases.
- **SC-005**: In a concurrency test that submits bursts of simultaneous intents to one table,
  every intent receives exactly one definite outcome, no state corruption is observed, and
  the final state matches a valid sequential ordering in 100% of runs.
- **SC-006**: With the bot delay set to zero, a complete game finishes quickly enough that
  100 games run unattended as part of an ordinary automated test pass.
- **SC-007**: With a non-zero bot delay configured, 100% of consecutive bot moves are
  separated by at least the configured delay.

## Assumptions

- The rules engine, default (Bulgarian) rule set, baseline random-move bot, seat-view
  projection, and event log format from feature 001 already exist and are reused unchanged;
  this feature adds no new game rules.
- "Persistent connection" means a long-lived, two-way channel over which the server can
  push updates without the client polling. The specific transport is a planning decision.
- There is at most one active table per server instance, and it lives only in memory; it is
  lost when the server stops. Nothing is persisted to a database. A finished table is
  replaced when the client creates a new one.
- The client is trusted to identify itself only as "the connected client"; there are no
  accounts or authentication. Seat ownership is derived from the connection that created the
  table.
- The only way to end a game is to play it to a final winner; abandoning or restarting a
  table is not part of this feature.
- The game's seed is supplied when the table is created (for tests) or chosen by the server
  when not supplied. The seed is never revealed to the client during play.
- Bot delay is a table-level setting with a short, human-followable default; tests set it
  to zero.
- Reconnect handling, turn timers, bot takeover of the human seat, browser UI, multiple
  tables, additional humans, and smarter bots are explicitly out of scope and deferred.
