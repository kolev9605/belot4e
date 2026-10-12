# Feature Specification: Headless Belote Game

**Feature Branch**: `001-headless-belote-game`

**Created**: 2026-10-12

**Status**: Draft

**Input**: User description: "A complete game of Belote can be played from the first deal to a final winner, with four seats (two teams of two) that can each be filled by a bot. Headless: no user interface, driven by a simulation runner. Scope: dealing and full bidding (suit trump, no trump, all trump, contra/recontra); play of tricks with legal-move enforcement; announcements; hand and match scoring with edge cases; baseline random-legal-move bot; seeded simulation runner; ordered game log. Rule variant: Bulgarian belote (default rule set), differences are configuration. Out of scope: UI, networking, accounts, smart bots, turn timers. Success: robust unit and integration tests simulating bot-vs-bot and bot-vs-human games that finish according to the rules."

## Clarifications

### Session 2026-10-12

- Q: When the declaring team fails its contract in the default Bulgarian rule set, who receives the points of that hand? → A: The declaring team scores 0 and the defending team receives all points in play for the hand (card points, last-trick bonus, and both teams' counted announcements), multiplied by any contra/recontra.
- Q: When both teams score exactly the same number of points in a hand, what happens to those points in the default Bulgarian rule set? → A: The declaring team's points are held over ("hanging") and awarded to whichever team wins the next hand; the defenders keep their own points now.
- Q: When one team wins all eight tricks in a hand, what extra reward does it get in the default Bulgarian rule set? → A: A fixed bonus of 90 points (value stored in the rule set) is added to the sweeping team's hand total, on top of card points, last-trick bonus, and announcements.
- Q: What should happen when all four players pass during bidding in the default Bulgarian rule set? → A: The hand is voided with no score; the deck is reshuffled and redealt by the next dealer in rotation.
- Q: If both teams reach the target score in the same hand, who wins the match in the default Bulgarian rule set? → A: The team with the higher total score wins; if totals are exactly equal, another hand is played until the tie is broken.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Play a complete game to a winner (Priority: P1)

A developer or tester starts a game with four seats (two teams of two), each filled by a
baseline bot, and the game runs unattended from the first deal through as many hands as needed
until one team reaches the target score and is declared the winner.

**Why this priority**: A full, correct game is the core deliverable; every other capability
exists to make this work and to prove it works.

**Independent Test**: Start one game with four baseline bots and a fixed seed; verify it ends
with exactly one winning team whose score has reached the target, and that every hand along
the way followed the rules.

**Acceptance Scenarios**:

1. **Given** a new game with four seats filled by baseline bots and the default rule set,
   **When** the game is run, **Then** it deals, bids, plays all tricks, scores each hand, and
   repeats until a team reaches the target score, and then reports that team as the winner.
2. **Given** a hand in which all four players pass during bidding, **When** bidding ends,
   **Then** the hand is voided with no score, and the deck is reshuffled and redealt by the
   next dealer in rotation, and the game continues without error.
3. **Given** a game in progress, **When** any hand completes, **Then** the hand result and the
   updated match score are available and consistent with the cards won and points announced.

---

### User Story 2 - Correct bidding and contracts (Priority: P1)

Each hand is dealt and bid according to Bulgarian belote: players bid in turn to choose a
contract (a suit as trump, no trump, or all trump), higher bids supersede lower ones, and
opponents may double (contra) and the bidding team may redouble (recontra).

**Why this priority**: The contract decides the card values, the trump rules, and the scoring
multiplier; incorrect bidding invalidates everything after it.

**Independent Test**: Feed scripted bid sequences into a hand and verify which bids are
allowed, which contract and declaring team result, and what multiplier applies.

**Acceptance Scenarios**:

1. **Given** the bidding phase, **When** a player makes a bid not higher than the current
   bid, **Then** the bid is rejected as illegal and the player must act again.
2. **Given** a standing contract, **When** an opposing player doubles and later the declaring
   team redoubles, **Then** the contract multiplier reflects the double and redouble, and no
   further doubling is allowed.
3. **Given** each contract type (suit trump, no trump, all trump), **When** the hand is
   played, **Then** card strengths and point values follow that contract's table.
4. **Given** a double or redouble is not legal in the current state (e.g., by the declaring
   team's own partner, or after a redouble), **When** it is attempted, **Then** it is
   rejected.

---

### User Story 3 - Legal play of tricks (Priority: P1)

During play, only legal cards may be played: players must follow suit, must trump when unable
to follow (where required), and must overtrump when able (where required), according to the
contract and the active rule set.

**Why this priority**: Legal-move enforcement is what makes the game Belote and is what both
bots and any human controller depend on.

**Independent Test**: For a catalogue of hand and trick situations, verify the exact set of
legal cards for the player to act matches the rules for each contract.

**Acceptance Scenarios**:

1. **Given** a player holding a card of the led suit, **When** it is their turn, **Then** only
   cards of the led suit (subject to overtrump rules in a trump suit) are legal.
2. **Given** a player with no card of the led suit but with trumps and the partner not
   currently winning, **When** it is their turn, **Then** only trump cards are legal (per
   active rule set).
3. **Given** a player attempts to play an illegal card, **When** the move is submitted,
   **Then** it is rejected, the hand state is unchanged, and a legal card is still required.
4. **Given** any reachable state in a hand, **When** it is a player's turn, **Then** at least
   one legal card exists.

---

### User Story 4 - Announcements detected, compared, and scored (Priority: P2)

Players may announce combinations (sequences of 3, 4, or 5+, four of a kind, and
king-queen of trump) at the permitted time; the system detects which announcements are valid,
compares the competing teams' announcements, and awards points only to the team entitled to
them.

**Why this priority**: Announcements materially change scores and who wins; they are a
major source of rule bugs but are layered on top of basic play.

**Independent Test**: Provide hands and contracts with known combinations and verify the
detected announcements, which team's announcements count, and the points awarded.

**Acceptance Scenarios**:

1. **Given** a hand containing a valid combination, **When** the announcement window opens,
   **Then** the combination is detected and may be announced; combinations that are not
   present or not valid in the contract cannot be announced.
2. **Given** both teams hold announcements, **When** they are compared, **Then** only the team
   with the highest announcement scores its announcements, following tie-break rules in the
   active rule set.
3. **Given** a player holds king and queen of trump (in a suit contract, and all-trump where
   the rule set allows), **When** both cards are played, **Then** the declared bonus is
   awarded to that player's team.
4. **Given** an announcement is made by a team, **When** the hand is scored, **Then** its
   points are included in that team's hand total and multiplied by the contract multiplier
   where the rule set specifies.

---

### User Story 5 - Hand and match scoring with edge cases (Priority: P2)

Each hand is scored from card points, last-trick bonus, announcements, and contract
multipliers; match scores accumulate across hands until a configurable target is reached.
Edge cases are handled: failed contract, tied hand, and one team winning every trick.

**Why this priority**: Scoring determines the winner; edge cases are where implementations
most often diverge from the rules.

**Independent Test**: Provide completed hands (cards won, announcements, contract, multiplier)
and verify the hand result and updated match scores, including each edge case.

**Acceptance Scenarios**:

1. **Given** the declaring team scores fewer points than the defenders, **When** the hand is
   scored, **Then** the declaring team receives 0 and the defending team receives all points
   in play for the hand (card points, last-trick bonus, and both teams' counted
   announcements), multiplied by any contra/recontra.
2. **Given** both teams end a hand tied on points, **When** the hand is scored, **Then** the
   defending team keeps its own points, and the declaring team's points are held over and
   awarded to whichever team wins the next hand.
3. **Given** one team wins all tricks, **When** the hand is scored, **Then** a fixed
   all-tricks bonus (90 points in the default rule set) is added to that team's hand total
   on top of card points, last-trick bonus, and announcements.
4. **Given** a configured target score, **When** a team reaches or exceeds it at the end of a
   hand, **Then** the match ends and the team with the higher total wins; if both teams
   reach the target in the same hand and totals are exactly equal, further hands are played
   until the tie is broken.

---

### User Story 6 - Reproducible seeded simulations with a log (Priority: P2)

A developer runs N seeded games through a simulation runner. For each game the runner reports
whether it finished correctly (reached a winner with no rule violation or inconsistency).
Re-running a seed reproduces the identical game, and each game has an ordered log of every
event so a failure can be inspected step by step.

**Why this priority**: This is the evidence that the whole system works and the tool for
finding and diagnosing bugs.

**Independent Test**: Run the same seed twice and compare logs and results for equality; run
a batch of N seeds and verify the report lists a pass/fail outcome per game.

**Acceptance Scenarios**:

1. **Given** a seed, **When** a game is simulated twice with the same seats and rule set,
   **Then** both runs produce identical logs, hand results, and final result.
2. **Given** a batch of N seeds, **When** the runner completes, **Then** it reports each
   game's seed and outcome (finished correctly or not, with the reason), and a summary count.
3. **Given** any completed game, **When** its log is read, **Then** it lists in order every
   deal, bid, double/redouble, announcement, card played, trick result, hand score, and match
   score change, so the game can be reconstructed from the log alone.
4. **Given** a game that fails a correctness check, **When** the runner reports it, **Then**
   the seed and log are retained so the failure can be reproduced.

---

### User Story 7 - Mixed bot and non-bot seats (Priority: P3)

Seats can be filled by baseline bots or by another kind of controller (for example a scripted
"human" controller in tests) and games with a mixture still run to completion under identical
rules. The game does not behave differently depending on what controls a seat.

**Why this priority**: Confirms seats are interchangeable, which later features (human play)
depend on, but it is not needed to prove the rules themselves.

**Independent Test**: Run games with 1, 2, 3, and 4 scripted-controller seats among bots and
verify identical rule enforcement, including rejection of scripted illegal moves.

**Acceptance Scenarios**:

1. **Given** a game in which some seats are scripted controllers and others are bots,
   **When** it is run, **Then** it finishes with a winner under the same rules as an
   all-bot game.
2. **Given** a scripted controller submits an illegal bid or card, **When** it is processed,
   **Then** the move is rejected, game state is unchanged, and the controller is asked again.
3. **Given** a seat's controller acts, **When** it decides, **Then** it is shown only the
   information that seat is entitled to see (its own cards, public bids and played cards).

---

### Edge Cases

- All four players pass in bidding: the hand is voided and redealt by the next dealer, with
  the game continuing (and no infinite loop in a simulation, even if several consecutive
  hands are all-pass).
- A player holds several overlapping combinations (e.g., a sequence of 5 and a four of a
  kind): only the valid, non-conflicting set is credited per the rule set.
- Both teams announce combinations of equal rank: tie-break per the rule set.
- King-queen of trump announced but the cards are not both played: no bonus is awarded.
- Contra or recontra on a failed contract: the multiplier applies to the penalty.
- Both teams reach the target score in the same hand: the higher total wins; equal totals
  lead to further hands until the tie is broken.
- Hand ends tied with points held over to the next hand; the game ends before they are used.
- A last-trick bonus goes to the team that wins the final trick, including when that trick
  decides a failed or tied hand.
- A seat's controller errors, never responds, or repeatedly submits illegal moves: the game
  reports a failure for that game rather than hanging.
- Same seed with a different rule set or different seat controllers produces a different (but
  itself reproducible) game; this is not an error.

## Requirements *(mandatory)*

### Functional Requirements

**Game structure and dealing**

- **FR-001**: The system MUST support a game with exactly four seats forming two teams of two,
  with partners seated opposite each other and play proceeding in a fixed rotation.
- **FR-002**: The system MUST deal a standard 32-card Belote deck so that each player receives
  eight cards, with every card dealt exactly once, according to the active rule set.
- **FR-003**: The system MUST continue a game across hands, rotating the dealer and the first
  bidder between hands, until the match ends.

**Bidding**

- **FR-004**: The system MUST run a bidding phase in turn order in which each player passes or
  bids one of: a suit as trump, no trump, or all trump.
- **FR-005**: The system MUST enforce the ranking of bids and reject any bid that does not
  outrank the standing bid.
- **FR-006**: The system MUST support contra (double) by the opposing team and recontra
  (redouble) by the declaring team, with legality and ordering as defined by the active rule
  set, and MUST record the resulting multiplier.
- **FR-007**: The system MUST end bidding when the rule set's closing conditions are met and
  MUST handle an all-pass according to the rule set. In the default rule set, an all-pass
  voids the hand with no score and the next dealer in rotation reshuffles and redeals.

**Play and legal moves**

- **FR-008**: The system MUST compute, for any player to act, the exact set of legal cards,
  applying follow-suit, must-trump, and must-overtrump obligations per the contract and the
  active rule set.
- **FR-009**: The system MUST reject any illegal card without changing game state.
- **FR-010**: The system MUST resolve each trick to a winner using the card strengths of the
  contract, and the trick winner MUST lead the next trick.
- **FR-011**: The system MUST guarantee that a legal card exists for every player on turn in
  every reachable game state.

**Announcements**

- **FR-012**: The system MUST detect valid sequences (by length), four-of-a-kind
  combinations, and king-queen-of-trump bonuses from a player's hand, as defined for the
  contract by the active rule set.
- **FR-013**: The system MUST compare the competing teams' announcements and determine which
  team's announcements count, including tie-break rules, per the active rule set.
- **FR-014**: The system MUST award announcement points to the entitled team and include them
  in the hand score, applying the contract multiplier where the rule set specifies.

**Scoring**

- **FR-015**: The system MUST score each hand from card points won, last-trick bonus,
  announcements, and the contract multiplier, using each contract's point table.
- **FR-016**: The system MUST handle a failed contract, a tied hand, and a team winning all
  tricks exactly as the active rule set defines. In the default rule set, a contract fails
  when the declaring team scores fewer points than the defenders; the declaring team then
  receives 0 and the defending team receives all points in play for the hand, multiplied by
  any contra/recontra.
- **FR-016a**: In the default rule set, when both teams score equal points in a hand, the
  defending team MUST keep its own points and the declaring team's points MUST be held over
  and awarded to the team that wins the next hand. Held-over points MUST be included in the
  point-conservation check (FR-018); if the match ends with points still held over, they are
  not awarded.
- **FR-016b**: In the default rule set, a team that wins all eight tricks MUST receive a
  fixed all-tricks bonus of 90 points (configurable in the rule set) added to its hand total,
  and this bonus MUST be included in the points in play for FR-018.
- **FR-017**: The system MUST accumulate match scores across hands and end the match when the
  configurable target score is reached. When one team has reached it, that team wins; when
  both reach it in the same hand, the team with the higher total wins, and exactly equal
  totals lead to further hands until the tie is broken. A hand in which one team wins all eight
  tricks cannot end the match.
- **FR-018**: The system MUST conserve points: for every hand, the points distributed to the
  teams MUST equal the points the rule set says are in play for that contract, announcements,
  and multiplier.

**Seats, bots, and information**

- **FR-019**: The system MUST allow each of the four seats to be filled independently by a
  baseline bot or by another controller, and MUST apply identical rules regardless of which.
- **FR-020**: The system MUST provide the baseline bot, which at every decision chooses
  uniformly at random from the legal options (bids, announcements, and cards), using only the
  information its seat is entitled to see.
- **FR-021**: The system MUST show each seat's controller only that seat's own cards plus
  publicly visible information (bids, played cards, announcements revealed so far, scores).

**Simulation, determinism, and logging**

- **FR-022**: The system MUST provide a simulation runner that plays N games, each driven by a
  seed, and reports per game the seed, whether it finished correctly, the winner and final
  score, and the reason for any failure.
- **FR-023**: The system MUST be deterministic: the same seed, rule set, and seat controllers
  MUST reproduce the same deal, the same decisions, the same log, and the same result.
- **FR-024**: The system MUST produce an ordered log of every event in a game (deals, bids,
  doubles, announcements, cards played, trick results, hand scores, match scores, and the
  final result) sufficient to reconstruct the game.
- **FR-025**: The system MUST verify correctness of every simulated game, including: all
  moves were legal, all 32 cards were accounted for in every hand, scores were conserved,
  and the game ended with one winner at or above the target score.

**Rule configuration**

- **FR-026**: The system MUST express all rule differences (bid set and ranking, doubling
  options, trump rules, point tables, announcement values and conditions, scoring and
  tie/failure handling, target score) as a rule-set configuration; no rule value may be
  fixed in game logic.
- **FR-027**: The system MUST ship Bulgarian belote as the default rule set.

### Key Entities

- **Game / Match**: A sequence of hands between two teams until a target score is reached;
  holds the rule set, seed, seats, hands played, running scores, and winner.
- **Team**: Two partnered seats; accumulates match score.
- **Seat**: One of four positions; controlled by a bot or another controller; has a private
  hand and a restricted view of the game.
- **Rule Set**: The configuration of all variant rules (contracts, doubling, legality
  obligations, point tables, announcements, scoring, target score).
- **Card / Deck**: A suit-and-rank card from a 32-card deck, with contract-dependent
  strength and point value.
- **Hand (Deal)**: One round from dealing through scoring, with a dealer, bidding record,
  contract, announcements, tricks, and result.
- **Contract**: The winning bid: trump type (suit, no trump, all trump), declaring team,
  and multiplier (normal, doubled, redoubled).
- **Announcement**: A declared combination (sequence, four of a kind, king-queen of trump)
  with its owner, rank, and point value.
- **Trick**: Four cards, one per seat, with a lead suit and a winner.
- **Game Log**: Ordered record of every event in a game.
- **Simulation Report**: Per-game outcomes (seed, result, correctness, failure reason) and a
  batch summary.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A batch of at least 10,000 seeded all-bot games runs unattended, and 100% of
  them finish with exactly one winner at or above the target score and with no rule
  violation.
- **SC-002**: Running any seed twice produces identical logs and results in 100% of 1,000
  sampled seeds.
- **SC-003**: In 100% of simulated games, every hand accounts for all 32 cards and the points
  awarded exactly equal the points in play.
- **SC-004**: Mixed games with one, two, three, and four non-bot seats (alongside bots) each
  pass a batch of at least 1,000 seeded games with the same 100% correctness as all-bot
  games, including rejection of injected illegal moves.
- **SC-005**: Every rule listed in the scope (contracts, doubling, follow-suit, must-trump,
  must-overtrump, each announcement type, announcement comparison, failed contract, tied
  hand, all tricks won, target-score ending) is covered by at least one automated test with
  a known expected result.
- **SC-006**: When a game fails a correctness check, a developer can reproduce it from its
  seed and identify the first offending event from its log in a single re-run.
- **SC-007**: A batch of 1,000 all-bot games completes in under 5 minutes on a typical
  developer machine.

## Assumptions

- Bulgarian belote is played with a 32-card deck (7 through Ace) by two teams of two,
  partners opposite, to a target score that defaults to 151 and is configurable.
- Contract types are the four suits as trump, no trump, and all trump, with contra and
  recontra. The default rule set follows the published Bulgarian rules (verified; see
  research.md): bidding happens after five cards are dealt, a suit contract has 152 card points
  plus a 10-point last-trick bonus (162 in play), no trump has 260 and all trump 258 in play,
  game points are hand points divided by 10, and tierce, quarte, quinte, four of a kind and
  belote are worth 20, 50, 100, 100-200 and 20 points.
- Under contra or recontra the team that wins more points takes all points in play multiplied
  by the multiplier; sequences and four-of-a-kinds are compared within their own category, and
  identical sequences cancel out.
- A hand won by one team taking all eight tricks cannot end the match; play continues until a
  match is won by an ordinary hand.
- Where the description says "per the active rule set" the default rule set's value is used
  for tests; alternative rule sets (e.g., a French variant) are out of scope for this
  feature, but the configuration mechanism MUST make adding one possible without altering
  game logic.
- "Human" seats in this headless feature are represented by a scripted or programmatic
  controller used by tests; real interactive humans arrive with later features.
- "Finished correctly" means the game reached a legal end with a single winner and passed
  all correctness checks in FR-025; it does not measure play quality.
- Randomness in a game comes solely from the seed (deal and baseline bot choices), so a seed
  plus the rule set and seat controllers fully determines a game.
- No user interface, networking, accounts, smart bots, or turn timers are included.
