# Feature Specification: Playable Table UI

**Feature Branch**: `003-playable-table-ui`

**Created**: 2026-10-12

**Status**: Draft

**Input**: User description: "Let's build a bare bones UI - no flashy animations, menus - just a table so the game can be played with one player and 3 bots. Let's use react, tailwindcss for styles - no authentication/authorization for now. The game must be playable and the rules must work, SignalR must also connect reliably."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Play a complete game at the table (Priority: P1)

A person opens the game in a browser and sees a single table with four seats: themselves at
the bottom (South), their partner opposite (North), and two opponents to the left and right
(West, East). They start a game, and it begins on its own. They see their own hand, the cards
on the table, the current bid, and the score. On their turn they pick a bid, a card, or an
announcement; on the bots' turns the table updates by itself. The game runs through every hand
until one team wins, and the person is told who won and the final score.

**Why this priority**: A complete, playable game is the whole point of the feature; everything
else supports it.

**Independent Test**: Open the page, start a game, and play every turn using only what the page
offers, until a winner is shown. No other tool or console is needed.

**Acceptance Scenarios**:

1. **Given** the page is open and connected, **When** the person starts a game, **Then** a
   table appears with their hand dealt, three opponents' seats shown (with the number of cards
   each holds, never the cards themselves), and the first bidding turn proceeds.
2. **Given** it is the person's turn to bid, **When** they look at the table, **Then** they
   see only the bids that are currently allowed and can choose one.
3. **Given** it is the person's turn to play, **When** they look at their hand, **Then** the
   cards they may legally play are clearly distinguishable from those they may not, and
   choosing a legal card plays it.
4. **Given** it is a bot's turn, **When** the bot acts, **Then** the table shows its bid or
   card without the person doing anything.
5. **Given** a team reaches the target score, **When** the game ends, **Then** the winning
   team and final score are shown and no further moves are offered.
6. **Given** a finished game, **When** the person chooses to play again, **Then** a fresh game
   starts at the same table.

---

### User Story 2 - Rules are enforced and mistakes are explained (Priority: P1)

The person can never put the game into an illegal state from the UI. Anything the rules forbid
is either not offered or, if attempted, refused with a short plain-language explanation, and
the table stays exactly as it was.

**Why this priority**: The user explicitly requires that "the rules must work." The UI must
faithfully surface the rules the server enforces.

**Independent Test**: Over many games, try to play out of turn, play an off-suit card when
suit must be followed, and bid when not permitted; confirm each is either impossible to
select or refused with a reason, and that the game continues normally afterward.

**Acceptance Scenarios**:

1. **Given** it is not the person's turn, **When** they try to act, **Then** nothing is
   played and the table indicates that it is not their turn.
2. **Given** the person must follow suit, **When** they try to play a card that breaks that
   rule, **Then** the card is not played, a reason is shown, and their hand is unchanged.
3. **Given** a refused action, **When** the person then makes a legal move, **Then** the game
   proceeds normally with no leftover error state.
4. **Given** a hand where everyone passes, **When** the bidding ends, **Then** the table shows
   that the hand was voided and a new hand is dealt.
5. **Given** the person has declarable announcements, **When** it is time to declare, **Then**
   only the announcements the rules currently allow are offered.

---

### User Story 3 - Reliable connection with clear status (Priority: P1)

The person always knows whether the page is connected to the game server. Opening the page
connects automatically; if the server is not yet reachable the page keeps trying and says so;
if the connection drops mid-game the page says so, tries to re-establish it, and never
silently shows stale information as though it were current. Moves cannot be submitted while
disconnected.

**Why this priority**: The user explicitly requires that the real-time connection "connect
reliably." A table that silently freezes is worse than one that says it is disconnected.

**Independent Test**: Load the page before the server is running and then start the server;
start a game and stop/restart the server or cut the network; confirm the page reports each
state accurately and recovers to a usable state without a manual reload where possible.

**Acceptance Scenarios**:

1. **Given** the server is running, **When** the page is opened, **Then** it connects
   automatically within a few seconds and shows that it is connected.
2. **Given** the server is not reachable, **When** the page is opened, **Then** it shows that
   it is connecting or unavailable, keeps retrying, and becomes usable as soon as the server
   is reachable, without a manual reload.
3. **Given** a game is in progress, **When** the connection drops, **Then** the page shows a
   clearly visible disconnected state, disables move input, and attempts to reconnect.
