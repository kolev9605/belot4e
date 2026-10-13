# Feature Specification: Rooms and Multiplayer

**Feature Branch**: `007-rooms-multiplayer`

**Created**: 2026-10-13

**Status**: Draft

**Input**: User description: "Let's think about rooms and multiplayer. On the main menu there should be two buttons "Start game" and "Join game". Start game creates a room and the creator can share a URL with the room inside it like `<site>/room/{some_guid}`. People can either open the URL or place the guid in a text box to join a room. Other players can pick a seat. If a player reloads the browser, the app attempts to reconnect them and the game pauses for 1 minute waiting for the player. If the dropped player doesn't reconnect within 1 minute they are replaced by a bot so the game continues, but if they reconnect eventually they can proceed playing. Think about user experience and make the game user friendly; resiliency is the most important consideration. Avoid databases for now - store as much as possible in the browser; if a database becomes needed, say so. Players can pick a name instead of the default South/North/East/West."

## Clarifications

### Session 2026-10-13

- Q: Once a game has started, should someone opening the room link later be able to take a seat a bot has been playing since the start? → A: Yes, at the next hand boundary; the seat's original human, if any, keeps reclaim priority.
- Q: When a player deliberately leaves a game in progress, is their seat reserved for them? → A: No reservation; it becomes an ordinary bot seat that anyone, including the leaver, can take at the next hand boundary. (Only humans replaced by timeout/disconnect keep reclaim priority.)
- Q: How long does a room with nobody connected stay alive? → A: 1 hour.
- Q: Should there be a cumulative limit on a player's total paused time per game? → A: No cap; every disconnection gets a fresh full minute.
- Q: Can the remaining players skip the wait during a pause and have a bot take the seat now? → A: Yes; any remaining human can, and the missing player can still reclaim the seat.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a room and invite friends (Priority: P1)

