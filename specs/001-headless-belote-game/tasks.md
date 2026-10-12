---

description: "Task list for the Headless Belote Game feature"
---

# Tasks: Headless Belote Game

**Input**: Design documents from `/specs/001-headless-belote-game/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (all present)

**Tests**: INCLUDED. The spec's success criteria (SC-001..SC-005) and constitution Principle V
require unit tests per rule, property-based tests, and seeded simulations. Within each story,
write the tests first and confirm they fail before implementing.

**Organization**: Tasks are grouped by user story. Priorities P1 stories (US1, US2, US3) all
sit on the critical path; US1 is the integrating story, so US2 and US3 are built first (see
Dependencies).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story the task belongs to (US1..US7)
- File paths are relative to the repository root (`src/`, `tests/` per plan.md)
- `Rxx` refers to the rule rows in [research.md](research.md); name tests `Rxx_Description`
  so rule coverage is traceable

## Path Conventions

Single solution `Belote.sln`; source in `src/Belote.Core`, `src/Belote.Bots`,
`src/Belote.Simulation`, `src/Belote.Simulation.Cli`; tests in `tests/Belote.Core.Tests` and
`tests/Belote.Simulation.Tests`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution and project skeleton

- [X] T001 Create `Belote.sln`, `Directory.Build.props` (net10.0, `Nullable` enabled, `ImplicitUsings` disabled, `TreatWarningsAsErrors` true, `Deterministic` true), `.editorconfig`, and `.gitignore` at the repository root
- [X] T002 [P] Create `src/Belote.Core/Belote.Core.csproj` with **no** package or project references
- [X] T003 [P] Create `src/Belote.Bots/Belote.Bots.csproj` referencing only `Belote.Core`
- [X] T004 [P] Create `src/Belote.Simulation/Belote.Simulation.csproj` referencing `Belote.Core` and `Belote.Bots`
- [X] T005 [P] Create `src/Belote.Simulation.Cli/Belote.Simulation.Cli.csproj` (console, assembly name `belote-sim`) referencing `Belote.Simulation`
- [X] T006 [P] Create `tests/Belote.Core.Tests/Belote.Core.Tests.csproj` (xUnit, FsCheck.Xunit) referencing `Belote.Core`
- [X] T007 [P] Create `tests/Belote.Simulation.Tests/Belote.Simulation.Tests.csproj` (xUnit, FsCheck.Xunit) referencing `Belote.Core`, `Belote.Bots`, `Belote.Simulation`
- [X] T008 Add all six projects to `Belote.sln` and confirm `dotnet build Belote.sln -c Release` succeeds

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cards, deterministic randomness, rule-set object, core state/event/view types,
and the seat interface. No user story can start before this phase completes.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests first

- [X] T009 [P] Write tests for card/deck/seat basics in `tests/Belote.Core.Tests/Unit/CardsTests.cs`: 32 unique cards; card notation round-trip (`JH`, `10S`, `7C`); `Partner(seat)` is the opposite seat; `Team(seat)` pairs North/South and East/West; clockwise order
- [X] T010 [P] Write tests for `SeededRandom` in `tests/Belote.Core.Tests/Unit/SeededRandomTests.cs`: fixed known-answer vectors for seeds 0, 1, 42 (literal expected outputs, so algorithm drift fails); `NextInt(bound)` stays in range and is unbiased over many draws; `Derive(salt)` streams differ; shuffle is a permutation of the input; same seed gives same shuffle
- [X] T011 [P] Write tests for rule-set validation in `tests/Belote.Core.Tests/Unit/RuleSetValidatorTests.cs`: Bulgarian rule set is valid; card order not a permutation of the 8 ranks is reported; duplicate bid in ranking reported; deal pattern not summing to 8 reported; non-positive target score reported; points-in-play for suit contract equals 162 (R8)
- [X] T012 [P] Write the architecture/purity test in `tests/Belote.Core.Tests/Unit/CorePurityTests.cs`: `Belote.Core` references only the .NET base class library (no package/project references); source scan of `src/Belote.Core/**/*.cs` fails on `DateTime.Now`, `DateTime.UtcNow`, `Stopwatch`, `System.Random`, `Guid.NewGuid`, `System.IO`, `Console.`, `Environment.` (Principle I)
- [X] T013 [P] Write the shared test-support helper `tests/Belote.Core.Tests/Support/HandBuilder.cs`: a helper to build `GameState`/`HandState` with chosen hands, dealer, contract, and trick history for use by all later tests

### Implementation

- [X] T014 [P] Implement `Suit`, `Rank`, `Card`, `Seat`, `Team` and notation parse/format in `src/Belote.Core/Cards/` (`Suit.cs`, `Rank.cs`, `Card.cs`, `Seat.cs`, `Team.cs`)
- [X] T015 [P] Implement `Deck.Standard32` in `src/Belote.Core/Cards/Deck.cs`
- [X] T016 [P] Implement `SeededRandom` (SplitMix64 seeding + xoshiro256**), unbiased `NextInt`, `Derive`, and Fisher-Yates shuffle in `src/Belote.Core/Random/SeededRandom.cs`; implement `Seeds.ForGame/ForHand/ForSeat` in `src/Belote.Core/Random/Seeds.cs`
- [X] T017 Implement the `RuleSet` record graph (`DealRules`, `BiddingRules`, per-contract `ContractRules`, `PlayRules`, `AnnouncementRules`, `ScoringRules`, `MatchRules`) in `src/Belote.Core/Rules/RuleSet.cs` matching [contracts/rule-set.md](contracts/rule-set.md), including a computed `PointsInPlay(ContractKind)`
- [X] T018 Implement `RuleSets.Bulgarian` with the working defaults in `src/Belote.Core/Rules/RuleSets.cs` (values from contracts/rule-set.md; no rule value may appear anywhere else in Core)
- [X] T019 Implement `RuleSetValidator` and `RuleSetIssue` in `src/Belote.Core/Rules/RuleSetValidator.cs`
- [X] T020 [P] Implement `PlayerAction` (`Pass`, `Bid`, `Contra`, `Recontra`, `Announce`, `PlayCard`) in `src/Belote.Core/Players/PlayerAction.cs` and `IPlayer` (`DecideAsync(SeatView, CancellationToken) -> ValueTask<PlayerAction>`) in `src/Belote.Core/Players/IPlayer.cs`
- [X] T021 [P] Implement `GameEvent` hierarchy with `Sequence`, `HandNumber`, and the event types listed in data-model.md in `src/Belote.Core/Events/GameEvent.cs`
- [X] T022 [P] Implement immutable state records `GameState`, `HandState`, `BiddingState`, `Contract`, `ContractKind`, `Trick`, `Phase` in `src/Belote.Core/State/` per data-model.md (state invariants documented in XML comments)
- [X] T023 [P] Implement `ApplyResult` (`Accepted`/`Rejected`), `RejectionReason` codes from contracts/core-api.md, and `GameStart` in `src/Belote.Core/Engine/ApplyResult.cs`
- [X] T024 Implement `SeatView` and `Projection.For(GameState, Seat)` skeleton (own hand, phase, dealer, `ToAct`, match score, rule set reference only) in `src/Belote.Core/State/SeatView.cs` and `src/Belote.Core/State/Projection.cs`

**Checkpoint**: Foundation ready; `dotnet test` passes for T009–T013 and the project builds.

---

## Phase 3: User Story 2 - Correct bidding and contracts (Priority: P1)

**Goal**: A hand is dealt from a seed and bid to a contract (suit, no trump, all trump) with
ranking, contra, recontra, and all-pass handling.

**Independent Test**: Feed scripted bid sequences into a freshly dealt hand and verify the
legal actions, the resulting contract/declarer/multiplier, and the all-pass redeal.

### Tests for User Story 2 ⚠️ (write first, confirm failing)

- [X] T025 [P] [US2] Dealing tests in `tests/Belote.Core.Tests/Unit/DealingTests.cs`: R1/R2 each seat gets 8 cards, all 32 dealt once, deal follows the 3-2-3 pattern, same hand seed gives same deal, different seeds differ, dealer rotates between hands
- [X] T026 [P] [US2] Bidding tests in `tests/Belote.Core.Tests/Unit/BiddingTests.cs`: R3 bid ranking (each rank below standing bid is rejected `BidNotHigher`), pass always legal, R4 closes after three consecutive passes following a bid, closes immediately after recontra, R5 contra only by an opponent of the standing bidder, recontra only by the bidding team after contra, no action after recontra, multipliers 1/2/4, contract kind/declarer recorded, R6 all-pass voids the hand with no score and next dealer redeals
- [X] T027 [P] [US2] Property tests in `tests/Belote.Core.Tests/Properties/BiddingProperties.cs`: any sequence of legal bidding actions terminates in at most a bounded number of steps; legal bidding actions are never empty for the seat to act; bidding never changes any seat's hand

### Implementation for User Story 2

- [X] T028 [P] [US2] Implement hand dealing from `Seeds.ForHand` and the rule set's deal pattern in `src/Belote.Core/Engine/HandDealer.cs`
- [X] T029 [P] [US2] Implement the bidding state machine (ranking, contra/recontra, close conditions, all-pass policy) in `src/Belote.Core/Bidding/BiddingEngine.cs`
- [X] T030 [US2] Implement `GameEngine.Start` (validate rule set, deal first hand, emit `GameStarted`/`HandDealt`) in `src/Belote.Core/Engine/GameEngine.cs`
- [X] T031 [US2] Implement bidding-phase handling in `GameEngine.Apply`: turn/phase checks, rejections with reasons, events `BidMade`/`PassMade`/`ContraMade`/`RecontraMade`/`BiddingClosed`/`HandVoided`, redeal by next dealer, transition to `Playing` in `src/Belote.Core/Engine/GameEngine.cs`
- [X] T032 [P] [US2] Implement `LegalActions.For(SeatView)` bidding branch in `src/Belote.Core/Players/LegalActions.cs`; extend `Projection` with public bidding history and contract in `src/Belote.Core/State/Projection.cs`
- [X] T033 [US2] Add the per-contract card-strength and point tables lookup (`ContractTables`) used by later phases in `src/Belote.Core/Rules/ContractTables.cs`, with tests for R7–R10 values in `tests/Belote.Core.Tests/Unit/ContractTablesTests.cs` *(Done differently: contract tables are `RuleSet.PointsInPlay/CardPoints/LastTrickBonus` in `RuleSet.cs`; R7–R10 are tested in `RulesAndPurityTests.cs`.)*

**Checkpoint**: A hand can be dealt and bid to a contract (or voided) under the engine.

---

## Phase 4: User Story 3 - Legal play of tricks (Priority: P1)

**Goal**: Play proceeds trick by trick with exact legal-card computation (follow suit, must
trump, must overtrump) and correct trick resolution.

**Independent Test**: For a catalogue of hand/trick situations per contract, the legal-card
set equals the expected set; illegal cards are rejected without state change.

### Tests for User Story 3 ⚠️ (write first, confirm failing)

- [X] T034 [P] [US3] Legal-card tests in `tests/Belote.Core.Tests/Unit/LegalCardsTests.cs`: R11 follow suit; R12 must trump when unable to follow and partner not winning; partner winning exempts must-trump; R13 must overtrump a winning trump, else any trump; led trump requires higher trump if able; R14 all-trump must follow and go higher if able; no-trump requires only following suit; leader may play any card
- [X] T035 [P] [US3] Trick resolution tests in `tests/Belote.Core.Tests/Unit/TrickResolutionTests.cs`: winner by contract strength order (suit trump beats led suit; no-trump order; all-trump order per led suit); off-suit non-trump never wins; winner leads the next trick; eighth trick ends play
- [X] T036 [P] [US3] Rejection tests in `tests/Belote.Core.Tests/Unit/PlayRejectionTests.cs`: card not in hand, illegal card, out-of-turn play each return `Rejected` with the right reason and a state equal to the input state; after rejection a legal card is still required
- [X] T037 [P] [US3] Property tests in `tests/Belote.Core.Tests/Properties/PlayProperties.cs`: FR-011 a legal card exists for the seat to act in every reachable play state (random legal play, all contract kinds); every legal card is in the seat's hand; every legal card is accepted; cards in hands + tricks + current trick always total 32

### Implementation for User Story 3

- [X] T038 [P] [US3] Implement card strength comparison by contract in `src/Belote.Core/Play/CardStrength.cs`
- [X] T039 [P] [US3] Implement `LegalCards.For(SeatView)` (follow-suit / must-trump / must-overtrump per `PlayRules`) in `src/Belote.Core/Play/LegalCards.cs`
- [X] T040 [P] [US3] Implement `TrickResolver` (winner determination, next leader) in `src/Belote.Core/Play/TrickResolver.cs`
- [X] T041 [US3] Extend `LegalActions.For(SeatView)` with the play branch and extend `Projection` with current trick and completed tricks (public cards only) in `src/Belote.Core/Players/LegalActions.cs` and `src/Belote.Core/State/Projection.cs`
- [X] T042 [US3] Implement play-phase handling in `GameEngine.Apply`: validate against `LegalActions` from the acting seat's view, emit `CardPlayed`/`TrickWon`, advance leader, track cards won per team and last-trick team, detect end of hand in `src/Belote.Core/Engine/GameEngine.cs`

**Checkpoint**: A hand can be played to completion with only legal moves accepted.

---

## Phase 5: User Story 1 - Play a complete game to a winner (Priority: P1) 🎯 MVP

**Goal**: A game of four baseline bots runs unattended from the first deal to a winner,
scoring each hand and accumulating the match score to the target.

**Independent Test**: Run one seeded all-bot game; it ends with exactly one winner at or above
the target score and every hand followed the rules.

### Tests for User Story 1 ⚠️ (write first, confirm failing)

- [X] T043 [P] [US1] Hand scoring tests in `tests/Belote.Core.Tests/Unit/HandScoringTests.cs`: R8 card points per contract sum to the rule set's points in play (162 suit; no-trump and all-trump totals from the rule set); R20 last-trick bonus goes to the last trick's winner; multiplier applied to the hand total; made vs failed outcome (declarer fewer points than defenders) per R21; game-point conversion table-driven test for R24 rounding
- [X] T044 [P] [US1] Match scoring tests in `tests/Belote.Core.Tests/Unit/MatchScoringTests.cs`: scores accumulate per team across hands; match ends when a team reaches `TargetScore`; `Winner` set only at `Finished`; no actions accepted after `Finished` (`GameFinished`)
- [X] T045 [P] [US1] Bot tests in `tests/Belote.Simulation.Tests/Unit/RandomLegalBotTests.cs`: bot returns only actions from `LegalActions.For(view)`; same bot seed and same views give same choices; different seeds explore different choices; bot compiles against `SeatView` only (no `GameState` access)
- [X] T046 [US1] Full-game integration tests in `tests/Belote.Simulation.Tests/Integration/AllBotGameTests.cs`: 100 seeded all-bot games each finish with one winner at or above target; game with all-pass hands continues; hand count bounded; per-hand 32-card and point-conservation spot checks

### Implementation for User Story 1

- [X] T047 [P] [US1] Implement `HandScorer` (card points, last-trick bonus, multiplier, made/failed outcome, `HandResult`) in `src/Belote.Core/Scoring/HandScorer.cs`
- [X] T048 [P] [US1] Implement `GamePointConverter` (divisor + rounding mode from rule set) in `src/Belote.Core/Scoring/GamePointConverter.cs`
- [X] T049 [US1] Implement `MatchScorer` (accumulate, target check, winner) in `src/Belote.Core/Scoring/MatchScorer.cs`
- [X] T050 [US1] Wire hand end into `GameEngine.Apply`: score hand, emit `HandScored`/`MatchScoreUpdated`, deal next hand with next dealer or finish with `GameFinished`, in `src/Belote.Core/Engine/GameEngine.cs`
- [X] T051 [P] [US1] Implement `RandomLegalBot` (uniform choice from `LegalActions.For(view)` using `SeededRandom` from `Seeds.ForSeat`) in `src/Belote.Bots/RandomLegalBot.cs`
- [X] T052 [US1] Implement `GameRunner` (single game: start, loop asking each seat's `IPlayer` for its action, apply, collect events, finish) in `src/Belote.Simulation/GameRunner.cs`
- [X] T053 [US1] Add a bounded-hands safeguard (configurable max hands, failure kind `ExceededHandLimit`) to `GameRunner` in `src/Belote.Simulation/GameRunner.cs`

**Checkpoint**: MVP. An all-bot game runs to a winner; US1 acceptance scenarios 1–3 pass.

---

## Phase 6: User Story 4 - Announcements detected, compared, and scored (Priority: P2)

**Goal**: Sequences, four-of-a-kind, and belote are detected, declared, compared between
teams, and scored.

**Independent Test**: Hands with known combinations produce the expected detected,
counted, and scored announcements.

### Tests for User Story 4 ⚠️ (write first, confirm failing)

- [X] T054 [P] [US4] Detection tests in `tests/Belote.Core.Tests/Unit/AnnouncementDetectionTests.cs`: R15 tierce 20, quarte 50, quinte (5+) 100; carré J 200, 9 150, A/K/Q/10 100; carré of 7s or 8s not an announcement; overlapping combinations (sequence plus carré) detect the valid set; R17 none allowed in no-trump
- [X] T055 [P] [US4] Belote tests in `tests/Belote.Core.Tests/Unit/BeloteBonusTests.cs`: R16 K+Q of trump in a suit contract; per suit in all-trump; awarded only when both cards are played; not awarded in no-trump
- [X] T056 [P] [US4] Comparison tests in `tests/Belote.Core.Tests/Unit/AnnouncementComparisonTests.cs`: R19 carré beats sequences, longer sequence beats shorter, higher top card wins, equal rank resolved by trump suit then first-bidder proximity; only the winning team's announcements count; belote counts regardless of comparison
- [X] T057 [P] [US4] Timing and scoring tests in `tests/Belote.Core.Tests/Unit/AnnouncementFlowTests.cs`: R18 announcement legal only with the seat's first card; declaring an announcement not in hand is rejected; announcements revealed after trick 1; counted announcements added to hand total and multiplied; `SeatView` exposes declared announcements publicly but not undeclared combinations

### Implementation for User Story 4

- [X] T058 [P] [US4] Implement `AnnouncementDetector` (from hand + contract + rule set) in `src/Belote.Core/Announcements/AnnouncementDetector.cs`
- [X] T059 [P] [US4] Implement `AnnouncementComparer` (ordered tie-break list from rule set) in `src/Belote.Core/Announcements/AnnouncementComparer.cs`
- [X] T060 [P] [US4] Implement `BeloteTracker` (K+Q both played) in `src/Belote.Core/Announcements/BeloteTracker.cs`
- [X] T061 [US4] Extend `LegalActions.For(SeatView)` with `Announce` actions available with the seat's first card in `src/Belote.Core/Players/LegalActions.cs`
- [X] T062 [US4] Handle `Announce` in `GameEngine.Apply`: emit `AnnouncementDeclared`; after trick 1 emit `AnnouncementsRevealed` and `AnnouncementsCompared`; emit `BeloteAwarded`; in `src/Belote.Core/Engine/GameEngine.cs`
- [X] T063 [US4] Include counted announcements and belote in `HandScorer` totals (before multiplier) in `src/Belote.Core/Scoring/HandScorer.cs`
- [X] T064 [P] [US4] Extend `Projection` with declared/revealed announcements in `src/Belote.Core/State/Projection.cs`; confirm `RandomLegalBot` announces when legal in `src/Belote.Bots/RandomLegalBot.cs`

**Checkpoint**: Games with announcements finish; US4 acceptance scenarios 1–4 pass.

---

## Phase 7: User Story 5 - Hand and match scoring with edge cases (Priority: P2)

**Goal**: Failed contract, tied hand (held-over points), all-tricks bonus, rounding, and
simultaneous target reach are all handled and covered by tests.

**Independent Test**: Table-driven completed-hand fixtures yield the expected hand results and
match scores for every edge case.

### Tests for User Story 5 ⚠️ (write first, confirm failing)

- [X] T065 [P] [US5] Failed-contract tests in `tests/Belote.Core.Tests/Unit/FailedContractTests.cs`: R21 declarer 0, defenders receive all points in play (including both teams' counted announcements) times multiplier; with contra and recontra; last trick decides a near-miss
- [X] T066 [P] [US5] Tied-hand tests in `tests/Belote.Core.Tests/Unit/TiedHandTests.cs`: R22 defenders keep their points, declarer's points held over; held-over awarded to next hand's winner; held-over stacks across consecutive tied hands; unawarded if the match ends first; `PointsHeldOver`/`HeldOverAwarded` events
- [X] T067 [P] [US5] All-tricks tests in `tests/Belote.Core.Tests/Unit/AllTricksBonusTests.cs`: R23 +90 for a declarer sweep and for a defender sweep; bonus included in points in play; combined with announcements and multiplier
- [X] T068 [P] [US5] Match-end tests in `tests/Belote.Core.Tests/Unit/MatchEndEdgeCaseTests.cs`: R25 both teams reach target in one hand, higher total wins; exactly equal totals lead to further hands; held-over points can push a team over the target
- [X] T069 [P] [US5] Conservation property tests in `tests/Belote.Core.Tests/Properties/ScoringProperties.cs`: FR-018 for every scored hand across random games and all contract kinds, points awarded plus change in held-over equals points in play; scores never decrease

### Implementation for User Story 5

- [X] T070 [US5] Implement tied-hand hold-over and award logic (state `HeldOverPoints`, events) in `src/Belote.Core/Scoring/HandScorer.cs` and `src/Belote.Core/Scoring/MatchScorer.cs`
- [X] T071 [US5] Implement all-tricks detection and bonus (value from rule set) in `src/Belote.Core/Scoring/HandScorer.cs`
- [X] T072 [US5] Implement both-reach-target and equal-totals match-end rules from `MatchRules` in `src/Belote.Core/Scoring/MatchScorer.cs`
- [X] T073 [US5] Ensure the multiplier applies to failed-contract transfer and tie hold-over per rule set, and expose `HandResult` breakdown (raw totals, outcome, held-over before/after) in `src/Belote.Core/Scoring/HandResult.cs`

**Checkpoint**: All US5 acceptance scenarios pass; conservation property holds.

---

## Phase 8: User Story 6 - Reproducible seeded simulations with a log (Priority: P2)

**Goal**: A runner plays N seeded games, verifies each with an independent auditor, and
reports outcomes; the same seed gives byte-identical logs; every game has an ordered log.

**Independent Test**: Run the same seed twice and compare log hashes; run a batch and verify
per-game pass/fail with reasons; audit a saved log.

### Tests for User Story 6 ⚠️ (write first, confirm failing)

- [X] T074 [P] [US6] Determinism tests in `tests/Belote.Simulation.Tests/Integration/DeterminismTests.cs`: SC-002 same seed twice gives identical log hash and results for 1,000 sampled seeds; different base seeds differ; parallelism 1 vs 8 gives identical per-game results
- [X] T075 [P] [US6] Log tests in `tests/Belote.Simulation.Tests/Unit/GameLogTests.cs`: events strictly ordered by `Sequence`; header contains seed, full rule set, and seat controller names; JSON Lines round-trip is lossless; replaying accepted actions from header reproduces all events; no localized text in log
- [X] T076 [P] [US6] Auditor tests in `tests/Belote.Simulation.Tests/Unit/GameAuditorTests.cs`: valid log passes; tampered logs are detected and the first offending event is named for each of: duplicated card, missing card, illegal card played, wrong trick winner, wrong hand score, score not conserved, no winner / winner below target
- [X] T077 [P] [US6] Runner tests in `tests/Belote.Simulation.Tests/Integration/SimulationRunnerTests.cs`: N games produce N outcomes with seed/finished/winner/score/hands/logHash; summary counts; failed games retain seed and log; report ordered by game index; `Seeds.ForGame` makes any game re-runnable alone (SC-006)
- [X] T078 [P] [US6] Golden log tests in `tests/Belote.Core.Tests/Golden/GoldenLogTests.cs` with committed files in `tests/Belote.Core.Tests/Golden/*.jsonl` for seeds 1, 2, 3 under the Bulgarian rule set (changing a golden file requires review) *(Done in `tests/Belote.Simulation.Tests/Integration/GoldenAndBatchTests.cs`; golden files in `tests/Belote.Simulation.Tests/Golden/` because the test needs the runner and bots - analysis finding A2.)*
- [X] T079 [P] [US6] CLI tests in `tests/Belote.Simulation.Tests/Integration/CliTests.cs`: `run` exit code 0 and report written; failing game yields exit code 1; invalid arguments or invalid rule set yield exit code 2; `replay` of a seed produces identical files; `audit` reports the first offending event

### Implementation for User Story 6

- [X] T080 [P] [US6] Implement `GameLog` (header + ordered events), canonical serialization, and SHA-256 `LogHash` in `src/Belote.Simulation/Logging/GameLog.cs`
- [X] T081 [P] [US6] Implement JSON Lines writer/reader for logs and the card/seat/event codecs in `src/Belote.Simulation/Logging/GameLogJson.cs`
- [X] T082 [P] [US6] Implement rule-set JSON reader/writer (per contracts/rule-set.md) with validation in `src/Belote.Simulation/RuleSetJson.cs`
- [X] T083 [US6] Implement `GameAuditor` (replay through the engine plus independent 32-card accounting, legality re-check, trick winners, hand and match score recomputation, point conservation, single winner at or above target) in `src/Belote.Simulation/Auditing/GameAuditor.cs`
- [X] T084 [US6] Implement `SimulationRunner`, `RunOptions`, `GameOutcome`, `SimulationReport`, failure kinds, parallel execution with deterministic per-game seeds in `src/Belote.Simulation/SimulationRunner.cs`
- [X] T085 [US6] Make `GameRunner` produce a `GameLog` and record `ActionRejected` entries (excluded from hash) in `src/Belote.Simulation/GameRunner.cs`
- [X] T086 [P] [US6] Implement report JSON writer (per contracts/game-log-and-report.md) in `src/Belote.Simulation/ReportJson.cs`
- [X] T087 [US6] Implement `belote-sim` `run`, `replay`, `audit` commands, options, console summary, and exit codes per contracts/simulation-cli.md in `src/Belote.Simulation.Cli/Program.cs`
- [X] T088 [US6] Generate and commit golden logs for seeds 1, 2, 3 into `tests/Belote.Core.Tests/Golden/` after the auditor passes them *(Golden logs live in `tests/Belote.Simulation.Tests/Golden/`.)*

**Checkpoint**: Seeded batches run, are audited, and reproduce exactly; US6 acceptance
scenarios 1–4 pass.

---

## Phase 9: User Story 7 - Mixed bot and non-bot seats (Priority: P3)

**Goal**: Seats can be controlled by bots or other controllers (scripted "humans") with
identical rule enforcement, including illegal-move rejection and controller failure handling.

**Independent Test**: Run games with 1–4 scripted seats among bots; all finish correctly,
injected illegal moves are rejected with unchanged state.

### Tests for User Story 7 ⚠️ (write first, confirm failing)

- [X] T089 [P] [US7] Mixed-seat tests in `tests/Belote.Simulation.Tests/Integration/MixedSeatGameTests.cs`: 1, 2, 3, and 4 scripted seats among bots, 200 seeded games each, all finish with a winner and pass the auditor; outcome equals an all-bot run when the scripted controller makes the same choices as the bot (engine is seat-blind)
- [X] T090 [P] [US7] Illegal-move injection tests in `tests/Belote.Simulation.Tests/Integration/IllegalMoveInjectionTests.cs`: scripted controller submits an illegal bid, illegal card, out-of-turn action, and unknown announcement first; each is rejected, state/log hash unaffected, controller re-asked; exceeding `MaxIllegalAttempts` fails the game with `TooManyIllegalAttempts`
- [X] T091 [P] [US7] Controller failure tests in `tests/Belote.Simulation.Tests/Integration/ControllerFailureTests.cs`: a controller that throws fails the game with `ControllerException`; one that never returns fails with `ControllerTimeout` within the watchdog time; neither hangs the batch
- [X] T092 [P] [US7] View-entitlement tests in `tests/Belote.Core.Tests/Properties/ProjectionProperties.cs`: for random states and every seat, `SeatView` never contains another seat's unplayed cards, undealt-card data, or the seed (serialize the view and search); `LegalActions.For(view)` equals the engine's accepted set for that seat *(Covered by `GameProperties.cs` (Core) and `MixedSeatGameTests` (Simulation).)*

### Implementation for User Story 7

- [X] T093 [P] [US7] Implement `ScriptedPlayer` (plays from a supplied action script or strategy delegate, falls back to first legal action) in `src/Belote.Simulation/Players/ScriptedPlayer.cs`
- [X] T094 [P] [US7] Implement `IllegalFirstPlayer` decorator (submits a configured illegal action once per decision, then delegates) in `src/Belote.Simulation/Players/IllegalFirstPlayer.cs`
- [X] T095 [US7] Add per-decision watchdog (`DecisionTimeout`), exception capture, and `MaxIllegalAttempts` handling to `GameRunner` in `src/Belote.Simulation/GameRunner.cs`
- [X] T096 [US7] Add seat presets `all-bot`, `mixed-1`..`mixed-4`, `scripted-illegal` to `RunOptions` and the CLI `--seats` option in `src/Belote.Simulation/SeatPresets.cs` and `src/Belote.Simulation.Cli/Program.cs`

**Checkpoint**: All seven stories independently functional; US7 acceptance scenarios 1–3 pass.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Evidence for the success criteria and project hygiene

- [X] T097 [P] Add large-batch tests in `tests/Belote.Simulation.Tests/Batches/LargeBatchTests.cs` sized by environment variable `BELOTE_BATCH_GAMES` (default 1,000; CI nightly 10,000) covering SC-001, SC-003, and SC-004 (each mixed preset)
- [X] T098 [P] Add a rule-coverage traceability test in `tests/Belote.Core.Tests/Unit/RuleCoverageTests.cs` that fails if any `R1`..`R25` has no test named `Rxx_*` (SC-005)
- [X] T099 [P] Add CI workflow `.github/workflows/ci.yml`: build, `dotnet test`, PR batch of 1,000 games per preset, nightly 10,000-game batch, upload `report.json` and failing logs as artifacts
- [X] T100 Measure SC-007 (1,000 all-bot games under 5 minutes) with `belote-sim run --games 1000` and record the result in `specs/001-headless-belote-game/research.md`; profile and fix hot spots if exceeded *(Measured: 1,000 games in 1.6 s single-threaded; 10,000 games in 4.0 s; recorded in research.md.)*
- [X] T101 Resolve each **Verify** row in `specs/001-headless-belote-game/research.md` against a cited Bulgarian-rules source; update `src/Belote.Core/Rules/RuleSets.cs` values and affected tests (values only, no logic changes) *(Done: rules verified against belot.bg; results recorded in research.md, including the assumed items.)*
- [X] T102 [P] Write `README.md` at the repository root describing the solution layout, how to run tests and simulations, and how to add a rule-set variant
- [X] T103 Run every step of `specs/001-headless-belote-game/quickstart.md` and record pass/fail
- [X] T104 Code cleanup and public-API review of `Belote.Core` against `specs/001-headless-belote-game/contracts/core-api.md` (no unused public surface, XML docs on public types)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none; start immediately
- **Foundational (Phase 2)**: depends on Setup; BLOCKS all user stories
- **US2 Bidding (Phase 3)** and **US3 Play (Phase 4)**: depend on Foundational. US3 needs a
  contract to exist, so its engine wiring (T042) depends on T031; its pure pieces
  (T038–T040) can proceed in parallel with US2
- **US1 Full game (Phase 5)**: depends on US2 and US3 (integrates them with scoring and the bot)
- **US4 Announcements (Phase 6)**: depends on US3 and US1 scoring (T047)
- **US5 Scoring edge cases (Phase 7)**: depends on US1 scoring; interacts with US4 totals
  (T063) for the announcement-including cases
- **US6 Simulation (Phase 8)**: depends on US1 (runner/bot); best run after US4 and US5 so
  the auditor covers announcements and hold-over
- **US7 Mixed seats (Phase 9)**: depends on US1 runner; the auditor from US6 is used by its tests
- **Polish (Phase 10)**: depends on all stories

### User Story Dependencies

- US2 → US3 (engine wiring) → US1 → {US4, US5} → US6 → US7
- US4 and US5 touch `HandScorer.cs` and `GameEngine.cs`; do not run them concurrently
- US6 and US7 are independent of each other apart from sharing `GameRunner.cs`

### Within Each User Story

- Tests are written first and must fail before implementation
- Pure rule modules before engine wiring; engine wiring before integration/bot/runner work
- Story checkpoint before moving on

### Parallel Opportunities

- Setup T002–T007 in parallel
- Foundational tests T009–T013 in parallel; implementation T014–T016, T020–T023 in parallel
- Within a story, all test tasks marked [P] and all pure-module implementation tasks marked
  [P] touch different files and can run in parallel
- Phase 3 tests (T025–T027) and Phase 4 tests (T034–T037) can be written concurrently

---

## Parallel Example: User Story 3

```bash
# Tests for User Story 3 together:
Task: "Legal-card tests in tests/Belote.Core.Tests/Unit/LegalCardsTests.cs"
Task: "Trick resolution tests in tests/Belote.Core.Tests/Unit/TrickResolutionTests.cs"
Task: "Rejection tests in tests/Belote.Core.Tests/Unit/PlayRejectionTests.cs"
Task: "Property tests in tests/Belote.Core.Tests/Properties/PlayProperties.cs"

# Pure modules for User Story 3 together:
Task: "Implement card strength in src/Belote.Core/Play/CardStrength.cs"
Task: "Implement LegalCards in src/Belote.Core/Play/LegalCards.cs"
Task: "Implement TrickResolver in src/Belote.Core/Play/TrickResolver.cs"
```

---

## Implementation Strategy

### MVP First (through User Story 1)

1. Phase 1 Setup, Phase 2 Foundational
2. Phase 3 (US2) and Phase 4 (US3), then Phase 5 (US1)
3. **STOP and VALIDATE**: one seeded all-bot game finishes with a winner (T046); run quickstart step 2
4. Note: at this point announcements, hold-over, and sweep bonus are not yet implemented, so
   the MVP game is "complete" only for hands without those features

### Incremental Delivery

1. MVP (above)
2. US4 announcements → US5 edge-case scoring: rules complete
3. US6: batches, auditor, logs, CLI: proof of correctness at scale
4. US7: mixed seats and fault handling
5. Polish: CI, coverage traceability, rule verification, performance

### Parallel Team Strategy

After Foundational: one developer on US2 → US1 engine wiring, another on US3 pure modules
and tests, a third on US4 detection/comparison modules (T054–T060, which are pure) and the
US6 log/auditor modules that only depend on event types.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- Every rule row R1–R25 must end up referenced by at least one `Rxx_*` test (T098 enforces it)
- Rule *values* live only in `RuleSets.Bulgarian`; no task may hard-code a rule value elsewhere
- Verify tests fail before implementing; commit after each task or logical group
- `Belote.Core` must stay free of I/O, clocks, and ambient randomness (T012 enforces it)
