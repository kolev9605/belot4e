# Feature Specification: Smart Bot AI

**Feature Branch**: `004-smart-bot-ai`

**Created**: 2026-10-13

**Status**: Draft

**Input**: User description: "implement non-random moves for the bots - there needs to be some sort of an AI (not LLM, just calculated moves). For now implement just one difficulty."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Bots play purposeful, rule-abiding Belote (Priority: P1)

A player sits at a table against three bots. Instead of choosing bids and cards at random,
each bot evaluates its own hand and everything that is publicly visible, and chooses the move
that a reasonable human player would consider sensible: it bids when its hand is strong and
passes when it is weak, it tries to win tricks that are worth winning, it avoids throwing
away high-value cards for nothing, and it supports its partner. Every move is still legal
under the rules, and the bot never uses information it is not entitled to see.

**Why this priority**: This is the whole point of the feature. Everything else (measuring,
selecting) only makes sense once bots make calculated moves.

**Independent Test**: Play a seeded batch of complete games with the smart bot in every seat
and verify that all games finish with only legal moves. Then play the same seeds with a
smart-bot team against a random-legal-bot team and verify the smart team wins clearly more
often (see Success Criteria).

**Acceptance Scenarios**:

1. **Given** a bot is asked to act, **When** it decides, **Then** the move is one of the
   legal actions for its seat in the current view, every time, across all rule-set
   configurations the game supports.
2. **Given** a bot's hand is clearly too weak to support any contract, **When** it is its
   turn to bid, **Then** it passes.
3. **Given** a bot holds a hand strong enough to support a contract and a bid is legal and
   higher than the standing bid, **When** it is its turn to bid, **Then** it bids for the
   trump type its hand favours most.
4. **Given** a trick in which the bot's partner is currently winning and the bot cannot
   change that, **When** the bot must play, **Then** it contributes valuable cards to the
   trick rather than its lowest ones, where the rules allow.
5. **Given** a trick that the bot's opponents are currently winning and the bot can win it,
   **When** the bot must play, **Then** it wins the trick using the cheapest card that does
   so, rather than an unnecessarily strong one.
6. **Given** the bot cannot win a trick, **When** it must play, **Then** it discards its
   least valuable legal card.
7. **Given** a bot holds a declarable announcement or the belote pair when the rules allow
   declaring it, **When** the opportunity arises, **Then** the bot declares it.

---

### User Story 2 - Bots are measurably stronger than the random baseline (Priority: P1)

A maintainer wants evidence that the new bot is genuinely better than the baseline, not just
different. A benchmark plays a large, seeded set of games in which one team is made of smart
bots and the other of random-legal bots, with seats swapped so neither team benefits from
seating or deal luck, and reports win rate and average score margin.

**Why this priority**: Without a measurable bar, "AI" cannot be told apart from noise; the
benchmark also guards against future regressions.

**Independent Test**: Run the benchmark with a fixed set of seeds; the result is identical on
every run and meets the win-rate target in Success Criteria.

**Acceptance Scenarios**:

1. **Given** a fixed set of seeds, **When** smart bots as one team play random-legal bots as
   the other team over the full set, **Then** the smart team wins at least the target share of
   matches.
2. **Given** the same seeds and seats swapped between teams, **When** the benchmark runs
   again, **Then** the smart team still meets the target, showing the result is not an
   artefact of seating.
3. **Given** the same seeds run twice, **When** results are compared, **Then** they are
   identical move for move.

---

### User Story 3 - The live table uses the smart bots by default (Priority: P2)

When a person plays a live table, the three bot seats are filled by the smart bot rather than
the random baseline, without the player doing anything. The existing table behaviour (pacing
delay, views, validation, game log, partner on North) is unchanged; only the quality of the
bots' choices changes.

**Why this priority**: This is where players actually feel the improvement, but it depends on
the bot existing and being verified first.

