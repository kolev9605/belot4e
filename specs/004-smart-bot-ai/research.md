# Research: Smart Bot AI

No `NEEDS CLARIFICATION` items remained in the plan's Technical Context. This records the
design decisions the spec deliberately left open, with the codebase facts they rest on.

## Codebase facts relied on

- `IPlayer.DecideAsync(SeatView, CancellationToken)` is the only seat interface; `SeatView`
  carries own hand, bidding history, standing bid/bidder, contra flags, `Contract`, current and
  completed tricks (with every card and seat), public announcements, scores and hand sizes
  (`src/Belote.Core/State/State.cs`).
- `LegalActions.For(view)` returns the exact legal action list for the seat (bids, contra,
  recontra, announcements, cards). Choosing from it makes illegal moves impossible by
  construction.
- `CardStrength.Of`, `TrickResolver.Best/Winner` and `RuleSet.CardPoints` expose contract-aware
  strength and points, so no rank values need hard-coding in the bot.
- Bulgarian rules deal 5 cards before bidding (`DealRules.BiddingAfterPacket`), so bids are made
  on a 5-card hand; the remaining 3 arrive after the contract is set.
- `RandomLegalBot` is built per seat from `Seeds.ForSeat(gameSeed, seat)`; the live table builds
  bots through `LiveTable.DefaultBot(seed)` and the log labels bot seats with the bot's type name.

## Decisions

### D1. Rule-based heuristics, not search

- **Decision**: hand evaluation + card memory + prioritised play rules. No tree search, no
  sampling of opponents' hands.
- **Rationale**: the requirement is "calculated, not LLM", one difficulty, clearly better than
  random. Heuristics are fast (SC-004), fully deterministic (SC-003), easy to unit-test per rule
  and easy to explain. Beating a uniformly random legal player by 80 % of matches is a low bar
  that sound heuristics clear comfortably; the benchmark verifies it.
- **Alternatives considered**: Perfect-information Monte Carlo (sample consistent deals, play
  out): stronger but costs per-decision CPU, needs seeded sampling and a larger test surface;
  kept as the natural basis for a future "hard" difficulty. Minimax/alpha-beta over determinised
  deals: same objections. Learned policy: out of scope (no training pipeline).

### D2. Stateless per decision, memory rebuilt from the view

- **Decision**: `SmartBot` holds no mutable game state. `CardMemory` is computed on each call
  from `view.CompletedTricks`, `view.CurrentTrick` and `view.OwnHand`.
- **Rationale**: the view already contains the full public history of the hand, so rebuilding is
  O(32) and removes a class of bugs (stale memory after a void-and-redeal, after a restarted
  table, or when one bot instance is reused). It makes "same view -> same action" trivially true
  (FR-010) and keeps the bot interchangeable with a human seat (Principle III).
- **Alternatives considered**: incremental memory updated per event: faster but order-dependent
  and fragile; the cost saving is irrelevant at this size.

### D3. Determinism with no randomness

- **Decision**: ties between equally scored actions break by a fixed canonical order (action
  type, contract rank, then suit/rank). The bot takes no seed.
- **Rationale**: FR-010 allows a seeded tie-break only "where needed"; with a canonical order
  none is needed, which avoids a stateful RNG that would break "same view, same action".
  `SmartBot` is therefore constructed without arguments (or with options), and the factory in
  `LiveTable`/`SeatPresets` ignores the per-seat seed for it.
- **Alternatives considered**: seeded tie-breaks (varied play between games): adds statefulness
  and nothing the spec asks for; the game seed already varies the deals.

### D4. Bidding model

- **Decision**: for each contract kind in `RuleSet.Bidding.Ranking`, compute a strength score
  from the own 5-card hand using the rule set's card points and strengths:
  - suit contract: points and strength of own trump cards (jack and nine weighted highest),
    trump length, side aces/tens, and a bonus for holding the king-queen pair of the trump suit;
  - no trump: aces and tens (and protected kings) scaled by the no-trump point table;
  - all trump: jacks and nines across suits, with long, strong suits favoured.
  Bid the best-scoring legal kind whose score meets `SmartBotOptions` bid threshold and that
  outranks the standing bid; otherwise pass. Contra only when the bot holds multiple top
  trumps/aces against an opposing suit contract; recontra only when its own team declared and
  the hand is still strong. Never doubles its partner. When the partner holds the standing bid,
  overcall only if the own score exceeds the threshold by a configured margin.
- **Rationale**: matches how competent humans bid from five cards; every weight is a named
  option so tuning against the benchmark is a data change, not a logic change.
- **Alternatives considered**: always bid the most common suit / bid by point count only:
  weaker and indifferent to contract family; full lookahead on the 3 unseen cards: not
  available in the view and adds nothing deterministic.

### D5. Card-play model