From the main menu a player chooses **Start game**. A new room is created and the player lands
in the room's lobby, where they see the four seats around the table and a prominent, one-tap
way to share the room: a link they can copy (or send through the device's share options) and
the bare room code they can read out. The creator is seated automatically and can start the
game whenever they like; seats nobody has taken are played by bots.

**Why this priority**: Nothing else in this feature exists without rooms. Even with only this
story, a player can create a room and play against bots through the new lobby flow.

**Independent Test**: From the main menu, press Start game, confirm a lobby appears with the
creator seated, a copyable link and room code are shown, and starting the game produces a
normal game with bots in the three empty seats.

**Acceptance Scenarios**:

1. **Given** the main menu, **When** the player presses Start game, **Then** a room is created,
   the player is seated and shown a lobby with a shareable link and a room code.
2. **Given** a lobby, **When** the player taps Copy link, **Then** the full room link is on the
   clipboard and the player gets a visible confirmation.
3. **Given** a lobby with only the creator seated, **When** the creator starts the game,
   **Then** the game begins with bots in all empty seats.
4. **Given** the player's device cannot copy to the clipboard, **When** they open the share
   control, **Then** the link remains visible and selectable so it can be copied by hand.

---

### User Story 2 - Join a room by link or code and pick a seat (Priority: P1)

A friend either opens the shared link directly, or presses **Join game** on the main menu and
pastes the room code (or the whole link) into a text box. They arrive in the room's lobby,
choose a name, and pick any free seat. Everyone already in the room sees the new player appear
in real time.

**Why this priority**: Joining is the other half of multiplayer; with Story 1 it delivers
the core "play with friends" value.

**Independent Test**: With a room open in one browser, open the link in a second browser, pick a
name and a free seat, and verify both browsers show the same lobby within a moment.

**Acceptance Scenarios**:

1. **Given** a valid room link, **When** a person opens it, **Then** they land in that room's
   lobby (after choosing a name if they have none saved).
2. **Given** the main menu, **When** the person presses Join game and enters a valid room code
   or pastes a full room link, **Then** they land in that room's lobby.
3. **Given** a lobby with free seats, **When** a person picks a free seat, **Then** the seat
   shows their name to everyone in the room and the other seats stay available.
4. **Given** two people tap the same free seat at the same moment, **When** the room settles,
   **Then** exactly one gets the seat and the other is told it was just taken and is invited to
   pick another.
5. **Given** a room code that does not exist (or a room that has ended), **When** a person tries
   to join, **Then** they see a clear message and a way back to the main menu or to re-enter the
   code, never a blank screen.
6. **Given** a room with all four seats held by humans, **When** another person opens the link,
   **Then** they are told the room is full.
7. **Given** a game in progress with at least one seat played by a bot that was never taken over
   from a human, **When** a person opens the link, **Then** they can choose to take that seat,
   are told they will join at the start of the next hand, and take over then with that seat's
   current score situation.
8. **Given** a seat is held for a replaced human who may return, **When** a newcomer looks for a
   seat to take over, **Then** that seat is not offered to them (the original human keeps
   priority).

---

### User Story 3 - Play together as a table of humans and bots (Priority: P1)

Once the host starts the game, every human plays their own seat from their own device and sees
only their own hand. Seats that no human took are driven by bots. Everyone sees the same
table, the same turn order, and the real names of the other players instead of compass
directions; partners are clearly identified.

**Why this priority**: This is the product value of the feature — real people playing together.

**Independent Test**: Start a game in a room with two humans (opposite seats, i.e. partners)
and two bots; play a full hand and confirm each human acts only on their turn, sees only their
own cards, and sees the other human's name and moves in real time.

**Acceptance Scenarios**:

1. **Given** a started game with several humans, **When** it is a human's turn, **Then** only
   that human can act and everyone else sees whose turn it is by name.
2. **Given** a started game, **When** any player views the table, **Then** they never see
   another player's hidden cards.
3. **Given** a started game, **When** a human is seated, **Then** the table shows each seat's
   chosen name (and keeps the seat's position/partnership visible) rather than only
   South/North/East/West.
4. **Given** a game in progress, **When** the game ends, **Then** everyone in the room sees the
   final result and can stay in the room to play again.

---

### User Story 4 - Reload or lose connection without losing your seat (Priority: P1)

Resilience is the primary quality bar. If a player reloads the page, closes and reopens the tab,
puts their phone to sleep, or briefly loses their network, the app silently tries to bring them
back to the same room and the same seat with their hand intact — no menus, no re-entering
codes. While that player is away, the game **pauses for up to one minute**. Everyone remaining
sees who is missing and a countdown, and the missing player's own device shows that it is
reconnecting.

**Why this priority**: Reloads and flaky mobile connections are the norm, not the exception.
Without this, multiplayer would be too fragile to use.

**Independent Test**: In a running multi-human game, reload one human's browser mid-hand;
verify the others see a pause with a countdown, the reloaded browser returns to the same seat
and hand state within seconds, and the game resumes where it left off.

**Acceptance Scenarios**:

1. **Given** a player in a started game, **When** they reload the page, **Then** they are
   returned to the same room and seat automatically, with the table and their hand exactly as
   they were.
2. **Given** a human disconnects mid-game, **When** the others look at the table, **Then** the
   game is paused, shows the missing player's name and a visible countdown from 1 minute, and
   no bot or other player can act in the meantime.
3. **Given** the game is paused for a missing player, **When** that player reconnects before the
   countdown ends, **Then** the pause lifts for everyone, play resumes from exactly the same
   point, and nobody has lost a move.
4. **Given** the game is paused for a missing player, **When** another human taps "Play with a
   bot now", **Then** a bot takes that seat immediately, the pause lifts, everyone is told who
   was replaced and by whose action, and the missing player can still reclaim the seat later.
5. **Given** the game is paused, **When** more than one human is missing, **Then** the game
   stays paused until all of them have returned or been replaced, with each missing player
   listed.
6. **Given** a player's connection drops, **When** their device is trying to come back,
   **Then** their screen says so (reconnecting, with attempts continuing automatically) rather
   than showing a broken or empty table.
7. **Given** the player closed the tab entirely, **When** they reopen the site later in the
   same browser, **Then** the main menu offers a one-tap "Return to your game" for the room
   that is still running.

---

### User Story 5 - Dropped player is replaced by a bot and can take their seat back (Priority: P2)

If a missing player does not return within the one minute, a bot takes over their seat so the
others can continue without waiting further. The bot continues from the exact current state of
that seat. Anyone who was replaced can still come back later: when they return they are put
straight back into their seat (taking it back from the bot) and carry on playing.

**Why this priority**: Prevents one flaky player from ruining everyone's game, but requires
the P1 stories to be useful.

**Independent Test**: Disconnect one human and wait out the minute; verify a bot takes the seat,
play continues; then reconnect the human and verify they resume control of that seat.

**Acceptance Scenarios**:

1. **Given** a paused game, **When** the missing player's minute elapses, **Then** a bot takes
   their seat, the pause lifts, everyone is told who was replaced, and the game continues.
2. **Given** a seat is being played by a bot that replaced a human, **When** that human
   reconnects, **Then** they regain the seat, its current hand and score situation, and the bot
   stops acting for it.
3. **Given** a human regains their seat while the bot was mid-turn or it is currently another
   player's turn, **When** they return, **Then** the handover happens cleanly at a turn boundary
   — no move is duplicated, lost, or applied twice.
4. **Given** a human has been replaced, **When** others look at the table, **Then** the seat
   still shows that player's name with a clear "bot playing for …" indication, and it returns to
   normal when they come back.
5. **Given** a replaced human, **When** they return, **Then** they are informed what happened
   while they were away (e.g., the bot played N cards) before they continue.

---

### User Story 6 - Pick a display name (Priority: P2)

Before taking a seat, a player enters a display name. The name is remembered on their device so
they are not asked again, can be changed at any time from the lobby, and replaces
South/North/East/West as the label everywhere in the room (seat, turn indicator, scoreboard,
pause notices). Bots keep friendly default names clearly marked as bots.

**Why this priority**: Makes games between friends feel personal; independent of rooms working.

**Independent Test**: Enter a name, take a seat, and verify it appears at the table for all
participants; reload and verify the name is still remembered.

**Acceptance Scenarios**:

1. **Given** a first-time visitor, **When** they are about to create or join a room, **Then**
   they are asked for a name before taking a seat, with a sensible suggested default.
2. **Given** a saved name, **When** the player returns in the same browser, **Then** it is
   prefilled and they are not blocked on it.
3. **Given** a name that is empty, only whitespace, too long, or already used by someone else in
   the room, **When** the player submits it, **Then** they see a clear inline message and can
   correct it.
4. **Given** a player changes their name in the lobby, **When** they confirm, **Then** everyone
   in the room sees the new name immediately.

---

### User Story 7 - Lobby housekeeping: leaving, host handover, play again (Priority: P3)

Players can change seats or leave while in the lobby, and the room keeps working if the creator
disappears. After a game ends, the room stays open so the same group can start another game
without everyone re-joining.

**Why this priority**: Polish that makes rooms feel solid but is not required for a first
playable multiplayer.

**Independent Test**: Seat two players, have one switch seats, have the host leave, and verify
the remaining player can still start the game; finish a game and verify a new one can start in
the same room.

**Acceptance Scenarios**:

1. **Given** a lobby, **When** a seated player picks a different free seat or chooses to stand
   up, **Then** their old seat becomes free and everyone sees the change.
2. **Given** the host leaves the room or is disconnected for the full minute, **When** other
   humans remain, **Then** the host role passes to another human so the game can still be
   started and managed.
3. **Given** a started game, **When** a human chooses to leave deliberately, **Then** a bot
   takes their seat immediately (without the one-minute wait), the seat is not reserved for
   them, and it becomes open to any newcomer — the leaver included — at the next hand boundary
   (see FR-008a).
4. **Given** a finished game, **When** the host chooses Play again, **Then** a new game starts
   in the same room with the same seats and names.
5. **Given** a room in which no human is connected, **When** it stays empty beyond a
   1 hour, **Then** it is closed and its link shows a "room has ended" message.

---

### Edge Cases

- **Same player, two tabs/devices**: opening the room again with the same identity takes over
  the seat; the older tab is told it was taken over and offers to take it back, and never
  silently fights for control.
- **Different browser/device after being replaced**: seat reclaim is tied to the original
  browser; from another device the player joins as a new person and can only pick a free seat
  (see Assumptions: no accounts).
- **Everyone leaves or drops at once**: the game stays paused (no bots playing against nobody)
  for up to 1 hour (the room idle period); whoever returns first resumes.
- **Reload during the pause countdown**: the countdown is the same for everyone and is not
  reset or extended by reloads of other players; the dropped player's remaining time is shown
  consistently on every device.
- **Dropout at the moment of a hand/trick transition or during the final result**: the pause
  applies at the next point a decision is needed; no result is lost or shown twice.
- **Service restarted or room genuinely gone**: reconnect attempts end with a plain-language
  explanation and a way back to the menu; the player is never stuck on a spinner.
- **Slow/unstable network ("flapping")**: each drop starts its own one-minute grace period and
  ends the moment the player is back, so repeated short drops cause repeated short pauses
  (accepted; no cumulative limit — see FR-021).
- **Malformed or truncated room links/codes**: trimmed and validated; a helpful message is
  shown instead of an error.
- **Name collisions / lookalike or hostile names**: duplicates in one room are refused; names
  are displayed as plain text only.
- **Language**: all new text is available in English and Bulgarian, and the room link works
  regardless of the language chosen.
- **Pre-game seat holding**: a seat held in the lobby by someone who vanished is freed after
  the same one-minute grace period, so it never blocks the room.

## Requirements *(mandatory)*

### Functional Requirements

**Rooms and entry**

- **FR-001**: The main menu MUST offer two primary actions: **Start game** (create a room) and
  **Join game** (enter an existing room).
- **FR-002**: Creating a room MUST produce a unique, hard-to-guess room identifier and a
  shareable link containing it in the form `<site>/room/<identifier>`.
- **FR-003**: The creator MUST be able to copy the link in one action and see the bare room code
  for reading aloud; where the device supports it, a native share action SHOULD be offered.
- **FR-004**: A person MUST be able to join by opening the link, or by entering either the bare
  room code or a pasted full link into a text box on the main menu.
- **FR-005**: Opening a link to a room MUST take the person to that room's lobby (or straight
  back to their seat — see FR-014) without requiring them to visit the main menu first.
- **FR-006**: Joining a room that does not exist, has ended, or is full MUST show a specific,
  friendly explanation and a next step; it MUST NOT show a blank or broken screen.
- **FR-007**: A room MUST seat at most four humans, one per seat, and every seat MUST be either
  a human, a bot, or free (in the lobby).

**Seats and names**

- **FR-008**: Any person in a room's lobby MUST be able to pick any free seat; seat selection
  MUST be visible to everyone in the room in real time and conflicting picks MUST resolve to
  exactly one winner with a clear message for the other.
- **FR-008a**: During a game in progress, a person who is not seated MUST be able to take over
  any seat held by a bot that did not replace a human, effective at the start of the next hand;
  seats of humans replaced by disconnection MUST NOT be offered to others while that human may
  still reclaim them (until the game ends). A human who deliberately leaves keeps no
  reservation: their seat becomes an ordinary bot seat, open to anyone (including the leaver)
  under this same rule. Taking over MUST NOT change game state or scoring (FR-028), and the
  room MUST show everyone who is joining and when.
- **FR-009**: Players MUST choose a display name before taking a seat; the name MUST be
  remembered on the player's device, prefilled next time, and changeable from the lobby.
- **FR-010**: Names MUST be validated (non-empty after trimming, a maximum length, unique within
  the room) with inline, localized error messages; names are shown as plain text only.
- **FR-011**: The player's chosen name MUST replace the South/North/East/West labels in every
  place a player is referred to (seats, turn indicator, scoreboard, hand results, notices),
  while the table MUST still make seat position and partnership evident.
- **FR-012**: Bots MUST have default friendly names that are clearly marked as bots.
- **FR-013**: The host MUST be able to start the game at any time with at least one human
  seated; any seat not taken by a human MUST be filled by a bot at start. The host role MUST
  transfer automatically to another human if the host leaves or is replaced.

**Resilience (highest priority)**

- **FR-014**: When a player's page is reloaded or reopened in the same browser, or their
  connection resumes, the app MUST automatically restore them to the same room and seat, with
  their current hand and the current table state, without any manual step.
- **FR-015**: The app MUST persist, on the player's device, whatever is needed to restore their
  identity, room, seat and name; and MUST offer "Return to your game" on the main menu if the
  player lands there while a room they belong to is still running.
- **FR-016**: While reconnecting, the app MUST keep retrying automatically with increasing
  intervals, show a clear "reconnecting" state, and let the player give up and return to the
  menu at any time.
- **FR-017**: When a human in a started game becomes unavailable, the game MUST pause
  immediately for everyone: no player or bot may act, the missing player and a visible
  countdown (starting at 1 minute) MUST be shown on every device, and the pause MUST lift
  automatically when the player returns or is replaced.
- **FR-018**: If the missing player has not returned when the countdown ends, a bot MUST take
  over their seat, play MUST continue from the exact current state, and all players MUST be
  told who was replaced.
- **FR-018a**: While the game is paused for a missing player, any remaining human MUST be able
  to skip the wait with a clearly labelled action (e.g., "Play with a bot now"), causing a bot
  to take that seat immediately exactly as if the minute had elapsed; the missing player keeps
  the right to reclaim the seat (FR-019). The skip action MUST NOT be offered to the missing
  player themselves.
- **FR-019**: A replaced player who returns later MUST be able to take their seat back with its
  current hand; the handover MUST occur at a safe point so no move is lost, duplicated, or
  applied twice, and the bot MUST stop acting for that seat.
- **FR-020**: When several humans are missing at once, the game MUST remain paused until each
  has returned or been replaced, and each countdown MUST be shown.
- **FR-021**: Every disconnection MUST get its own full one-minute grace period; there is no
  cumulative limit on paused time per player. Another human in the room cannot be forced to
  wait by a player who is not actually missing, because the pause lifts the moment the player
  is connected again.
- **FR-022**: The same identity connecting from a second tab or device MUST take over cleanly:
  the first connection is told it was superseded and offers to take control back; two
  connections MUST NOT control the same seat at once.
- **FR-023**: If a room can no longer be recovered (e.g., it has ended or the service lost it),
  the app MUST say so plainly within a bounded time and return the player to the main menu.
- **FR-024**: A room with no connected humans MUST be kept for 1 hour (during which the game
  remains paused and returning players resume) and then be closed with a "room has ended"
  outcome.
- **FR-025**: Seats held in the lobby by players who never return MUST be released after the
  same one-minute grace period.

**Play and integrity**

- **FR-026**: The server MUST remain authoritative: it validates every action against the rules
  and ownership of the acting seat; a player may only act for the seat they hold.
- **FR-027**: A player MUST only ever receive their own hand and information they are entitled
  to see; names, seat occupancy and connection status are visible to the whole room.
- **FR-028**: A bot taking over, or a human reclaiming a seat, MUST NOT alter game state or
  scoring; seat control is the only thing that changes.
- **FR-029**: Rooms MUST be independent: several rooms can run at the same time without
  interfering with each other.
- **FR-030**: After a game ends, the room MUST remain available so the same group can play
  again without rejoining.

**Accessibility and localization**

- **FR-031**: All new screens, messages and notices (menu, lobby, join errors, reconnecting,
  pause countdown, bot replacement) MUST be available in English and Bulgarian, operable by
  keyboard, usable at phone widths, and announce important state changes (pause, resume,
  replaced, reclaimed) to assistive technology.

### Key Entities

- **Room**: A shared place identified by a unique id/code and link. Has a host, four seats, a
  lifecycle (lobby → game in progress → game finished → ended), and an idle expiry.
- **Seat**: One of the four table positions. Is free, held by a human, or played by a bot (and,
  if the bot took over from a human, remembers which human may reclaim it).
- **Player (participant)**: A person in a room, identified by a device-held identity that
  persists across reloads. Has a display name, a seat (optional in lobby), and a connection
  status (connected, reconnecting with time left, replaced).
- **Player identity**: A private, device-stored token proving "this is the same person as
  before"; the basis for reconnection and seat reclaim.
- **Pause**: A room-wide hold on the game with the list of missing players and each one's
  remaining time before replacement.
- **Room link / code**: The shareable address and the short form of the room identifier that a
  person can type or paste.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new player can create a room and have the shareable link copied in under
  15 seconds from opening the app, and a friend can go from opening that link to sitting in a
  seat in under 20 seconds.
- **SC-002**: After a page reload or a network drop of up to 10 seconds, at least 95% of
  players are back at their seat with the correct table state within 5 seconds of the
  connection becoming available, with no manual action.
- **SC-003**: In 100% of tested disconnects, the game pauses for all other players within 2
  seconds of the disconnection being detected (immediately for a closed or reloaded page, and
  within 15 seconds for a silent network loss), and no move is accepted from any seat until the
  pause ends.
- **SC-004**: A player who does not return within 60 seconds is replaced by a bot, and play
  resumes for the others within 3 seconds of the minute elapsing, 100% of the time.
- **SC-005**: A replaced player who returns can resume their own seat with no lost, duplicated
  or reordered moves in 100% of tested handovers, including handovers during a bot's turn.
- **SC-006**: Across a full multi-human game with injected reloads and disconnects, the game
  always reaches a final result (no stuck or unrecoverable states) in every test run.
- **SC-007**: In usability checks, at least 90% of first-time participants can join a room from
  a link, and at least 90% of participants can explain why the game paused and what will
  happen next, without help.
- **SC-008**: At least 3 independent rooms with 4 simultaneous humans each can play at the same
  time without any observable cross-room interference or slowdown.
- **SC-009**: No participant at any point sees another player's hidden cards in what they
  receive.

## Assumptions

- **Storage and databases**: No database is introduced by this feature. Player identity, name
  and the "return to my room" reference live on the player's device. Live room and game state
  must necessarily be held by the running game service (it is the only place all players can
  share and trust), kept in memory only. **Consequence to flag**: if the service restarts,
  rooms in progress are lost and players see the "room has ended" outcome. A durable store
  would be needed only if surviving restarts, long-lived rooms, or game history/statistics
  become requirements; this will be raised explicitly if that point is reached.
- **No accounts**: Identity is per browser. Reclaiming a seat from a different browser or device
  is not supported; a player who loses their device identity joins as a new person.
- **Reconnect window**: 1 minute per disconnection per missing player, as requested, with no
  cumulative cap (FR-021). A player who keeps dropping can slow the table down; players can
  handle that socially for now, and a cap can be added later if it proves a problem.
- **Starting the game**: Only the host starts the game, and empty seats are filled by bots, so
  a solo player can still play (preserving today's play-against-bots experience as the
  one-person case of a room).
- **Teams**: Seating keeps the existing partnership layout (opposite seats are partners); no
  automatic team balancing or randomization of seats.
- **Spectators**: Not included; a room holds at most four humans and a person arriving when no
  seat can be taken over (all four held by humans, or held for replaced humans) is told the
  room is full.
- **Existing game rules/bots**: The rules engine, bots and in-game UI from earlier features are
  reused unchanged; this feature adds room, seat, naming and connection-lifecycle behavior
  around them. Bots replacing humans use the same bot behavior already available.
- **Room lifetime**: A room with no connected humans is retained for 1 hour before it ends.
- **Out of scope**: chat, voting to kick, public room lists, matchmaking, private/password
  rooms and persistence of game history.
- **Languages**: English and Bulgarian, consistent with the rest of the product.
