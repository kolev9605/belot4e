# Research: Headless Belote Game

Phase 0 decisions. No `NEEDS CLARIFICATION` remained in the Technical Context. Domain values were
verified against the published Bulgarian rules during implementation (see the table below); rows
still marked **Assumed** are rule-set *values* that can be corrected without code changes
(constitution Principle IV).

## Architecture decisions

### D1. Immutable state machine with an explicit action/event model
- **Decision**: `GameEngine.Apply(state, action, ruleSet)` returns either `Accepted(newState,
  events)` or `Rejected(reason)` with the original state untouched. All state types are
  immutable records.
- **Rationale**: Makes "illegal move leaves state unchanged" (FR-009) structural, enables
  replay-from-log auditing, and keeps the engine free of side effects.
- **Alternatives**: Mutable game object with methods (rejected: harder to replay/audit and to
  guarantee unchanged-on-reject); event sourcing as the only truth (rejected: heavier than
  needed; we keep events as an append-only *output* and verify by replay instead).

### D2. Own deterministic PRNG inside `Belote.Core`
- **Decision**: Implement `SeededRandom` (SplitMix64 for seeding + xoshiro256**) and a
  Fisher-Yates shuffle. Per-hand seeds are derived from the game seed and hand index;
  per-seat bot seeds are derived from the game seed and seat.
- **Rationale**: `System.Random`'s seeded algorithm is not guaranteed stable across .NET
  versions, which would break "same seed reproduces the same game" (FR-023) after a runtime
  upgrade. A tiny own PRNG is stable forever and still counts as an injected seed
  (Principle I).
- **Alternatives**: `System.Random(seed)` (rejected: stability); `RandomNumberGenerator`
  (rejected: not seedable).

### D3. Legal-move computation takes the projected view, not the full state
- **Decision**: `LegalActions.For(SeatView)` is the single source of truth; the engine builds
  the acting seat's view and uses the same function to validate the action.
- **Rationale**: Guarantees a bot/human can compute exactly the legal set from what it is
  allowed to see (Principle III), and prevents the engine and the controller from diverging.
- **Alternatives**: Separate legality code for engine and controllers (rejected: duplication
  and divergence risk).

### D4. Seat interface is asynchronous but synchronous-friendly
- **Decision**: `IPlayer.DecideAsync(SeatView, CancellationToken)` returns `ValueTask<
  PlayerAction>`. Bots complete synchronously (no allocation). The runner applies a
  configurable per-decision watchdog and records a game failure if a controller throws, times
  out, or submits illegal actions more than a configured number of times (default 3).
- **Rationale**: The same interface will serve network-backed human seats later; the
  watchdog is a *runner safety net*, not a turn timer feature (out of scope).
- **Alternatives**: Purely synchronous interface (rejected: cannot detect a controller that
  never returns, and would need a breaking change for human seats).

### D5. Rule set is a plain data object; JSON is a Simulation-layer concern
- **Decision**: `RuleSet` is an immutable record graph validated by `RuleSetValidator`.
  `RuleSets.Bulgarian` is defined in code in Core. Reading/writing rule-set JSON files is in
  `Belote.Simulation` (it performs I/O).
- **Rationale**: Keeps Core free of I/O while still letting variants be authored as files.
  The active rule set is embedded in the log header so any log is self-describing.

### D6. One omniscient game log; per-seat views are separate
- **Decision**: The engine emits ordered `GameEvent`s (with sequence numbers). The log is the
  complete, omniscient developer artifact. Controllers never receive it; they receive a
  `SeatView` containing only entitled information.
- **Rationale**: FR-024 needs reconstructability; Principle II needs hidden info protection.
  A test asserts no `SeatView` ever contains a card that seat is not entitled to see.

### D7. Correctness auditor re-derives results independently of the run
- **Decision**: `GameAuditor` replays the log from `(seed, ruleSet, seat actions)` through
  the engine and additionally checks invariants: 32-card accounting per hand, every played
  card was legal, trick winners correct, point conservation including held-over points,
  one winner at/above target, no unresolved hanging state mid-game.
- **Rationale**: FR-025 and SC-001..SC-003. The auditor and the engine share the rules but
  the auditor's accounting (card multiset, point totals) is separate code, so a scoring bug
  cannot hide by being consistently wrong in one place.

### D8. Simulation parallelism
- **Decision**: Games are independent and are run in parallel (bounded degree); results are
  collected and reported ordered by game index. Each game is single-threaded and
  deterministic, so parallelism never affects results.

## Domain decisions (Bulgarian belote defaults)