**Independent Test**: Create a live table and play a game; confirm the bot seats are driven by
the smart bot and that every bot move appears in the game record as an ordinary legal move
for its seat.

**Acceptance Scenarios**:

1. **Given** a newly created live table, **When** the bot seats are filled, **Then** they are
   filled with the smart bot.
2. **Given** a live game, **When** a bot acts, **Then** it acts from its own seat's view only
   and the move is validated by the same checks as a human seat's.
3. **Given** a live game with a fixed seed and the same bots in the same seats as a headless
   simulation of that seed, **When** both are played to completion, **Then** they produce the
   same moves, log and final scores.

---

### User Story 4 - The random bot remains available as a baseline (Priority: P3)

The random-legal bot is kept so that it can still be seated anywhere the smart bot can,
serving as the comparison baseline for the benchmark and for existing simulations and tests.

**Why this priority**: It is needed for measurement and regression tests, but requires no new
behaviour.

**Independent Test**: Seat a mix of random and smart bots in a headless simulation and
confirm games complete correctly.

**Acceptance Scenarios**:

1. **Given** a simulation with any mix of smart and random bots across the four seats,
   **When** games are played, **Then** every game completes legally.
2. **Given** existing seeded simulations that use only random bots, **When** they are re-run
   after this feature, **Then** their results are unchanged.

---

### Edge Cases

- Only one legal action exists (for example a forced follow-suit card, or only "pass" is
  possible): the bot takes it without further evaluation.
- The bot is last to bid after three passes with a hand that would normally not bid: it
  follows the rules for that situation and never makes an illegal bid.
- The bot's partner has made a bid: the bot takes it into account (it does not outbid its
  own partner without good reason) and never contras its own partner.
- Contra and recontra are available: the bot only doubles when its own hand and the
  bidding strongly suggest the opposing contract will fail, and otherwise declines.
- Late in a hand, when few cards remain unseen: the bot uses what has been played to deduce
  which strong cards are still out, and does not assume cards that have already been played.
- Every high card of a suit has already been played: the bot treats the remaining cards of
  that suit as ordinary and plays accordingly.
- Different rule sets (contract types, announcement values, scoring options): the bot still
  produces legal moves; its strength heuristics are tuned for the default Bulgarian rule set
  and need only remain legal elsewhere.
- A hand is voided after four passes and redealt: the bot carries no state from the voided
  hand into the redeal.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a bot that chooses every bid, contra/recontra, card
  play and announcement by evaluating its seat's view, rather than by random choice.
- **FR-002**: The bot MUST choose only actions that are legal for its seat in the current
  view; it MUST NEVER produce an illegal action in any game, for any rule set or seed.
- **FR-003**: The bot MUST use only information available in its own seat's view: its own
  hand, public cards played so far, bids, announcements that have become public, scores and
  turn order. It MUST NOT be given, and MUST NOT depend on, any other seat's hidden cards,
  the undealt deck or the game seed.
- **FR-004**: During bidding, the bot MUST estimate the strength of its hand per available
  trump type, bid for the most favourable type when the estimate meets its bidding
  threshold and the bid is legal and higher than the standing bid, and pass otherwise.
- **FR-005**: During bidding, the bot MUST take the bids of its partner and opponents into
  account, and MUST NOT contra or recontra without a strong basis in its hand and the
  bidding.
- **FR-006**: During play, the bot MUST try to win tricks whose points or position make
  them worth winning, using the lowest card that wins when it can, and MUST otherwise
  discard its least valuable legal card.
- **FR-007**: When its partner is currently winning a trick (and is reasonably certain to
  keep winning it), the bot MUST prefer to add valuable cards to that trick instead of
  low ones, where the rules allow.
- **FR-008**: The bot MUST track which cards have been played in the current hand and use
  that to judge which cards are still outstanding, including whether a card is now the
  highest remaining of its suit.
- **FR-009**: The bot MUST declare announcements and the belote pair whenever the rules
  allow and doing so is not disadvantageous under the current rule set.