- **Decision**: a fixed priority order evaluated against `TrickSituation` and `CardMemory`:
  1. Declare any legal announcement first, highest points first (announcements are only legal
     before the first card of the hand, and are public after the reveal trick anyway).
  2. Forced move: a single legal card is played immediately.
  3. Following, partner currently winning and secure (partner's card is the highest unseen of its
     suit/trump, or no seat after the bot can still beat it): play the highest-point legal card
     ("feed" the trick).
  4. Following, partner winning but not secure: play the lowest-point legal card unless it is
     the last play of the trick.
  5. Following, opponent winning and bot can win: play the cheapest winning card (lowest
     strength that still wins; trump only when no plain winner exists and the trick carries
     points or the contract forces it).
  6. Following, cannot win: discard the lowest-point, lowest-strength legal card, preferring a
     suit that does not break a sequence or the king-queen pair.
  7. Leading: cash a master card (highest unseen of its suit under the contract's strength
     order); otherwise, as declarer lead trump while opponents still hold trumps and the bot
     has the jack/nine; as defender lead the longest plain suit's lowest card or the partner's
     inferred strong suit; avoid leading an unprotected ten or king.
  8. Fallback: first legal card in canonical order.
- **Rationale**: these are the standard "obvious" Belote principles and map one-to-one to spec
  requirements FR-006, FR-007, FR-008 and acceptance scenarios 4-6. Each is a small pure
  function testable with a hand-built view.
- **Alternatives considered**: a single weighted utility per card: harder to explain and test
  per scenario; a hybrid (rules for structure, utility for ties) is possible later.

### D6. Card memory and inference

- **Decision**: `CardMemory` exposes unseen cards (32 - own hand - all played cards), per-suit
  remaining counts, trump cards remaining (if a suit contract), each seat's *known voids*
  (inferred when a seat failed to follow suit or failed to trump when it could not), and
  `IsMaster(card)` (no unseen card of that suit or trump beats it under `CardStrength`).
- **Rationale**: FR-008 and the late-hand edge cases require exactly this; voids let the bot
  avoid leading into a known trump and let it recognise secure partner wins.
- **Alternatives considered**: inferring opponents' specific holdings from bids/announcements:
  more strength, more code; deferred (can extend `CardMemory` without changing the interface).

### D7. Single difficulty, extensible by options

- **Decision**: one `SmartBotOptions` record holds all thresholds and weights; the parameterless
  `SmartBot` uses `SmartBotOptions.Standard`. Future difficulties are new option presets or new
  `IPlayer` classes chosen by a factory.
- **Rationale**: satisfies FR-016 (no engine/table change to add levels) without building
  selection machinery that nothing uses yet.
- **Alternatives considered**: enum-based difficulty plumbed through table/hub/UI now: out of
  scope and speculative.

### D8. Measuring strength

- **Decision**: add `Benchmark` (library) and `belote-sim bench` (CLI). It plays N seeded games
  half with smart on North/South vs random on East/West and half the other way, using the
  existing `SimulationRunner` + `SeatFactory` (so every game is audited), and reports smart-team
  match wins, win rate and mean final-score margin. A test runs a fixed seed set and asserts the
  SC-001 threshold; CI runs the larger 1,000-game form.
- **Rationale**: reuses the audited, parallel runner; swapping seats removes seating bias
  (spec User Story 2, scenario 2).
- **Alternatives considered**: ad-hoc script outside the repo: not repeatable in CI.

### D9. Live-table integration and parity

- **Decision**: `LiveTable.DefaultBot` returns `SmartBot` for North, East, West; the log label
  becomes `nameof(SmartBot)`. The "live game equals simulation of the same seed" property now
  compares against a new `smart-table` preset (South `RandomLegalBot`, others `SmartBot`), which
  is exactly how the test client plays South.
- **Rationale**: preserves the 002 equivalence guarantee with the smallest change; the all-random
  presets (`all-bot`, `mixed-*`) and golden logs remain untouched (FR-013).
- **Alternatives considered**: keeping the random bot as the live default behind a flag: defeats
  the feature's purpose.

### D10. Blunder audit for SC-005

- **Decision**: SC-005's human review is backed by an automated audit over recorded smart-bot
  games using objective predicates for obvious blunders, asserted at zero occurrences:
  trumping a trick the partner is securely winning when a non-trump legal card exists; discarding
  a master card on a lost trick while a lower-valued legal card exists; contra-ing the partner's
  contract. A human spot check of a handful of logs remains a manual quickstart step.
- **Rationale**: turns a subjective bar into a regression test while keeping the manual check
  the spec asks for.
- **Alternatives considered**: manual review only: not repeatable.

## Risks

- **80 % target not reached on first tuning**: mitigated by the benchmark loop in the quickstart
  (tune `SmartBotOptions`, re-run); the target is not a hard algorithmic limit.
- **Variant rule sets**: heuristics assume Bulgarian-like structure; legality is guaranteed by
  construction, strength is not. Documented in the spec's assumptions and covered by the
  legality property test over modified rule sets.
