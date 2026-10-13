# Feature Specification: Modern Game UI

**Feature Branch**: `005-modern-game-ui`

**Created**: 2026-10-13

**Status**: Draft

**Input**: User description: "Let's improve the UI - we need a modern but minimal game UI. No need for flashy animations, but some animations for the cards are needed. Right now the cards are removed and dealt way too fast, reduce that a bit. Also, there is no order in the hand - cards should always be ordered by suits and then power. There needs to be a better representation of who is dealing the cards, also some visuals for the hands, the players and the table. The background should be dark and follow a nice design theme. If needed, you can introduce new libraries, but don't overdo it. Out of scope are menus, room management - just implement the final UI/UX for the game itself."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An ordered, readable hand (Priority: P1)

The person's hand is always shown in a consistent, predictable order: grouped by suit, and
within each suit sorted from the strongest card to the weakest according to the power the cards
have in the current hand. When the contract changes what a card is worth (for example a suit
becoming trumps, or no-trumps), the order reflects the new power. Cards in the hand are shown
as a tidy, overlapping fan or row so a full hand fits comfortably on a phone and on a desktop.

**Why this priority**: An unordered hand is the most concrete usability defect today and affects
every single turn of every game. It is also independent of any visual redesign.

**Independent Test**: Deal several hands and, for each, confirm the cards appear grouped by suit
and sorted by power within each suit; play a card and confirm the remaining cards keep their
order; reach a hand with a trump suit and confirm that suit's cards are ordered by trump power.

**Acceptance Scenarios**:

1. **Given** a freshly dealt hand, **When** it appears, **Then** cards of the same suit sit
   together and, within a suit, run from strongest to weakest.
2. **Given** a hand in progress, **When** the person plays a card, **Then** the remaining
   cards keep their grouping and order with no reshuffling jumps.
3. **Given** the contract is decided (a suit as trumps, or no-trumps), **When** the hand is
   shown, **Then** the order within each suit uses that contract's card power, and the order
   of suit groups is the same on every hand and every deal so the person always knows where to
   look.
4. **Given** the person's cards are shown, **When** they look at which cards may be played,
   **Then** playable cards are clearly distinguishable from unplayable ones without changing
   the order.

---

### User Story 2 - Paced, readable card movement (Priority: P1)

Cards visibly move rather than appearing and vanishing: they are dealt from the dealer to each
seat, they travel from a hand to the centre of the table when played, and a completed trick is
gathered and swept toward the winner. All of this happens at a calm, readable pace — slower
than today, where cards are dealt and cleared almost instantly — so the person can follow what
every player did. Motion is subtle and functional, never flashy.

**Why this priority**: The user explicitly called out that deals and trick clearing are too
fast and that card animation is needed. Without pacing the person cannot follow bot play.

**Independent Test**: Start a game and watch a deal, a full trick played by bots, and the trick
being collected; confirm each card is visibly seen entering the table, that the completed trick
stays visible long enough to read before being cleared, and that the next player's action does
not begin until the previous motion has settled.

**Acceptance Scenarios**:

1. **Given** a new hand begins, **When** cards are dealt, **Then** they are seen to leave the
   dealer's position toward each seat in turn, over a noticeable but short duration, not
   instantaneously.
2. **Given** a player plays a card, **When** it arrives, **Then** it visibly travels from that
   player's seat to its place on the table.
3. **Given** the fourth card of a trick has been played, **When** the trick completes, **Then**
   all four cards remain visible for a brief pause so the result can be read, and are then
   collected toward the winning seat.
4. **Given** several bots act in a row, **When** their moves arrive faster than the motion can
   play, **Then** the table presents them one after another at the readable pace rather than
   skipping or overlapping them, and always ends showing the true current state.
5. **Given** the person prefers reduced motion in their system settings, **When** cards move,
   **Then** movement is replaced by simple fades or immediate placement while still pausing
   long enough for the trick to be read.

