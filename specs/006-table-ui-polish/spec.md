# Feature Specification: Table UI Polish

**Feature Branch**: `006-table-ui-polish`

**Created**: 2026-10-13

**Status**: Draft

**Input**: User description: "some UI Improvements: 1. The content on the page doesn't fit a normal laptop monitor's height - there isn't enough components for that to happen - try to make it so I don't have to scroll to see my cards. 2. On the bidding phase, all cards appear as illegal to play, but that's not needed - just display them normally. 3. There is weird spacing between them and some cards are sometimes higher - make sure they are aligned always, even if they are different suits. In the playing phase, you can highlight the legal moves, but don't do that weird spacing. 4. The bidding is confusing and goes too fast - make sure you pause a little when bots are making their bids. Also, add inside the players/bots boxes where their name/location is what was their original bid. If they are multiple include them also, that helps in-game. 5. Post game, show a pop-up window with all hands of both teams and the score from the current game and the entire game as a whole. The hands can be concatenated into a single row of cards, no need to show them hand by hand, but they should be separated by team."

## Clarifications

### Session 2026-10-13

- Q: How much detail should the end-of-hand pop-up show for each team's hand score? → A: Totals plus a short breakdown per team (card points, announcements/belote bonuses, and whether the contract was made or failed)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Everything fits on one laptop screen (Priority: P1)

The person opens the table on a typical laptop display (around 1366×768 up to 1920×1080,
browser chrome included) and sees the whole table - the other three seats, the centre of the
table, status information and their own hand of cards - without scrolling vertically. The
layout adapts to the available height rather than leaving large empty areas that push the
person's cards below the fold.

**Why this priority**: Having to scroll to reach your own cards breaks the core interaction on
every turn of every game. Nothing else matters if the hand is not reachable at a glance.

**Independent Test**: Open the table at 1366×768 and 1920×1080 viewport sizes through each phase
(waiting, bidding, playing, between hands) and confirm the person's cards are fully visible and
no vertical page scrollbar appears.

**Acceptance Scenarios**:

1. **Given** a laptop-sized viewport, **When** the table is shown during bidding, **Then** all
   four seats, the bidding information and the person's whole hand are visible without scrolling.
2. **Given** a laptop-sized viewport, **When** the table is shown during play with a trick on
   the table, **Then** the trick, all four seats and the person's whole hand are visible without
   scrolling.
3. **Given** a viewport taller or shorter than the common range, **When** the table is shown,
   **Then** the layout scales so the hand stays fully visible, and nothing overlaps or is cut off.

---

### User Story 2 - A tidy, evenly aligned hand (Priority: P1)

The cards in the person's hand sit on a single straight line with even, consistent spacing.
No card sits higher or lower than its neighbours because of its suit, rank or position, and
there are no unexplained gaps between cards (including between suit groups). The only
difference in a card's vertical position is the deliberate, temporary emphasis for a card the
person is pointing at or has selected.

**Why this priority**: Misaligned and unevenly spaced cards make the hand hard to read and look
broken; it is visible on every turn, and independent of the other stories.

**Independent Test**: Deal several hands with different suit mixes and confirm every card shares
the same baseline and the gaps between all adjacent cards are equal, in every game phase.

**Acceptance Scenarios**:

1. **Given** a hand containing cards of all four suits, **When** it is shown, **Then** all cards
   share one baseline and adjacent cards are spaced equally.
2. **Given** a hand with a single suit or an odd number of cards, **When** it is shown, **Then**
   alignment and spacing are still uniform and the hand is centred.
3. **Given** the person plays cards through a hand, **When** the remaining cards re-flow,
   **Then** they stay aligned and equally spaced at every step.

---

### User Story 3 - Cards look normal while bidding (Priority: P1)

During the bidding phase no card in the hand is dimmed, greyed out or otherwise marked as
unplayable, because cards cannot be played yet. The hand is shown in its normal appearance. In
the playing phase, cards that are legal to play are highlighted and illegal ones are shown as
unavailable, with this being the only difference between them - it never changes spacing or
alignment (see User Story 2).

**Why this priority**: Marking every card as illegal during bidding is misleading and makes the
person think something is wrong.

**Independent Test**: Reach the bidding phase and confirm all cards look identical in style and
none appear disabled; reach the playing phase on the person's turn and confirm legal cards are
highlighted and the others are visibly unavailable, with no shift in layout.

**Acceptance Scenarios**:

1. **Given** the bidding phase, **When** the hand is shown, **Then** no card appears disabled or
   unplayable.
2. **Given** the playing phase and it is the person's turn, **When** the hand is shown, **Then**
   legal cards are highlighted, illegal cards are visibly unavailable, and every card keeps the
   same position and spacing as it had without highlighting.
