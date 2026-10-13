---

description: "Task list for feature 004: Smart Bot AI"
---

# Tasks: Smart Bot AI

**Input**: Design documents from `/specs/004-smart-bot-ai/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/bot-api.md](contracts/bot-api.md), [contracts/benchmark-cli.md](contracts/benchmark-cli.md), [quickstart.md](quickstart.md)

**Tests**: Included. Constitution Principle V (test-first game logic) requires tests written before or with each heuristic, plus bot-vs-bot simulations.

**Organization**: Grouped by user story. US1 and US2 are P1, US3 is P2, US4 is P3.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 = purposeful rule-abiding play, US2 = measurably stronger than random, US3 = live table uses smart bots, US4 = random bot remains a baseline
- All paths are relative to the repository root. `Belote.Core` is NOT modified by any task.

## Conventions used by every task

- Bot types in `src/Belote.Bots` are `internal` except `SmartBot` and `SmartBotOptions`; the new test project sees them through `InternalsVisibleTo`.
- `Belote.Bots` may reference only `Belote.Core` (no new package or project references).
- Card strengths, points, trump identity and legality MUST come from `RuleSet`, `CardStrength`, `TrickResolver`, `LegalCards`, `LegalActions` - never hard-code ranks or points.
- The bot MUST pick only from `LegalActions.For(view)`; ties break by canonical order (action type, then contract rank, then suit, then rank); no randomness, no mutable state.

---

## Phase 1: Setup

**Purpose**: New test project and shared test support.

- [X] T001 Create `tests/Belote.Bots.Tests/Belote.Bots.Tests.csproj` modelled on `tests/Belote.Simulation.Tests/Belote.Simulation.Tests.csproj` (xunit 2.9.3, FsCheck.Xunit 3.4.0, Microsoft.NET.Test.Sdk 18.10.1, xunit.runner.visualstudio 4.0.1; `ImplicitUsings` enable, `IsTestProject` true) with project references to `src/Belote.Core` and `src/Belote.Bots` only; add the project to `Belote.sln` under the `tests` folder (`dotnet sln Belote.sln add tests/Belote.Bots.Tests/Belote.Bots.Tests.csproj --solution-folder tests`)
- [X] T002 Add `<InternalsVisibleTo Include="Belote.Bots.Tests" />` (inside an `ItemGroup`) to `src/Belote.Bots/Belote.Bots.csproj`
- [X] T003 [P] Create `tests/Belote.Bots.Tests/Support/ViewBuilder.cs`: a fluent builder that produces a `SeatView` (all record fields from `src/Belote.Core/State/State.cs`) for crafted situations - `Bidding(seat, hand, history, standingBid, standingBidder, contra, recontra)` and `Playing(seat, contract, hand, currentTrick, completedTricks, ownDeclared)`; defaults: `RuleSets.Bulgarian`, scores `[0,0]`, `ToAct == Seat`, `Dealer` North; plus helpers `Cards("AS 10H JD")` using `Card.TryParse` and `Trick(leader, "7H 9H AH 8H")` that fills `Winner` via `TrickResolver.Winner`
- [X] T004 [P] Create `tests/Belote.Bots.Tests/Support/GameDriver.cs`: helper that plays a full game with a caller-supplied `Func<Seat, IPlayer>` using `GameEngine.Start`, `Projection.For`, `GameEngine.Apply` exactly as in `tests/Belote.Simulation.Tests/Unit/BotTests.cs`, asserting each action is in `LegalActions.For(view)` and the step count stays under 20000; returns the final `GameState`

**Checkpoint**: `dotnet build Belote.sln` succeeds with the empty test project.

---

## Phase 2: Foundational (blocks all user stories)

**Purpose**: The shared building blocks every bot decision uses, plus the bot skeleton.

### Tests first

- [X] T005 [P] Write `tests/Belote.Bots.Tests/CardMemoryTests.cs` (fails until T008): (a) `Unseen`, `Played` and `OwnHand` partition the 32-card deck for views taken at every step of 20 seeded games (invariant `Unseen.Count + Played.Count + OwnHand.Length == 32`); (b) a seat that discarded off-suit on a led suit is recorded as void in that suit in `KnownVoids`; (c) in a suit contract a seat that did not trump a led plain suit while holding no led-suit card is NOT inferred to hold no trump unless the rules force trumping (`PlayRules.CutOnlyWhenOpponentWinning` case when partner was winning); (d) `IsMaster` is true for the ace of a suit with nothing higher unseen and, in a suit contract, true for the trump jack, and becomes true for a lower card once the higher ones are in `Played`; (e) `IsMaster` uses `CardStrength.Of` so it holds under no-trump and all-trump tables
- [X] T006 [P] Write `tests/Belote.Bots.Tests/TrickSituationTests.cs` (fails until T009): leading (no plays) gives no `CurrentWinner` and `PositionFromLast == 3`; with plays `[AH(N), 7H(E)]` for seat South `CurrentWinner == North`, `PartnerWinning == true`, `PositionFromLast == 1`; `PointsOnTable` sums `RuleSet.CardPoints(contract.Kind, card)`; a trump played over the led suit changes `CurrentWinner`; `PartnerSecure` is true when partner's card `IsMaster` and when the bot is last to play
- [X] T007 [P] Write `tests/Belote.Bots.Tests/SmartBotContractTests.cs` (fails until T010): `SmartBot` implements `IPlayer`; `DecideAsync` throws `InvalidOperationException` when it is not the seat's turn (`LegalActions.For` empty); the returned `ValueTask` is completed (`IsCompletedSuccessfully`); a single legal action is returned without evaluation (view with exactly one legal card)

### Implementation

- [X] T008 Implement `src/Belote.Bots/CardMemory.cs` (`internal sealed class CardMemory`, `static CardMemory From(SeatView view)`) per [data-model.md](data-model.md): `Played`, `Unseen` (deck from `Deck.Standard32` minus `OwnHand` minus played, across `CompletedTricks` and `CurrentTrick`), `RemainingBySuit`, `TrumpsRemaining`, `KnownVoids` per seat (void when a seat plays a card whose suit differs from the led suit of that trick), and `bool IsMaster(Card card)` using `CardStrength.Of(rules, kind, card.Suit, ...)` against every unseen card of the same suit plus unseen trumps in suit contracts; no state is cached outside the instance
- [X] T009 Implement `src/Belote.Bots/TrickSituation.cs` (`internal sealed record TrickSituation`, `static TrickSituation From(SeatView view, CardMemory memory)`) per [data-model.md](data-model.md): `LedSuit`, `CurrentWinner` via `TrickResolver.Best`, `PartnerWinning` (partner = `seat.GetTeam()` teammate, not self), `PositionFromLast`, `PointsOnTable`, `PartnerSecure`
- [X] T010 Implement `src/Belote.Bots/SmartBotOptions.cs` (`public sealed record SmartBotOptions` with `Standard` static instance and the fields listed in [data-model.md](data-model.md): `BidThreshold`, `OvercallPartnerMargin`, `ContraThreshold`, `RecontraThreshold`, `TrumpLengthWeight`, `BeloteWeight`, `SideAceWeight`, `MasterCardLeadBonus`; validate "finite and non-negative" in a private constructor/`init` guard) and `src/Belote.Bots/SmartBot.cs` (`public sealed class SmartBot : IPlayer`, constructor `SmartBot(SmartBotOptions? options = null)`; `DecideAsync` computes `LegalActions.For(view)`, throws `InvalidOperationException($"{view.Seat} was asked to act but has no legal action.")` when empty, returns the only action when there is exactly one, otherwise returns the first legal action in canonical order as a placeholder to be replaced by US1 tasks; returns `new ValueTask<PlayerAction>(action)`)

**Checkpoint**: T005-T007 pass; `SmartBot` plays legal-but-naive games.

---

## Phase 3: User Story 1 - Bots play purposeful, rule-abiding Belote (Priority: P1)

**Goal**: Bidding by hand strength, sensible trick play, announcements, always legal.

**Independent Test**: Smart bots in all four seats finish seeded games with every action legal; crafted-view tests show the bidding and play behaviours of acceptance scenarios 1-7.

### Tests first

- [X] T011 [P] [US1] Write `tests/Belote.Bots.Tests/HandEvaluatorTests.cs` (fails until T015): for a 5-card hand `JH 9H AH KD 7C`, `Scores[Hearts]` is greater than `Scores[Clubs]`, `Scores[Diamonds]` and `Scores[Spades]`; hand `AS AH AD AC 10S` scores `NoTrump` above every suit contract; hand `JS JH JD 9C 9S` scores `AllTrump` highest; a weak hand `7C 8D 7H 8S 9D` has every score below `SmartBotOptions.Standard.BidThreshold`; holding K+Q of the trump suit raises that suit's score by `BeloteWeight`; `Best` ties resolve to the lower bid rank
- [X] T012 [P] [US1] Write `tests/Belote.Bots.Tests/BiddingBehaviourTests.cs` (fails until T016), one test per rule: weak hand and first to bid passes (scenario 2); strong hearts hand with no standing bid bids `Hearts` (scenario 3); with standing bid `Spades` held by the opponent, a hand whose best contract does not outrank it passes and one that does (e.g. `NoTrump` strong) bids it; partner holds the standing bid and the bot's hand is only just over threshold: passes; partner holds the bid and bot's score exceeds threshold + `OvercallPartnerMargin`: bids the higher kind; never returns `ContraAction` when the standing bidder is its partner; returns `ContraAction` against an opposing suit contract when it holds the trump jack, nine and an outside ace; returns `RecontraAction` when its team's contract was doubled and its hand is strong, otherwise `PassAction`; every case asserts the result is in `LegalActions.For(view)`
- [X] T013 [P] [US1] Write `tests/Belote.Bots.Tests/PlayBehaviourTests.cs` (fails until T017-T019), one test per rule on crafted `Playing` views in a hearts contract unless stated: legal announcement available at the first decision is declared before any card, highest-points announcement first (scenario 7); partner winning with a master card and bot holds `10D` and `7D` and must follow: plays `10D` (scenario 4); partner winning but not secure and bot is not last: plays the lowest-point legal card; opponent winning, bot can win with `QS` or `AS`: plays `QS`, the cheapest winner (scenario 5); opponent winning and bot has only losers: plays the lowest-point, lowest-strength legal card (scenario 6); leading with a master plain card (`AD` after `AD`'s higher cards none): leads it; leading as declarer holding trump `JH` and `9H` while opponents still hold trumps: leads a trump; leading with nothing special: does not lead an unprotected `10` when a lower safe card exists; no-trump and all-trump contracts: the same behaviours hold using the contract's strength table (no hard-coded ranks); single forced card is played

### Implementation

- [X] T014 [P] [US1] Write `tests/Belote.Bots.Tests/DeterminismAndLegalityTests.cs` (fails until T015-T020): (a) calling `DecideAsync` twice with the same view on the same instance and on a fresh instance returns equal actions for every step of 25 seeded games; (b) all-smart games for seeds 1-100 finish with every action legal (use `GameDriver`); (c) legality still holds on rule-set variants built with `RuleSets.Bulgarian with { ... }`: contra and recontra disabled, announcements disabled in suit contracts, `MustOvertrump` false, `CutOnlyWhenOpponentWinning` false, a reduced `Bidding.Ranking` (suit contracts only); (d) a property (FsCheck) over random seeds that a smart seat never causes `GameEngine.Apply` to return a rejection
- [X] T015 [US1] Implement `src/Belote.Bots/HandEvaluator.cs` (`internal static class HandEvaluator`, `static HandEvaluation Evaluate(SeatView view, SmartBotOptions options)`; `HandEvaluation` record with `Scores` and `Best` per [data-model.md](data-model.md)): for every `ContractKind` in `view.RuleSet.Bidding.Ranking` compute the score as documented in [research.md](research.md) D4 - suit: `RuleSet.CardPoints` of own cards in the trump suit weighted by `TrumpLengthWeight` per extra trump, `BeloteWeight` when both K and Q of that suit are held, `SideAceWeight` per plain ace; no trump: `RuleSet.CardPoints` of aces and tens (protected kings add half); all trump: points of jacks and nines per suit with a length bonus; all values obtained from `RuleSet`, none hard-coded
- [X] T016 [US1] Implement bidding in `src/Belote.Bots/SmartBot.cs` (private `DecideBid`): evaluate with `HandEvaluator`; legal bids are those in `LegalActions.For(view)`; choose the highest-scoring legal `BidAction` whose score >= `BidThreshold` (>= `BidThreshold + OvercallPartnerMargin` when `view.StandingBidder` is the partner); `ContraAction` only when legal, the standing contract is an opposing suit contract, and the bot's defensive score for that suit >= `ContraThreshold`; `RecontraAction` only when legal and own team's evaluation for the standing kind >= `RecontraThreshold`; otherwise `PassAction`; set `SmartBotOptions.Standard` threshold values so T011-T012 pass
- [X] T017 [US1] Implement announcement handling in `src/Belote.Bots/SmartBot.cs` (private `DecideAnnouncement`): when any `AnnounceAction` is in `LegalActions.For(view)` return the one with the highest `RuleSet`-derived points (use `AnnouncementDetector`/spec points already exposed by `Belote.Core.Announcements`; break ties by canonical order) before any card decision
- [X] T018 [US1] Implement following logic in `src/Belote.Bots/SmartBot.cs` (private `ChooseFollow`) using `CardMemory` and `TrickSituation`: rules 3-6 of [research.md](research.md) D5 in order - feed (highest `CardPoints` legal card) when `PartnerWinning && PartnerSecure`; lowest-point legal card when partner winning but not secure and `PositionFromLast > 0`; cheapest winning card (lowest `CardStrength.Of` among legal cards that would beat `CurrentWinner`'s card) when an opponent is winning; otherwise discard the lowest-point then lowest-strength legal card, preferring suits that do not hold the K+Q belote pair
- [X] T019 [US1] Implement leading logic in `src/Belote.Bots/SmartBot.cs` (private `ChooseLead`) per [research.md](research.md) D5 rule 7: cash a card with `IsMaster` (prefer higher `CardPoints`, apply `MasterCardLeadBonus` ordering), as the declaring team in a suit contract lead trump while `TrumpsRemaining` is non-empty and the bot holds the trump jack or nine, as a defender lead the lowest card of the longest plain suit not in an opponent's `KnownVoids`, never lead a ten or king that is not master when a lower legal card exists in another suit
- [X] T020 [US1] Wire `SmartBot.DecideAsync` in `src/Belote.Bots/SmartBot.cs`: `Phase.Bidding` -> `DecideBid`; `Phase.Playing` -> announcements first, then `ChooseLead` when `view.CurrentTrick` is empty else `ChooseFollow`; guard every returned action with `LegalActions.Check(view, action) is null`, falling back to the canonical-order first legal action (so a heuristic bug can never produce an illegal move); confirm T011-T014 all pass

**Checkpoint**: US1 fully functional; `dotnet test tests/Belote.Bots.Tests` green.

---

## Phase 4: User Story 2 - Bots are measurably stronger than the random baseline (Priority: P1)

**Goal**: A repeatable, seat-swapped, audited benchmark proving at least 80% match wins.

**Independent Test**: `belote-sim bench --games 1000 --seed 1 --min-win-rate 0.80` exits 0; running it twice yields identical JSON.

### Tests first

- [X] T021 [P] [US2] Write `tests/Belote.Simulation.Tests/Unit/SmartPresetTests.cs` (fails until T024): `SeatPresets.Names` contains `all-smart`, `smart-ns`, `smart-ew`; `Create("all-smart", seed)` returns four controllers labelled `SmartBot`; `smart-ns` labels North/South `SmartBot` and East/West `RandomLegalBot`; `smart-ew` is the mirror; random seats use `new RandomLegalBot(Seeds.ForSeat(seed, seat))`; existing presets (`all-bot`, `mixed-*`, `scripted-illegal`) still label `RandomLegalBot`/`ScriptedHuman` exactly as before; `IsValid` accepts the new names
- [X] T022 [P] [US2] Write `tests/Belote.Simulation.Tests/Integration/BenchmarkTests.cs` (fails until T025): (a) `Benchmark.RunAsync(new BenchmarkOptions { BaseSeed = 1, GameCount = 400 })` has `Failed == 0`, `SmartWins + RandomWins == 400`, and `SmartWinRate >= 0.80`; (b) the result for the same options run twice is equal, and equal again at `Parallelism = 1` vs 8 (determinism, SC-003); (c) exactly half the games (rounded up) have `SmartTeam == NorthSouth` and the rest `EastWest`; (d) `MeanScoreMargin > 0`
- [X] T023 [P] [US2] Write `tests/Belote.Simulation.Tests/Integration/BlunderAuditTests.cs` (fails until T020 and T024): play 200 all-smart games (`SeatPresets.AllSmart`, `KeepLogs.All`), walk each log's events with a replayed `GameState`, and assert zero occurrences of the predicates in [research.md](research.md) D10: (1) a smart seat plays a trump on a trick its partner is `PartnerSecure`-winning while a non-trump legal card existed; (2) a smart seat plays a master card to a trick already securely won by the opponents while a lower-point legal card of any suit existed; (3) a smart seat issues `ContraAction` while its partner holds the standing bid

### Implementation

- [X] T024 [US2] Add presets in `src/Belote.Simulation/SeatPresets.cs`: constants `AllSmart = "all-smart"`, `SmartNs = "smart-ns"`, `SmartEw = "smart-ew"`; add them to `Names`; in `Create` build, for these three presets, each seat's controller as `new SeatController("SmartBot", new SmartBot())` or `new SeatController("RandomLegalBot", new RandomLegalBot(Seeds.ForSeat(gameSeed, seat)))`; leave the existing `scripted` handling and the random-only presets byte-for-byte unchanged (golden logs must still pass)
- [X] T025 [US2] Create `src/Belote.Simulation/Benchmark.cs`: `BenchmarkOptions` (`BaseSeed` default 1, `GameCount`, `RuleSet` default `RuleSets.Bulgarian`, `Parallelism`), `BenchmarkGame(Index, Seed, SmartTeam, Winner, NorthSouth, EastWest)`, `BenchmarkResult` (fields per [data-model.md](data-model.md): `BaseSeed`, `GameCount`, `SmartWins`, `RandomWins`, `SmartWinRate`, `MeanScoreMargin`, `Failed`, `Games`) and `static Task<BenchmarkResult> RunAsync(BenchmarkOptions, CancellationToken)`; uses `SimulationRunner.RunAsync` with a `SeatFactory` that picks `smart-ns` for even game index and `smart-ew` for odd (game `i` seed is `Seeds.ForGame(BaseSeed, i)`), `Audit = true`, `KeepLogs.None`; results ordered by index so output is independent of `Parallelism`; a game whose `FinishedCorrectly` is false counts in `Failed` and in neither win total
- [X] T026 [US2] Add the `bench` verb to `src/Belote.Simulation.Cli/CliApp.cs` per [contracts/benchmark-cli.md](contracts/benchmark-cli.md): options `--games` (required), `--seed`, `--rules`, `--parallelism`, `--out`, `--min-win-rate` parsed with the same helpers as `run`; print the human summary; write the JSON (camelCase, indented, games ordered by index) when `--out` is given; exit code 0/1/2 as specified; update the usage text (add the verb and the new presets to the "Seat presets" line)
- [X] T027 [US2] Add CLI tests in `tests/Belote.Simulation.Tests/Integration/BenchmarkCliTests.cs`: `bench --games 20 --seed 3 --out <tmp>` exits 0 and writes JSON with `gameCount == 20`; `--min-win-rate 1.01` exits 1; missing `--games` exits 2; the same arguments produce byte-identical `--out` files
- [X] T028 [US2] Tune `SmartBotOptions.Standard` in `src/Belote.Bots/SmartBotOptions.cs` using `belote-sim bench --games 1000 --seed 1`, then confirm seeds 2 and 3 also report `smartWinRate >= 0.80`; record the final values and the three measured win rates in a short comment above `Standard` (no behaviour changes outside option values); re-run T022 and T023

**Checkpoint**: US1 and US2 both pass; SC-001, SC-002, SC-003, SC-005 satisfied by automated tests.

---

## Phase 5: User Story 3 - The live table uses the smart bots by default (Priority: P2)

**Goal**: The three bot seats at a live table are smart bots; the live-equals-simulation guarantee still holds.

**Independent Test**: Create a table and play a game; the log labels North/East/West `SmartBot` and the live moves equal a `smart-table` simulation of the same seed.

### Tests first

- [X] T029 [P] [US3] Add to `tests/Belote.Simulation.Tests/Unit/SmartPresetTests.cs` (fails until T031): `smart-table` labels North/East/West `SmartBot` and South `RandomLegalBot` (seeded `Seeds.ForSeat(seed, Seat.South)`); it is listed in `SeatPresets.Names` and accepted by `IsValid`
- [X] T030 [P] [US3] Update `tests/Belote.Table.Tests/Integration/ReplayAndSimulationTests.cs`: in `ClarificationQ1_LiveGameEqualsThe001SimulationMoveForMove` use `SeatPresets.Create(SeatPresets.SmartTable, seed)` instead of `AllBot`; in `FR013_FR023_...` assert `"SmartBot"` instead of `"RandomLegalBot"` for North/East/West (expected to fail until T032)

### Implementation

- [X] T031 [US3] Add the `smart-table` preset (`SmartTable = "smart-table"`) to `src/Belote.Simulation/SeatPresets.cs`: South `RandomLegalBot` seeded with `Seeds.ForSeat(gameSeed, Seat.South)`, North/East/West `SmartBot`; add to `Names`
- [X] T032 [US3] Change `src/Belote.Table/LiveTable.cs`: `DefaultBot(ulong seed)` returns `seat => new Belote.Bots.SmartBot()` (parameter `seed` kept for the signature, or removed if no caller needs it - adjust the call at line 76 accordingly), and the seat label at the `nameof(Belote.Bots.RandomLegalBot)` site (line ~376) becomes `nameof(Belote.Bots.SmartBot)`
- [X] T033 [P] [US3] Repoint `tests/Belote.Server.Tests/EndToEnd/HundredSeededGames.cs` (line ~73) from `SeatPresets.AllBot` to `SeatPresets.SmartTable` so the 100-game end-to-end equivalence compares like with like
- [X] T034 [P] [US3] Run `dotnet test tests/Belote.Table.Tests` and `dotnet test tests/Belote.Server.Tests`; fix any remaining assertion that names `RandomLegalBot` for live-table bot seats (search with `grep -rn "RandomLegalBot" tests/Belote.Table.Tests tests/Belote.Server.Tests`), leaving the tests that deliberately inject `RandomLegalBot` through `botFactory` (`BotSeatBlindnessTests`, `SeatingTests`) untouched
- [X] T035 [US3] Add `tests/Belote.Table.Tests/Integration/SmartBotTableTests.cs`: a table created with default options and `BotDelay = 0` plays a seeded game to the end with a client that always picks `LegalActions[0]`; every bot intent was accepted (no rejections in the log); a table built with `botFactory: s => new RandomLegalBot(...)` still works (override path)

**Checkpoint**: US3 done; the full 002/003 behaviour is unchanged apart from bot quality.

---

## Phase 6: User Story 4 - The random bot remains available as a baseline (Priority: P3)

**Goal**: Random-only behaviour, presets and golden logs are untouched; random and smart bots mix freely.

**Independent Test**: Existing simulation tests and golden logs pass unchanged; a mixed random/smart batch completes legally.

- [X] T036 [P] [US4] Add `tests/Belote.Simulation.Tests/Integration/MixedBotTests.cs`: for seed sets 1-50 run all 16 combinations of {Smart, Random} per seat using `RunOptions.SeatFactory` (smart seats `new SmartBot()`, random seats `new RandomLegalBot(Seeds.ForSeat(seed, seat))`), audited; assert every game `FinishedCorrectly`
- [X] T037 [P] [US4] Extend the assembly-reference test in `tests/Belote.Simulation.Tests/Unit/BotTests.cs` (the one at `typeof(RandomLegalBot).Assembly.GetReferencedAssemblies()`): keep it passing now that the `Belote.Bots` assembly also contains `SmartBot`, and add a second assertion that `typeof(SmartBot).Assembly` references only `Belote.Core` and framework assemblies
- [X] T038 [US4] Verify golden and random-only suites are unchanged: run `dotnet test tests/Belote.Simulation.Tests --filter "FullyQualifiedName~Golden|FullyQualifiedName~RandomLegalBot|FullyQualifiedName~AllBot"` and confirm no golden file under `tests/Belote.Simulation.Tests/Golden` changed (`git diff --stat tests/Belote.Simulation.Tests/Golden` is empty)

**Checkpoint**: all four stories complete.

---

## Phase 7: Polish & cross-cutting

- [X] T039 [P] Update `README.md`: the `src/Belote.Bots` row mentions `SmartBot`; document the `bench` verb and new seat presets in the simulation section; note the live table now uses smart bots
- [X] T040 [P] Add a doc comment on `SmartBot` and `SmartBotOptions` describing the heuristic order (bidding model, play priority list) and the "stateless, deterministic, view-only" guarantee, referencing `specs/004-smart-bot-ai/research.md`
- [X] T041 Timing check (SC-004): run `belote-sim run --games 200 --seed 1 --seats all-bot` and `--seats all-smart`, record the elapsed times in the PR description, and confirm all-smart <= 2x all-bot; if exceeded, profile `CardMemory.From` (hoist per-decision allocations) before merging
- [X] T042 Full validation: `dotnet build Belote.sln -warnaserror` (src projects treat warnings as errors) and `dotnet test Belote.sln`; then walk [quickstart.md](quickstart.md) steps 1-4 and 6; step 5 (visual check in the browser) is a manual confirmation

---

## Dependencies & execution order

- **Phase 1 -> Phase 2 -> user stories.** Phase 2 blocks all stories.
- **US1 (Phase 3)** has no dependency on other stories.
- **US2 (Phase 4)** depends on US1 (it measures the bot) and on T024 for the blunder audit.
- **US3 (Phase 5)** depends on US1 (a real bot) and on T031 (preset); it does not need US2, but T028 tuning should land first so the live bots are tuned.
- **US4 (Phase 6)** depends only on Phase 2 (T010) for `SmartBot` to exist and on T024 for presets; it can run in parallel with US3.
- **Polish (Phase 7)** last.

Within a story: tests first (fail), then implementation. `SmartBot.cs` tasks T016-T020 edit the same file and are sequential. `SeatPresets.cs` tasks T024 then T031 are sequential.

## Parallel opportunities

- Phase 1: T003 and T004 together after T001-T002.
- Phase 2: T005, T006, T007 together; then T008 and T009 together (different files), then T010.
- US1: T011, T012, T013 together; T015 (HandEvaluator) in parallel with T017 (a different region only if split into partial classes - otherwise sequential on `SmartBot.cs`).
- US2: T021, T022, T023 together.
- US3: T029, T030, T033, T034 together once T032 lands.
- US4: T036, T037 together.
- Polish: T039, T040 together.

## Implementation strategy

1. **MVP = Phases 1-3 (US1)**: a legal, purposeful bot verified by crafted-view and seeded-game tests. Usable immediately through `SmartBot` in simulations.
2. **Add US2** to prove and tune strength (the 80% bar), which also locks in regression protection.
3. **Add US3** to ship the improvement to live players; this is the user-visible increment.
4. **US4** is verification of what must not change; do it alongside US3.
5. Finish with Polish and the quickstart walk-through.

## Task counts

| Phase | Tasks |
|-------|-------|
| 1 Setup | 4 (T001-T004) |
| 2 Foundational | 6 (T005-T010) |
| 3 US1 | 10 (T011-T020) |
| 4 US2 | 8 (T021-T028) |
| 5 US3 | 7 (T029-T035) |
| 6 US4 | 3 (T036-T038) |
| 7 Polish | 4 (T039-T042) |
| **Total** | **42** |