---

### User Story 3 - Clear table, players and dealer (Priority: P2)

The table looks like a table. The four players are shown as distinct, recognisable seats around
a felt-style playing surface: each seat shows who they are (you, your partner, opponents), how
many cards they hold as a small visual hand of card backs, and whether it is their turn. The
dealer is unmistakable — a visible dealer marker at the seat that is dealing, and it is clear
that the deal originates from that seat and passes to the next player when the next hand
begins. The current contract, whose turn it is, and the score are visible at a glance without
crowding the cards.

**Why this priority**: This is the core of "better representation of who is dealing, and visuals
for the hands, the players and the table", but the game is already playable without it, so it
ranks just below the pacing and ordering fixes.

**Independent Test**: Start a game and, without reading any instructions, identify the dealer,
whose turn it is, how many cards each opponent holds, who is on the person's team, and the
current contract and score; then play through two hands and confirm the dealer marker moves to
the next seat.

**Acceptance Scenarios**:

1. **Given** a hand is in progress, **When** the person looks at the table, **Then** exactly
   one seat carries a clear dealer marker, and it is not conveyed by colour alone.
2. **Given** a new hand starts, **When** the previous dealer's role ends, **Then** the dealer
   marker moves to the next seat and the following deal visibly begins from that seat.
3. **Given** it is a seat's turn, **When** the table is shown, **Then** that seat is
   highlighted distinctly from the others and the person can tell whose turn it is from a
   glance across the table.
4. **Given** opponents hold cards, **When** the table is shown, **Then** each opponent's
   remaining cards are shown as a compact visual hand of card backs, whose size matches the
   number of cards that opponent actually holds.
5. **Given** the person's partner and the two opponents, **When** they look at the seats,
   **Then** it is clear which seats are on their team and which are not.
6. **Given** the contract and score, **When** the table is shown, **Then** both are visible
   without opening anything and update as the game progresses.

---

### User Story 4 - A cohesive dark theme (Priority: P2)

The whole game surface uses a dark, minimal visual theme: a dark background, a restrained
palette with a single accent colour, consistent typography, spacing and card styling, and clear
contrast for everything the person must read. The result feels modern and calm rather than
decorative, and bidding choices, announcements, the score, the results panel and any
notifications share the same visual language as the table.

**Why this priority**: The theme is an explicit requirement and defines the look of the
product, but it is purely presentational and can follow the structural fixes.

**Independent Test**: Open every state of the game (waiting to start, bidding, announcing,
playing, trick complete, game over, disconnected) and confirm each uses the same dark theme with
legible text and no leftover light-coloured default styling.

**Acceptance Scenarios**:

1. **Given** any state of the game, **When** the person views the page, **Then** the
   background is dark and every element uses the shared palette, typography and card styling.
2. **Given** text, buttons and cards, **When** assessed for readability, **Then** they meet
   accessible contrast against the dark background.
3. **Given** suits are shown on cards, **When** they are viewed, **Then** each suit is
   distinguishable by shape and not by colour alone.
4. **Given** the person uses a phone-sized screen, **When** the table is shown, **Then** all
   seats, the trick area, the person's hand and the available actions are visible and usable
   without horizontal scrolling.
5. **Given** the person uses a desktop-sized screen, **When** the table is shown, **Then** the
   layout makes good use of the extra space rather than stretching the phone layout.

---

### Edge Cases

- A new game is started while a previous deal or trick animation is still playing: the table
  must not show leftover cards from the earlier game.
- The connection drops or the game is lost mid-animation: motion stops and the disconnected or
  lost-game state is shown immediately, not after the animation completes.
- A bot plays several cards in very quick succession (or the page regains focus after being in a
  background tab): the table catches up in order, without long delays building up and without
  ever showing a state older than the latest.