3. **Given** the playing phase and it is not the person's turn, **When** the hand is shown,
   **Then** the hand is shown without misleading "playable" highlighting.

---

### User Story 4 - Followable bidding with visible bid history (Priority: P2)

Bidding happens at a pace the person can follow: after each bot bids or passes, there is a
short, noticeable pause before the next player acts, so every call can be read. Each player's
seat box also shows what that player bid - their call(s) in this round of bidding, including
passes and any raises or repeated bids, in the order they were made - so the person can see at
a glance who bid what without remembering it.

**Why this priority**: Bidding is currently hard to follow, which makes it hard to play well;
it is more of a comfort and clarity improvement than a blocker, so it follows the layout and
card fixes.

**Independent Test**: Start a hand against bots and confirm there is a clearly perceptible
pause between consecutive bot calls; confirm each seat box lists that player's calls, and that
a player who made several calls shows all of them in order.

**Acceptance Scenarios**:

1. **Given** several bots are due to bid in a row, **When** each makes its call, **Then** there
   is a clearly perceptible pause between consecutive calls and each call is visible before the
   next one replaces it.
2. **Given** a player has bid (or passed), **When** the table is shown, **Then** that player's
   seat box displays their call.
3. **Given** a player made more than one call in the same round of bidding, **When** the table
   is shown, **Then** all of their calls are displayed in the order made, and the most recent
   is distinguishable.
4. **Given** bidding is finished and play begins, **When** the table is shown, **Then** each
   seat still shows the calls it made so the contract's origin remains clear, until the next
   hand is dealt, at which point the seat boxes are cleared.

---

### User Story 5 - End-of-hand results pop-up (Priority: P2)

When a hand finishes, a pop-up appears over the table showing how it went. It shows, split by
team, all the cards that team won during the hand as one continuous row of cards (not
trick-by-trick), the score for the hand that just ended for each team with a short breakdown
of how it was made up (card points, announcements/belote bonuses, and whether the contract was
made or failed), and the running total for the whole game for each team. When the whole game is over, the pop-up makes the winner
clear. The person dismisses the pop-up to continue to the next hand (or to start a new game).

**Why this priority**: It gives the person the closure and verification they currently lack
after a hand, but play remains possible without it.

**Independent Test**: Finish a hand and confirm a pop-up appears with two clearly separated
team sections each containing a single row of the cards that team won, the hand score and the
game total per team; finish a whole game and confirm the winner is indicated.

**Acceptance Scenarios**:

1. **Given** a hand has just ended, **When** the pop-up appears, **Then** it shows two
   separated team sections, each a single row of that team's won cards, and the number of cards
   shown across both sections equals the cards played in the hand.
2. **Given** the pop-up is shown, **When** the person reads it, **Then** each team's score for
   the hand, its breakdown (card points, bonuses, contract made or failed) and each team's
   total for the whole game are displayed and labelled distinctly, and the breakdown parts add
   up to the hand score.
3. **Given** the pop-up is shown, **When** the person dismisses it, **Then** the table returns
   to normal and the game proceeds to the next hand.
4. **Given** the game has ended, **When** the pop-up is shown, **Then** it states which team
   won the game and offers the way to start a new one.
5. **Given** the person's team and the other team, **When** the pop-up is shown, **Then** it is
   unambiguous which team is "yours".

---

### Edge Cases

- A hand where one team won every trick: that team's row holds all the cards and the other
  team's section is shown as empty rather than missing.
- A hand that ends with no play (everyone passes and the hand is redealt): the pop-up is not
  shown with empty team rows; the person is told the hand was redealt instead.
- A very narrow viewport (phone width): the hand and seats remain fully visible, using a layout
  that adapts (for example, a row that fits or compresses) rather than being cut off.
- A long row of won cards (up to the whole deck): the row remains readable within the pop-up,
  wrapping or compressing as needed without requiring scrolling the page.
- A bot acting while the page is in a background tab: the table catches up in order without a
  build-up of long delays.
- The person reloads or reconnects during bidding: seat boxes still show the calls already made.
- Reduced-motion preference: pacing pauses still apply, but any decorative motion in the
  pop-up is minimised.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The table MUST fit within the visible viewport height on common laptop displays
  (1366×768 and larger) during every phase, such that the person's full hand is visible with no
  vertical scrolling.
- **FR-002**: The layout MUST scale to the available viewport height so that the hand remains
  fully visible without overlap or clipping at heights below and above the common range.
- **FR-003**: All cards in the person's hand MUST sit on a single shared baseline and be spaced
  evenly; suit, rank or phase MUST NOT change an individual card's vertical position or the gap
  to its neighbours.