4. **Given** the connection has dropped or the page has been refreshed, **When** the
   connection is re-established, **Then** the interrupted game is abandoned, the page clearly
   tells the person the game was lost, and a new game can be started immediately — never an
   unusable blank table and never blocked by the abandoned game.
5. **Given** updates arrive from the server, **When** they are shown, **Then** the table
   always reflects the most recent state in order, never an older one.

---

### User Story 4 - Readable and operable on phone and desktop (Priority: P2)

The table is plain but usable: it fits a phone screen first and scales up to desktop, every
interaction works with touch and keyboard, and the interface is available in both English and
Bulgarian. It has no animations and no menus; the only controls are the ones the current turn
needs (bid choices, card choices, announcements, start/play again).

**Why this priority**: Required by project principles (mobile-first, accessible, bilingual)
and keeps the "bare bones" promise, but a desktop-only English table would already let the
game be played.

**Independent Test**: Play a full game on a phone-sized viewport using touch only, then on a
desktop using keyboard only; switch language and confirm every visible string changes.

**Acceptance Scenarios**:

1. **Given** a phone-sized screen, **When** the table is shown, **Then** all four seats, the
   person's hand, the table cards, the bid and the score are visible and usable without
   horizontal scrolling.
2. **Given** a keyboard only, **When** the person plays, **Then** every action available by
   pointer is reachable and operable by keyboard.
3. **Given** any card or suit, **When** it is shown, **Then** its rank and suit are conveyed
   by text, not by color or symbol alone, and are available to assistive technology.
4. **Given** a browser set to Bulgarian or English, **When** the page opens, **Then** it
   appears in that language and the person can switch language at any time.

---

### Edge Cases

- The person opens the page in two browser tabs: the game belongs to the most recent
  connection; the earlier tab is told its game was lost and may start a new one, and neither
  tab is left showing a live-looking but dead table.
- The person double-taps a card quickly: at most one move is played; the second tap is
  ignored or refused without error noise.
- A bot move arrives while the person is mid-selection (e.g., choosing an announcement): the
  selection is not lost or misapplied.
- The server restarts mid-game: the game is lost; the page reports the connection loss and the
  lost game and allows starting a new one.
- The completed trick: after the fourth card, the full trick and its winner remain visible
  until the next card is played, so the person can see what happened.
- Very slow connection: the page shows that an action is pending and prevents submitting the
  same move twice.
- A turn with only one legal card: the table still waits for the person's action (no silent
  auto-play).

## Requirements *(mandatory)*

### Functional Requirements

**Table and game flow**

- **FR-001**: The page MUST present a single table showing four seats — South (the person),
  North (partner), West and East (opponents) — with each seat labelled by name and team.
- **FR-002**: The person MUST be able to start a game from the page with one action, and the
  game MUST then proceed to a final winner using the existing rules engine, with three bots in
  the other seats.
- **FR-003**: The page MUST show the person's own hand, the number of cards each other seat
  holds, the cards currently played to the table, whose turn it is, the dealer, the current
  bid / contract and who holds it, announcements made public, and the running score of both
  teams.
- **FR-004**: The page MUST keep the most recently completed trick, including its winner,
  visible until the next card is played.
- **FR-005**: The page MUST show the final result (winning team and final score) when the
  game finishes, and MUST offer a way to start a new game.

**Input and rules**

- **FR-006**: The person MUST be able to place bids, play cards, and declare announcements
  entirely from the page.
- **FR-007**: The page MUST offer only the actions the server reports as legal for the person
  at that moment, and MUST visibly distinguish playable from unplayable cards.
- **FR-008**: The page MUST NOT contain game-rule logic of its own: legality, scoring, trick
  winners, and turn order are taken solely from what the server reports.
- **FR-009**: When the server refuses an action, the page MUST show the reason in plain
  language (localized), leave the displayed game state unchanged, and allow the person to
  continue.
- **FR-010**: The page MUST prevent submitting the same action twice while the first is still
  awaiting its result.
- **FR-011**: The page MUST show only information the person is entitled to see; it MUST NOT
  display other players' hands or undealt cards.

**Connection**

- **FR-012**: The page MUST connect to the game server automatically on load and display the
  connection state at all times (connecting, connected, reconnecting, unavailable).
- **FR-013**: The page MUST retry a failed or dropped connection automatically with
  increasing waits, without requiring a manual reload, and MUST resume normal operation as
  soon as a connection is available.