- The hand is empty or has a single card: the hand area keeps a stable layout.
- A hand where everyone passes and the hand is voided: no cards are dealt animatedly in a way
  that suggests a normal deal began; the re-deal is clearly communicated and the dealer marker
  moves.
- The person has only unplayable cards in a given suit or a single legal card: order and
  grouping remain unchanged, with the legal card(s) distinguished.
- Very small phone widths and very wide desktop widths: no overlapping controls, no cards cut
  off, and no horizontal page scroll.
- Text length differs between English and Bulgarian: seat labels, buttons and panels still fit
  without truncating essential information.
- The person has reduced-motion preferences set: no movement-based animation, but all state
  changes are still communicated.

## Requirements *(mandatory)*

### Functional Requirements

**Hand order**

- **FR-001**: The system MUST always present the person's hand grouped by suit, with each suit
  group sorted from strongest to weakest card.
- **FR-002**: The system MUST use the card power of the active contract (trump suit, or
  no-trumps) when sorting within a suit, and MUST keep the order of the suit groups consistent
  from deal to deal.
- **FR-003**: The system MUST preserve the grouping and order as cards are played and MUST NOT
  rearrange remaining cards in a way that makes them jump.
- **FR-004**: The system MUST NOT alter which cards are legal or how they are played as a result
  of sorting; ordering is presentational only.

**Card motion and pacing**

- **FR-005**: The system MUST show cards moving between positions (dealer to seats when dealt,
  seat to table when played, table to winner when a trick is collected) instead of appearing and
  disappearing instantly.
- **FR-006**: The system MUST pace dealing and trick collection at a deliberately readable
  speed that is noticeably slower than the current behaviour.
- **FR-007**: The system MUST hold a completed trick visible for a short pause before it is
  collected so that the played cards and the winner can be read.
- **FR-008**: The system MUST present rapid sequences of server updates (for example several bot
  moves in a row) in order and at the readable pace, and MUST always converge on the latest true
  state without ever displaying an older one as current.
- **FR-009**: The system MUST NOT block or delay the person's own input longer than necessary:
  when it is the person's turn and the table has settled, input MUST be available immediately.
- **FR-010**: The system MUST honour the person's reduced-motion preference by replacing
  movement with non-moving transitions while keeping the same pauses and information.
- **FR-011**: Animation MUST remain limited to functional card movement and brief state
  emphasis; the system MUST NOT add decorative, looping or attention-seeking effects.

**Table, players and dealer**

- **FR-012**: The system MUST display the four seats as distinct players arranged around a
  table surface, with the person at the bottom and their partner opposite.
- **FR-013**: The system MUST show, for every seat, who the player is (you, partner, opponent),
  how many cards that player holds as a visual hand of card backs, and whether it is that
  player's turn.
- **FR-014**: The system MUST mark exactly one seat as the dealer at all times during a game,
  using a marker that is identifiable by shape or text and not by colour alone, and MUST move it
  to the next seat when a new hand begins.
- **FR-015**: The system MUST make the origin of the deal visible, so that each deal visibly
  starts from the dealer's seat.
- **FR-016**: The system MUST make clear which seats form the person's team and which form the
  opposing team.
- **FR-017**: The system MUST show the current contract, whose turn it is, and the running
  score at a glance on the table without requiring the person to open anything.
- **FR-018**: The system MUST visually distinguish the person's playable cards from unplayable
  ones, and the person's turn from waiting states, without relying on colour alone.

**Theme and layout**

- **FR-019**: The system MUST use a dark, minimal theme across every game state (waiting to
  start, bidding, announcing, playing, trick complete, game over, disconnected, game lost,
  notices).
- **FR-020**: The system MUST use one consistent palette with a single accent colour, one
  typographic scale, and one card style across all elements.
- **FR-021**: The system MUST keep all text and interactive elements at accessible contrast
  levels against the dark background.
- **FR-022**: The system MUST remain fully usable on phone-sized screens first, with a layout
  that also adapts to tablet and desktop sizes, and MUST NOT require horizontal scrolling.