Encoded in `RuleSets.Bulgarian`. Source for every **Verified** row: the Bulgarian rules published at
[belot.bg/en/rules-belot](https://belot.bg/en/rules-belot/) (read 2026-10-12), cross-checked with the
user's clarifications of this feature. **Assumed** rows are not stated by that source and are
configuration values that can be changed without code changes; they are listed under open items.

| # | Topic | Value in `RuleSets.Bulgarian` | Status |
|---|-------|-------------------------------|--------|
| R1 | Deck / seating | 32 cards (7-A), 4 seats, partners opposite, play and bidding rotate in one direction | Verified |
| R2 | Deal and bidding point | Dealt 3 + 2, **bidding happens on 5 cards**, the last 3 are dealt when bidding closes; dealer rotates each hand | Verified (changed from the earlier "after 8 cards" assumption) |
| R3 | Bid order (low to high) | clubs < diamonds < hearts < spades < no-trump < all-trump | Assumed (the source's ranking table did not load; this is the standard order) |
| R4 | Bidding close | Three consecutive passes after a bid; immediately after a recontra | Verified |
| R5 | Contra / recontra | Contra only against an opponent's standing bid; recontra by the bidding team; after a contra no new bids; multipliers x2 and x4 | Verified |
| R6 | All-pass | Hand voided with no score; the next dealer reshuffles and redeals | Verified (and clarified) |
| R7 | Suit-contract strength | Trump: J 9 A 10 K Q 8 7; plain: A 10 K Q J 9 8 7 | Verified |
| R8 | Suit-contract points | Trump J20 9:14 A11 10:10 K4 Q3; plain A11 10:10 K4 Q3 J2; last trick +10; total 162 (152 card points + 10) | Verified |
| R9 | No-trump | Order A 10 K Q J 9 8 7; plain values doubled (22/20/8/6/4); last trick +20; total 260 | Verified |
| R10 | All-trump | Order J 9 A 10 K Q 8 7 in every suit, trump values in every suit; last trick +10; total 258 | Verified (changed from the earlier J=18 assumption) |
| R11 | Follow suit | Must follow the led suit if able | Verified |
| R12 | Must trump | Suit contract: if unable to follow and an opponent is winning, must trump; partner winning frees the card | Verified for the opponent case; partner exemption Assumed |
| R13 | Must overtrump | If a trump is already played (including trump led) must play a higher trump if able, otherwise any card | Verified (partner exemption Assumed: none for overtrumping) |
| R14 | All-trump legality | Must follow suit and must go higher if able, whoever is winning | Verified |
| R15 | Announcements | Tierce 20, quarte 50, quinte (5+) 100; four of a kind: J 200, 9 150, A/K/Q/10 100; 7s and 8s no value | Verified |
| R16 | Belote | K+Q of trump (any suit in all-trump) worth 20 to the holder's team, awarded when the second card is played; independent of announcement comparison | Verified (automatic award is an engine simplification of "announce when playing the first") |
| R17 | Announcements by contract | Allowed in suit and all-trump; none in no-trump | Verified |
| R18 | Timing | Declared with the seat's first card; revealed and compared once after the first trick | Verified for declaration; reveal point Assumed |
| R19 | Comparison | Per category: longer sequence wins, then higher top card, identical means neither counts; four of a kind by J 9 A 10 K Q; a team winning a category scores all its announcements in it; a sequence still counts if the opponents hold a four of a kind | Verified |
| R20 | Hand total | Card points + last trick + counted announcements + belote + all-tricks bonus | Verified |
| R21 | Failed contract | Declarer scores fewer points than defenders: declarer 0, defenders get all points in play x multiplier | Verified (and clarified) |
| R22 | Tied hand | Defenders keep their points, declarer's points are held over to the next hand's winner; ties stack; when the held points are rounded the declarer gets the extra game point (154-154 gives 15 and 16 held) | Verified (and clarified) |
| R23 | All eight tricks | +90 points added to the sweeping team | Verified (and clarified); no-trump value Assumed |
| R24 | Game points | Hand totals / 10; pool rounded half-up; teams get the floor of their share plus leftover points by largest remainder, ties to the team with fewer points (so 154 + 104 in all-trump is 15 + 11) | Verified against the source's rounding examples |
| R25 | Match end | First team at or above 151; both: higher total; equal: play on; a hand won by all tricks cannot end the match | Verified (and clarified) |

Additional verified rule that changed the design: under contra or recontra the team that wins more
points takes **all** points in play multiplied by the multiplier and the other team scores nothing
(`WinnerTakesAllUnderMultiplier`).

### Open items (assumed values, to confirm with a rulebook or a player)

1. Bid ranking (R3) - source table did not load.
2. Partner exemption for must-trump (R12) and for overtrumping (R13).
3. Whether announcements are compared at the first or second trick (R18) - no scoring impact.
4. All-tricks bonus in no-trump: +90 raw points after doubling is used (R23).
5. Tie under contra: the multiplier is applied to both the kept and the held points (R22).
6. Rounding ties other than the source's examples (R24): "fewer points favoured" is applied uniformly.

## Testing strategy

- **Unit tests (per rule)**: one or more tests for each row R1–R25 with a known expected
  result; naming `Rxx_Description` for traceability.
- **Property-based tests (FsCheck)**:
  - Legal move set is never empty for the seat to act in any reachable state.
  - Every legal card belongs to the seat's hand; playing any legal card is accepted.
  - Card multiset is conserved (hands + tricks + undealt = 32) at every step.
  - Hand points distributed + held-over delta = points in play.
  - `Apply` of an illegal action returns `Rejected` and the state is equal to before.
  - Same seed ⇒ same deal; same inputs ⇒ same outputs.
  - Projection of seat *s* never contains another seat's unplayed cards.
- **Golden logs**: a few fixed seeds with committed expected logs to detect unintended rule
  drift; updating a golden file requires an explicit review.
- **Integration**: all-bot games; mixed games with 1–4 scripted seats; injected illegal
  bids/cards and a controller that throws or hangs; reproducibility of any seed.
- **CI batches**: PR gate runs ≈1,000 games per configuration; scheduled job runs 10,000+
  (SC-001, SC-004) and records timing (SC-007).

## Open risks

- Rows marked **Assumed** above could differ from some house rules; they are data-only rule-set
  values, so correcting them needs no logic change (the golden logs would be regenerated).
- Performance (SC-007) measured on 2026-10-12 on the development machine: 1,000 all-bot games in
  1.6 s single-threaded and 10,000 games in 4.0 s with parallelism (budget: 5 minutes per 1,000).