- **FR-014**: While not connected, the page MUST disable all move input and MUST make clear
  that the displayed state may be out of date.
- **FR-015**: After a connection is re-established, the page MUST obtain the current state
  from the server before allowing input, and MUST apply updates strictly in the order they
  occurred, detecting and recovering from any gap or duplicate.
- **FR-023**: The system MUST NOT let a game abandoned by a disconnected player prevent that
  player (or a new connection) from starting a new game; the abandoned game is discarded.
- **FR-016**: The page MUST NOT end up in a state with no usable action: after any connection
  failure, rejected request, or lost game, the person MUST be able to reach a state from
  which a new game can be started.

**Presentation**

- **FR-017**: The interface MUST be limited to the table and the controls the current turn
  needs; it MUST NOT include menus, animations, sound, or decorative effects.
- **FR-018**: The layout MUST be designed for phone screens first and remain usable on tablet
  and desktop without horizontal scrolling.
- **FR-019**: Every action MUST be operable by touch, pointer, and keyboard.
- **FR-020**: Every card MUST be identifiable by rank and suit in text and to assistive
  technology; color and suit symbol MUST NOT be the only means of conveying information.
- **FR-021**: All user-facing text MUST be available in English and Bulgarian, defaulting to
  the browser's language, with the person able to switch at any time.
- **FR-022**: The page MUST NOT require sign-in or any account; there is no authentication or
  authorization in this feature.

### Key Entities

- **Table View**: What the page shows at any moment — the person's seat, the phase (bidding,
  playing, finished), dealer, whose turn it is, own hand, other seats' card counts, cards on
  the table, last completed trick, contract, public announcements, scores, and legal actions.
- **Seat**: One of South (person), North (partner), West, East (opponents), belonging to a
  team.
- **Action**: A bid, a card play, or an announcement the person chooses; accepted or refused
  with a reason.
- **Connection State**: Whether the page is connecting, connected, reconnecting, or
  unavailable, and whether the displayed table is current.
- **Game Result**: Winning team and final score of a finished game.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person who has never seen the page can start and finish a complete game
  against three bots, using only the page, without any instructions or tooling.
- **SC-002**: Across 50 consecutive complete games, 0 games end in a stuck or unusable
  table, and every game reaches a displayed winner and final score.
- **SC-003**: In every game, 100% of actions refused by the rules are explained to the
  person, and 0 actions leave the displayed table in a state different from the server's.
- **SC-004**: With the server running, the page shows a connected state and is ready to start
  a game within 3 seconds of opening in 95% of loads.
- **SC-005**: When the server is stopped and restarted (or the network is cut and restored)
  during a session, the page reports the loss within 5 seconds and returns to a usable state
  within 10 seconds of the server being reachable again, in 100% of trials, with no manual
  reload.
- **SC-006**: No other seat's hand or undealt card is ever displayed or present in what the
  page receives, across all games played in testing.
- **SC-007**: A complete game can be played using only touch on a 375-pixel-wide screen and
  using only a keyboard on desktop, with every card announced by rank and suit.
- **SC-008**: 100% of visible interface text is available in both English and Bulgarian.

## Clarifications

### Session 2026-10-12

- Q: After a dropped connection or browser refresh, must the same game be resumable? → A: No — the interrupted game is abandoned and a new one can be started; resuming is out of scope. An unfinished game left by a disconnected player MUST NOT block creating a new one (a small change to the feature 002 server behaviour).

## Assumptions

- The live table backend from feature 002 (single table, one human in South, three baseline
  bots, server-validated intents, per-seat views, in-order updates) is reused as the source
  of truth; this feature adds no new game rules.
- As requested, the page is a web client built with React and styled with Tailwind CSS, and
  it talks to the server over its existing real-time (SignalR) connection.
- No authentication or authorization: anyone who can reach the page may play; there is one
  table per server and one person at it.
- Bots play at the server's default human-followable pace; no pace control is exposed in this
  UI.
- Resuming an interrupted game is out of scope; abandoned games are discarded.
- Seeds, rule-set selection, multiple tables, other humans, bot difficulty, chat, persistence
  of games, and statistics are out of scope.
- Bulgarian and English are required by the project constitution and are included despite the
  "bare bones" request; no further languages are in scope.
- The page is served so that the browser can reach the existing server; how it is hosted is a
  planning decision.