- **FR-023**: The system MUST remain operable by keyboard and expose meaningful accessible
  labels for every card (rank and suit), seat, dealer marker and state change, as in the
  current UI.
- **FR-024**: The system MUST keep all user-facing text localized in both English and
  Bulgarian, including any new labels (such as the dealer marker and team indications).

**Scope boundaries**

- **FR-025**: The system MUST NOT introduce menus, settings screens, lobbies or room
  management; the feature covers only the in-game table experience and its immediate
  start/play-again prompts.
- **FR-026**: The system MUST NOT change game rules, game outcomes, or what information is sent
  to the client; all changes are in how already-available information is presented. The only
  permitted server-side change is the order in which the person's own cards are listed, so that
  card power comes from the active rule set rather than being duplicated in the interface.

### Key Entities *(include if feature involves data)*

- **Seat**: One of four positions at the table (the person, their partner, and two opponents),
  with a role relative to the person, a team, a card count, and turn and dealer indications.
- **Hand**: The person's own cards, presented as an ordered collection grouped by suit with
  power ordering within each suit and playable/unplayable distinction.
- **Opponent Hand**: A compact visual representation of the number of cards another seat
  holds, shown as card backs.
- **Trick**: The cards currently played to the centre of the table, held visibly until
  collected by the winning seat.
- **Dealer Marker**: The indicator of which seat deals the current hand and where each deal
  originates.
- **Theme**: The shared dark palette, accent colour, typography, spacing and card styling
  applied across the game.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of dealt hands, the person's cards are shown grouped by suit and sorted by
  power within each suit, and remain so throughout the hand.
- **SC-002**: A completed trick remains visible for at least one full second before being
  collected, and the cards of a deal are seen arriving over at least one second in total, so
  that a first-time viewer can state who won each trick without replaying it.
- **SC-003**: When asked at any point mid-game, a first-time player can correctly identify the
  dealer, whose turn it is, and which seats are their team within 5 seconds, without
  instructions.
- **SC-004**: The person's input is available within half a second of the table settling on
  their turn, and a sequence of consecutive bot moves never causes the displayed state to lag
  more than a few seconds behind the true state.
- **SC-005**: Every game state passes an accessible-contrast check for text and controls on the
  dark background, and every suit is distinguishable without colour.
- **SC-006**: A full game can be played start to finish on a phone-sized screen with no
  horizontal scrolling and with all seats, the hand and the actions on screen at once, and the
  same game can be played with keyboard only.
- **SC-007**: With a reduced-motion preference set, a full game can be played with no
  movement-based animation and no loss of information.
- **SC-008**: At least 90% of people trying the new table say it looks modern and easy to read
  in a quick informal review, with no one needing to ask what a seat, marker or card state
  means.

## Assumptions

- This feature changes presentation only; game rules, bots, server behaviour and the data the
  client receives remain unchanged (apart from the listing order of the person's own cards, see
  FR-026).
- The hand order is: suits in a fixed alternating-colour order (so adjacent suit groups differ
  in colour), with each suit sorted strongest to weakest by the active contract's power; before
  a contract is known, the default (non-trump) power order is used.
- The current game remains one person plus three bots at a single table; multi-player lobbies,
  menus and room management are out of scope, as stated.
- The existing start and play-again prompts, result panel and notices are restyled to match the
  theme but keep their current behaviour.
- A small animation library (or equivalent) may be introduced if it keeps the footprint small;
  no heavy graphics or game engine is warranted.
- Pacing values (deal speed, trick hold, trick collection) are tuned to feel calm but not
  sluggish; a trick hold of roughly one to two seconds and a deal in the low seconds are
  reasonable starting points.
- The dark theme is the only theme required; a light theme or user-selectable themes are out of
  scope.
- Existing accessibility, bilingual and mobile-first expectations of the project apply to all
  new UI.