- **FR-010**: The bot MUST be deterministic: for a given seat view and the bot's own
  configuration, it MUST always choose the same action. Where it needs a tie-break, it MUST
  derive it from a seed provided at construction so that complete games remain reproducible
  from the game seed alone.
- **FR-011**: The bot MUST be selectable per seat wherever a player controller can be
  chosen — in headless simulations and in the live table — through the same player
  interface as the random bot and human seats, with no special-casing of the bot in the
  rules engine or the table.
- **FR-012**: The live table MUST seat the smart bot in the three bot seats by default.
- **FR-013**: The random-legal bot MUST remain available and unchanged, and results of
  existing seeded simulations that use only the random bot MUST be unchanged.
- **FR-014**: The system MUST provide a repeatable benchmark that plays a given set of
  seeds with the smart bot against the random bot, with seats swapped between teams, and
  reports each team's match wins and average score margin.
- **FR-015**: A single bot decision MUST be imperceptible to a player, and a hand with zero
  deliberate delay MUST finish in about the same time as before.
- **FR-016**: The system MUST offer exactly one bot difficulty in this feature; the way
  bots are chosen MUST NOT preclude adding further difficulties later without changing the
  rules engine or table.

### Key Entities

- **Smart bot**: A seat controller that, given its seat view, returns a single legal action
  chosen by evaluating hand strength, the state of the bidding or trick, partner and
  opponent positions, and the cards already played. Carries no hidden information.
- **Hand evaluation**: The bot's internal judgement of how strong its hand is for each
  possible trump type, used to decide whether and what to bid.
- **Card memory**: The bot's record, rebuilt from public information, of which cards have
  been played in the current hand and which remain unseen.
- **Benchmark result**: A summary of a seeded batch of games between two controller types:
  matches won per team, average score margin, and the seeds used.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Across at least 1,000 seeded games, with seats swapped so each team plays
  half of them from each side, a team of smart bots wins at least 80% of matches against a
  team of random-legal bots.
- **SC-002**: In 100% of games across all benchmarked and simulated seeds, every action the
  smart bot produces is legal and every game completes to a valid winner.
- **SC-003**: Re-running any seeded game with the same bots in the same seats reproduces
  the identical move sequence and final score, every time.
- **SC-004**: A player at a live table sees no change in responsiveness: with the pacing
  delay set to zero, playing a hand against smart bots takes no more than twice as long as
  playing a hand against random bots. (Measured per hand, not per game: smart play makes games
  last more hands because opponents no longer end games early through random doubling.)
- **SC-005**: In a review of a sample of recorded games, a reviewer familiar with Belote
  finds no bot move they would consider an obvious blunder (such as trumping a trick their
  partner is already winning, or leading away a master card for no purpose) in at least
  90% of sampled hands.
- **SC-006**: All existing automated tests for the rules engine, simulation, table and
  user interface continue to pass, apart from deliberate updates to which bot is seated by
  default at a live table.

## Assumptions

- The smart bot is a rule-based, calculated decision-maker (hand evaluation, card counting
  and heuristics); no machine-learning model, language model or external service is used.
- Its heuristics are tuned for the default Bulgarian rule set; other rule sets need only
  keep it legal.
- It is allowed to be beatable and imperfect: the goal is a clear, noticeable improvement
  over random play, not an optimal or unbeatable opponent.
- A single difficulty is delivered now. Selecting a difficulty in the user interface is out
  of scope; the live table simply always uses the smart bot.
- North (the player's partner), East and West are all the same smart bot; no distinct
  personalities per seat are required.
- The bot reasons only within the current game from what is visible; it does not learn
  opponents' habits across games.
- The existing live-table pacing delay and game record are reused unchanged.
- The random-legal bot is retained as the comparison baseline and for existing tests.
- Out of scope: additional difficulty levels, learning or self-play training, bot
  personalities or chat, user-selectable bot strength, explanations of why a bot moved,
  and any change to the rules engine or scoring.