- **FR-004**: Only deliberate interaction states (pointing at or selecting a card) MAY raise
  a single card, and doing so MUST NOT move or re-space any other card.
- **FR-005**: During the bidding phase, the hand MUST be displayed in the normal appearance, with
  no card shown as disabled, unplayable or illegal.
- **FR-006**: During the playing phase on the person's turn, legal cards MUST be visibly
  highlighted and illegal cards visibly unavailable, using colour, border or similar emphasis
  that does not alter card position or spacing.
- **FR-007**: The system MUST insert a perceptible pause (long enough to read the call, on the
  order of one second) between consecutive bot bidding calls so that each call can be seen
  before the next appears.
- **FR-008**: The system MUST NOT add any pause to the person's own bidding actions, and MUST
  ensure the person can always see whose turn it is and what the last call was.
- **FR-009**: Each player's seat box MUST display that player's call(s) for the current bidding
  round - including passes and any later raises - in the order they were made.
- **FR-010**: Where a player made more than one call, the seat box MUST show all of them, and
  the most recent call MUST be visually distinguishable.
- **FR-011**: Seat boxes MUST keep showing their calls after bidding completes, until the next
  hand is dealt, at which point they MUST be cleared.
- **FR-012**: When a hand finishes, the system MUST show a pop-up over the table that remains
  until the person dismisses it.
- **FR-013**: The pop-up MUST show, per team, the cards that team won in the hand as a single
  continuous row (not grouped per trick), with the two teams in clearly separated, labelled
  sections.
- **FR-014**: The pop-up MUST show each team's score for the hand that just ended, a short
  breakdown of that score (card points, announcements/belote bonuses, and whether the contract
  was made or failed), and each team's running total for the whole game, labelled distinctly
  from each other.
- **FR-015**: The pop-up MUST make clear which team is the person's team.
- **FR-016**: When the game has ended, the pop-up MUST state the winning team and provide the
  way to start a new game; otherwise dismissing it MUST continue to the next hand.
- **FR-017**: Dismissing the pop-up MUST be possible with a pointer and with the keyboard,
  and the pop-up MUST be readable on narrow and short viewports without page scrolling.
- **FR-018**: The pop-up MUST NOT reveal any information the person was not entitled to see
  during the hand beyond the cards already played in it.

### Key Entities

- **Call**: A single bidding action by one player in a bidding round - either a pass or a bid
  for a contract. Belongs to one seat and has an order within the round.
- **Seat call history**: The ordered list of calls a given seat made in the current bidding
  round, displayed in that seat's box.
- **Hand result**: The outcome of one dealt hand: the cards each team won, the score each team
  earned for it with its breakdown (card points, bonuses, contract made or failed), and the
  resulting totals for each team.
- **Game total**: The running score for each team across all hands of the current game.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On 1366×768 and 1920×1080 viewports, in 100% of game phases the person's full
  hand is visible with zero vertical scrolling.
- **SC-002**: In every hand shown, 100% of cards share one baseline and the gap between any two
  adjacent cards is identical, regardless of suit mix.
- **SC-003**: During bidding, zero cards are displayed in a disabled or illegal state; in the
  playing phase, legal vs illegal cards are distinguishable by a first-time viewer in under
  2 seconds.
- **SC-004**: Consecutive bot calls are separated by a pause of roughly 0.8 to 1.5 seconds, and
  in a usability check a first-time viewer can state each bot's call correctly at least 9 times
  out of 10 without asking for it to be repeated.
- **SC-005**: A viewer can name every player's bid(s) for the current hand from the table alone
  in under 5 seconds.
- **SC-006**: After every finished hand, a pop-up with both teams' won cards, hand scores and
  game totals appears within 1 second of the hand ending, and its card counts add up to the
  full number of cards played in that hand.
- **SC-007**: A first-time viewer can state which team won the hand and the current game
  standing from the pop-up alone, in under 10 seconds.

## Assumptions

- "Current game" in the request means the hand that just ended, and "the entire game as a
  whole" means the running total across all hands; the pop-up therefore appears after every
  hand, and shows the winner when the whole game ends.
- "All hands of both teams" means the cards each team won during the hand, shown as one
  combined row per team.
- The pop-up is dismissed by the person (no auto-dismiss), since they must be able to read it
  at their own pace.
- Showing each player's calls reuses information already available to every player at the
  table; no new hidden information is exposed.
- The pause applies to bots only; the person's own actions are never delayed.
- Existing phone-size support is preserved; the laptop-height fit is the primary new target.
- Out of scope: new game rules, changes to bot strategy, menus and room management, and any
  redesign of the look and feel beyond what is described above.
