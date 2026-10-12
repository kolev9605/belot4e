---

description: "Task list for the Live Table vs Bots feature"
---

# Tasks: Live Table vs Bots

**Input**: Design documents from `/specs/002-live-table-vs-bots/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (all present)

**Tests**: INCLUDED. The spec's success criteria (SC-001..SC-007) and constitution Principle V
require seed-reproducible automated checks. Within each story, write the tests first and
confirm they fail before implementing.

**Organization**: Tasks are grouped by user story. US1 is the integrating story and builds
the table, hub and client skeleton; later stories harden and verify it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story the task belongs to (US1..US5)
- File paths are relative to the repository root (`src/`, `tests/` per plan.md)
- `FR-nnn` / `SC-nnn` refer to [spec.md](spec.md); name tests so the requirement is
  traceable (e.g. `FR014_DuplicateCardBurst_OnlyOneAccepted`)

## Path Conventions

Existing solution `Belote.sln`. New source: `src/Belote.Table`, `src/Belote.Server`. New
tests: `tests/Belote.Table.Tests`, `tests/Belote.Server.Tests`. `Belote.Core` and
`Belote.Bots` are **not modified**.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project skeletons and references

- [X] T001 Create `src/Belote.Table/Belote.Table.csproj` (class library, root namespace `Belote.Table`) referencing `Belote.Core`, `Belote.Bots` and `Belote.Simulation`; it MUST NOT reference any `Microsoft.AspNetCore.*` package
- [X] T002 [P] Create `src/Belote.Server/Belote.Server.csproj` using `Microsoft.NET.Sdk.Web`, referencing `Belote.Table` (SignalR comes from the shared framework; no extra server packages)
- [X] T003 [P] Create `tests/Belote.Table.Tests/Belote.Table.Tests.csproj` (xUnit, FsCheck.Xunit as in `tests/Belote.Simulation.Tests`, plus `Microsoft.Extensions.TimeProvider.Testing`) referencing `Belote.Core`, `Belote.Bots`, `Belote.Simulation`, `Belote.Table`
- [X] T004 [P] Create `tests/Belote.Server.Tests/Belote.Server.Tests.csproj` (xUnit, `Microsoft.AspNetCore.SignalR.Client`, same test packages) referencing `Belote.Core`, `Belote.Bots`, `Belote.Simulation`, `Belote.Table`, `Belote.Server`
- [X] T005 Add the four new projects to `Belote.sln` (sources under the `src` folder, tests under `tests`) and confirm `dotnet build Belote.sln` succeeds with warnings-as-errors on the non-test projects
- [X] T006 [P] Make `JsonNames` public in `src/Belote.Simulation/Logging/JsonNames.cs` so the wire vocabulary equals the log vocabulary (`internal static class` -> `public static class`; no other change); confirm the existing `tests/Belote.Simulation.Tests` still pass

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared types, wire DTOs and mapping every story needs. No user story can
start before this phase completes.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests first

- [X] T007 [P] Write mapper tests in `tests/Belote.Table.Tests/Unit/SeatViewMapperTests.cs`: a `SeatView` for each phase (bidding, play, finished) maps to `SeatViewDto` with seat/suit/contract/card names equal to the 001 log vocabulary (`north`, `hearts`, `noTrump`, `10S`); `legalActions` is non-empty only when `toAct == seat` and is in exactly the order `LegalActions.For(view)` returns; `ownHand` equals the view's `OwnHand`
- [X] T008 [P] Write intent-parsing tests in `tests/Belote.Table.Tests/Unit/IntentParserTests.cs`: each `IntentDto` shape in [contracts/hub-protocol.md](contracts/hub-protocol.md) parses to the matching `PlayerAction`; a `legalActions` entry round-trips through parse unchanged; unknown `kind`, missing `seat`, unknown seat, unparsable card, unknown contract, and malformed announcement all return a `malformed` rejection (never throw)

### Implementation

- [X] T009 [P] Implement `Principal` (`ConnectionPrincipal(string ConnectionId)`, `BotPrincipal(Seat Seat)`; equality only) and `Intent(Seat Seat, PlayerAction Action, Principal Sender)` in `src/Belote.Table/Intent.cs`
- [X] T010 [P] Implement `RejectionCategory` (`Malformed`, `NotSeated`, `GameNotInProgress`, `NotOwner`, `OutOfTurn`, `Illegal`, `TableExists`), `Rejection(Category, Code, Detail)`, and `IntentOutcome` (`Accepted(Version)` / `Rejected(Rejection)`) in `src/Belote.Table/Rejection.cs`; codes exactly as in data-model.md "Rejection categories and codes"
- [X] T011 [P] Implement `TableOptions` in `src/Belote.Table/TableOptions.cs`: `RuleSet` (default `RuleSets.Bulgarian`), `Seed` (`ulong`), `BotDelay` (`TimeSpan`, default 600 ms, clamped to "0 to 5 s"), `TimeProvider` (default `TimeProvider.System`), `LogDirectory?`
- [X] T012 [P] Implement `TableLifecycle` (`AwaitingSeat`, `InProgress`, `Finished`, `Faulted`), `CompletedTrick` (`TrickNumber`, `Leader`, `Plays[4]`, `Winner`) and the immutable `TableSnapshot` (`Version`, `State`, `LastCompletedTrick?`, `Lifecycle`, `Paused`) in `src/Belote.Table/TableSnapshot.cs`
- [X] T013 [P] Implement the wire DTO records `SeatViewDto`, `TableUpdateDto` (`protocolVersion` = 1, `version`, `finished`, `view`), `PlayedCardDto`, `CompletedTrickDto`, `BidEntryDto`, `AnnouncementDto`, `IntentDto`, `IntentResultDto`, `CreateTableRequest`, `CreateTableResultDto`, `TakeSeatResultDto` in `src/Belote.Table/Views/Dtos.cs` with fields exactly as in data-model.md "Wire layer"; configure camelCase + string enums in `src/Belote.Table/Views/WireJson.cs`
- [X] T014 Implement `SeatViewMapper` (`SeatView` + `CompletedTrick?` -> `SeatViewDto`; legal actions via `LegalActions.For`, emitted only when `view.IsMyTurn`) in `src/Belote.Table/Views/SeatViewMapper.cs` using `JsonNames` (depends on T006, T013)
- [X] T015 Implement `IntentParser` (`IntentDto` -> `Intent` fields or `Rejection{Malformed}`; never throws) and the inverse `LegalActionMapper` (`PlayerAction` -> `IntentDto`) in `src/Belote.Table/Wire/IntentParser.cs` (depends on T009, T010, T013)

**Checkpoint**: Foundation ready; T007/T008 pass and the solution builds.

---

## Phase 3: User Story 1 - Play a full game against three bots (Priority: P1) 🎯 MVP

**Goal**: A connected client creates a table, takes South, and plays a whole game to a
winner against three random-move bots using the 001 engine, receiving seat-view updates.

**Independent Test**: In-process, a scripted South finishes a seeded game with exactly one
winner at or above target; end to end, the SignalR test client does the same over the real
connection.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T016 [P] [US1] Write `LiveTable` tests in `tests/Belote.Table.Tests/Integration/FullGameTests.cs`: create table, `TakeSeatAsync(client)` seats South (partner North) and starts the game with version 0; a driver that always submits the first-offered legal action finishes a seeded game; final snapshot is `Finished` with one winner whose score reached the target; after finish `SubmitAsync` is `gameNotInProgress` (FR-005, FR-006, FR-007)
- [X] T017 [P] [US1] Write the seating tests in `tests/Belote.Table.Tests/Unit/SeatingTests.cs`: only `south` is accepted by `TakeSeatAsync`; a second `TakeSeatAsync` is refused; seat-to-principal map has South = client and North/East/West = three distinct `BotPrincipal`s (FR-002, FR-003, FR-004)
- [X] T018 [P] [US1] Create the SignalR test client in `tests/Belote.Server.Tests/Support/TableClient.cs`: connects over WebSockets, calls `CreateTable`/`TakeSeat`/`SubmitIntent`/`GetView`, records every `Update` in order, verifies `version` increases by exactly 1 per update, and exposes `PlayToEndAsync(Func<TableUpdateDto, IntentDto> chooser)`
- [X] T019 [P] [US1] Create the loopback server fixture in `tests/Belote.Server.Tests/Support/ServerFixture.cs`: builds the server via `ServerHost.Build` bound to `http://127.0.0.1:0`, exposes its address and `TableRegistry`, disposes cleanly (xUnit `IAsyncLifetime`)
- [X] T020 [US1] Write `tests/Belote.Server.Tests/EndToEnd/PlaysOneCompleteGame.cs`: client connects, creates a table with `{ "seed": 42, "botDelayMs": 0 }`, takes South, plays every turn by choosing a `legalActions` entry, and ends with `finished: true`, a winner, and zero rejected intents (depends on T018, T019)

### Implementation for User Story 1

- [X] T021 [US1] Implement `LiveTable` core in `src/Belote.Table/LiveTable.cs`: single-reader `Channel<WorkItem>` loop; `TakeSeatAsync` (South only, calls `GameEngine.Start(options.RuleSet, options.Seed)`, publishes version 0, `Lifecycle = InProgress`); `SubmitAsync` applies via `GameEngine.Apply`, bumps `Version` by exactly 1 per accepted intent, publishes an immutable `TableSnapshot` with `Volatile.Write`, and returns exactly one `IntentOutcome`
- [X] T022 [US1] Implement trick tracking in `LiveTable`: when an accepted result contains a `TrickWon`, build `CompletedTrick` from the previous state's `CurrentPlays` plus the played card, keep it in the snapshot until the next `CardPlayed` (needed for FR-027; verified in US3)
- [X] T023 [US1] Implement subscriber fan-out in `src/Belote.Table/LiveTable.cs`: `Subscribe(ITableObserver)`; after each published snapshot call each observer's `OnUpdateAsync` with the seat view update in version order, never skipping or duplicating (FR-015, FR-019)
- [X] T024 [US1] Implement game completion in `LiveTable`: when `Phase == Finished`, set `Lifecycle = Finished`, build the `GameLog` (header: seed, full rule set, seat names `RandomLegalBot` for North/East/West and `RemoteClient` for South) and complete `Completion` with a `FinishedGame`; further intents are `gameNotInProgress`/`GameFinished` (FR-007, FR-023)
- [X] T025 [P] [US1] Implement `BotDriver` in `src/Belote.Table/BotDriver.cs`: for North/East/West create `RandomLegalBot(Seeds.ForSeat(seed, seat))`; when `ToAct` is a bot seat, build `Projection.For(state, seat)`, ask the bot, and submit through `LiveTable.SubmitAsync` with `BotPrincipal(seat)`; a rejection of a bot's own choice sets `Lifecycle = Faulted` (no delay yet; pacing is US4)
- [X] T026 [P] [US1] Implement `FinishedGames` store in `src/Belote.Table/FinishedGames.cs` (in-memory list, `Add`, `All`; never exposed on the hub)
- [X] T027 [US1] Implement `TableRegistry` in `src/Belote.Server/TableRegistry.cs`: holds the single active `LiveTable`, creates a table from `CreateTableRequest` (server picks a seed with `RandomNumberGenerator` only when none is supplied), closes a finished table before creating a new one, and owns the `FinishedGames` store
- [X] T028 [P] [US1] Implement `ClientUpdatePump` in `src/Belote.Server/ClientUpdatePump.cs`: per-connection unbounded `Channel<TableUpdateDto>` drained by one task that sends `Update` to that connection, preserving version order without blocking the table
- [X] T029 [US1] Implement `TableHub` in `src/Belote.Server/TableHub.cs`: `CreateTable`, `TakeSeat`, `SubmitIntent` (parse with `IntentParser`, submit with `ConnectionPrincipal(Context.ConnectionId)`), `GetView` stub returning the current update; JSON protocol with `WireJson` options; hub path `/table`
- [X] T030 [US1] Implement `ServerHost.Build(string[] args, Action<ServerOptions>? configure)` in `src/Belote.Server/ServerHost.cs` and `src/Belote.Server/Program.cs`: registers SignalR, `TableRegistry` and `ClientUpdatePump` services, maps `/table`, binds `--urls`; `Program` calls `Build(args).Run()`

**Checkpoint**: User Story 1 works end to end: T016, T017 and T020 pass.

---

## Phase 4: User Story 2 - Server rejects invalid intents without changing state (Priority: P1)

**Goal**: Every illegal, out-of-turn, not-owned, malformed or not-in-progress intent is
refused with a category, code and detail, and leaves state identical.

**Independent Test**: A scripted catalogue of bad intents produces the expected category and
code for each, and the table snapshot (version, state reference, event count, log hash) is
unchanged each time.

### Tests for User Story 2

- [X] T031 [P] [US2] Write the rejection catalogue in `tests/Belote.Table.Tests/Unit/RejectionTests.cs`, one test per row of data-model.md "Rejection categories and codes": malformed intent; intent before seating (`NotSeated`/`GameNotStarted`); bid not higher (`BidNotHigher`); card not in hand (`CardNotInHand`); card that breaks follow-suit (`MustFollowSuit`); announce in the wrong phase (`WrongPhase`); South submits for a bot seat (`NotOwner`) on a bot's turn and on South's turn; South submits for South on a bot's turn (`NotYourTurn` -> `OutOfTurn`); any intent after finish (`GameFinished`) (FR-011, FR-012)
- [X] T032 [P] [US2] Write state-unchanged assertions in `tests/Belote.Table.Tests/Unit/RejectionStateTests.cs`: for every case in T031 capture `TableSnapshot` before and after and assert same `Version`, same `State` reference, same event count and same log hash, and that no observer received an update (FR-013); then assert a legal intent submitted afterwards is accepted (spec Story 2 scenario 5)
- [X] T033 [P] [US2] Write `tests/Belote.Server.Tests/EndToEnd/RejectionCatalogue.cs`: over the real hub produce each of `malformed`, `notSeated`, `gameNotInProgress`, `notOwner`, `outOfTurn`, `illegal` and `tableExists` (`CreateTable` mid-game, `TakeSeat` with `"north"`), assert `code` and non-empty `detail`, and assert no `Update` is received for any rejected intent and `GetView` is unchanged (SC-003)

### Implementation for User Story 2

- [X] T034 [US2] Implement the check pipeline in `LiveTable.SubmitAsync` (`src/Belote.Table/LiveTable.cs`) in this exact order: malformed (from the parser) -> notSeated -> gameNotInProgress (`GameNotStarted`, `GameFinished`, `TableFaulted`) -> notOwner (seat's principal != sender) -> engine; ownership is checked **before** turn so another seat's intent is always `NotOwner`
- [X] T035 [US2] Map engine rejections in `src/Belote.Table/Rejection.cs`: `RejectionReason.NotYourTurn` -> `OutOfTurn`/`NotYourTurn`; `RejectionReason.GameFinished` -> `GameNotInProgress`/`GameFinished`; every other reason -> `Illegal` with the 001 `RejectionReason` name as `code`; `detail` is English developer text including the seat and reason
- [X] T036 [US2] Guarantee atomicity in `LiveTable`: a rejected intent must not bump `Version`, replace the snapshot, append events, or notify observers; add an internal assertion/debug check comparing the snapshot reference before and after a rejection
- [X] T037 [US2] Complete hub error handling in `src/Belote.Server/TableHub.cs`: `TakeSeat` with any seat other than `"south"` returns `notOwner`; `CreateTable` while the client's game is unfinished returns `TableExists` and leaves the table untouched; `SubmitIntent` without a table returns `notSeated`; map outcomes to `IntentResultDto`

**Checkpoint**: User Stories 1 and 2 pass; T031-T033 green.

---

## Phase 5: User Story 3 - Receive only my seat's view, and request it any time (Priority: P1)

**Goal**: Every update and requested view contains only South's own cards and public table
information, legal moves exactly when it is South's turn, and trick completion is visible.

**Independent Test**: Across many seeded games, a privacy oracle built from the omniscient
log finds no card in any message outside South's own hand or the public table.

### Tests for User Story 3

- [X] T038 [P] [US3] Write the privacy oracle in `tests/Belote.Table.Tests/Support/PrivacyOracle.cs`: given a `TableUpdateDto` and the omniscient event list up to its version, return every card string found anywhere in the serialized message and the allowed set (cards dealt to South, plus cards in `CardPlayed` events so far this game); also assert the message JSON contains no `seed`, `deck`, `hands`, or other seat's hand
- [X] T039 [P] [US3] Write `tests/Belote.Table.Tests/Unit/ViewPrivacyTests.cs`: use FsCheck over seeds 0..N; at every version of a live game assert the oracle reports no violation (FR-017, SC-002); additionally assert that during bidding `ownHand` has only the `DealtCount` cards `Projection.For` allows
- [X] T040 [P] [US3] Write `tests/Belote.Table.Tests/Unit/LegalActionsViewTests.cs`: `legalActions` non-empty and equal to `LegalActions.For` exactly when `toAct == south`; empty when a bot is to act; empty in the final update (Story 3 scenarios 2-3)
- [X] T041 [P] [US3] Write `tests/Belote.Table.Tests/Unit/TrickVisibilityTests.cs` (FR-027): the update caused by a fourth card has all four plays and `winner` in `lastCompletedTrick` with an empty `currentTrick`; `lastCompletedTrick` persists in later views until the next card is played; the 8th trick of a hand (engine already dealt the next hand) is still exposed via `lastCompletedTrick`
- [X] T042 [P] [US3] Write `tests/Belote.Table.Tests/Unit/GetViewTests.cs` (FR-018): `GetView` equals the most recent update's view for the same version; calling it repeatedly never changes `Version`, state reference, or observers; valid before seating (`notSeated`) and after finish
- [X] T043 [P] [US3] Write ordering tests in `tests/Belote.Table.Tests/Unit/UpdateOrderTests.cs` (FR-019): observer versions are exactly `0,1,2,...` with no gap or duplicate over a full game, including a slow observer that delays each callback

### Implementation for User Story 3

- [X] T044 [US3] Implement `GetView(Principal)` in `src/Belote.Table/LiveTable.cs` as a lock-free read of the published snapshot; return `NotSeated` for a principal that does not own South
- [X] T045 [US3] Wire `GetView` in `src/Belote.Server/TableHub.cs` to `LiveTable.GetView(ConnectionPrincipal)` returning the same `TableUpdateDto` shape as pushed updates
- [X] T046 [US3] Make the trick-tracking from T022 authoritative in `SeatViewMapper` (`src/Belote.Table/Views/SeatViewMapper.cs`): always emit `lastCompletedTrick` from the snapshot, never from `SeatView.CompletedTricks`, so hand rollover cannot drop it
- [X] T047 [US3] Add an `AssertNoHiddenInformation` self-check used by tests only (`src/Belote.Table/Views/ViewGuards.cs`, internal, exposed to the test projects through `InternalsVisibleTo`) that fails when a `SeatViewDto` carries a field not listed in data-model.md "Visibility rules"; no production code path may call it

**Checkpoint**: Stories 1-3 pass; the privacy oracle is clean across the property run.

---

## Phase 6: User Story 4 - Bots behave like any other seat, at a human pace (Priority: P2)

**Goal**: Bots go through the same submit path with a configurable delay, see only their
own view, and the table pauses while the client is disconnected.

**Independent Test**: With a fake clock, consecutive bot moves are separated by at least the
configured delay; with zero delay a full game needs no waiting; detaching the client stops
all progress.

### Tests for User Story 4

- [X] T048 [P] [US4] Write `tests/Belote.Table.Tests/Unit/PacingTests.cs` using `FakeTimeProvider` (SC-007): with delay 500 ms no bot move happens until the clock advances 500 ms; consecutive bot moves are at least 500 ms apart; with delay 0 a full game completes without advancing the clock
- [X] T049 [P] [US4] Write `tests/Belote.Table.Tests/Unit/BotSeatBlindnessTests.cs` (FR-020, FR-021): wrap each bot in a recording `IPlayer` and assert it only ever receives `SeatView`s whose `Seat` equals its own; assert every bot move enters through `LiveTable.SubmitAsync` with a `BotPrincipal`; assert `LiveTable` source/behaviour does not branch on principal type (a test double principal type is accepted for a bot seat and for South identically)
- [X] T050 [P] [US4] Write `tests/Belote.Table.Tests/Unit/PauseTests.cs` (FR-026): after `Detach` no bot move, version change, or update occurs even after the clock advances; after `Attach` play resumes from the identical state; a bot decision in flight at `Detach` is not applied until `Attach`
- [X] T051 [P] [US4] Write `tests/Belote.Server.Tests/EndToEnd/DisconnectPausesTable.cs`: client closes its connection mid-game; the table registry's snapshot version stays constant for a bounded observation period and no exception reaches the host

### Implementation for User Story 4

- [X] T052 [US4] Add pacing to `BotDriver` (`src/Belote.Table/BotDriver.cs`): before each bot move `await Task.Delay(options.BotDelay, options.TimeProvider, ct)`; skip the call entirely when the delay is `TimeSpan.Zero`
- [X] T053 [US4] Implement attach/detach and the bot gate in `src/Belote.Table/LiveTable.cs` and `BotDriver`: `Detach` sets `Paused`; the driver awaits an open gate before and after its delay and before submitting; the actor rejects bot-origin work with an internal `Paused` outcome so a late submit cannot slip through; `Attach` reopens the gate (FR-026)
- [X] T054 [US4] Call `Detach` from `TableHub.OnDisconnectedAsync` in `src/Belote.Server/TableHub.cs` and dispose that connection's `ClientUpdatePump`
- [X] T055 [US4] Bind configuration in `src/Belote.Server/ServerHost.cs`: `Table:BotDelayMs` (default 600) and `Table:LogDirectory`; apply the per-table `botDelayMs` override from `CreateTableRequest` clamped to "0 to 5 s" (0..5000 ms); register `TimeProvider.System`

**Checkpoint**: Stories 1-4 pass; pacing and pause tests green.

---

## Phase 7: User Story 5 - Concurrency safety and a replayable record (Priority: P2)

**Goal**: Simultaneous intents are serialised with one definite outcome each; every game is
recorded in the 001 format and replays to the same final state, matching the 001 run for the
same seed.

**Independent Test**: A burst of concurrent intents yields exactly one outcome per intent and a
valid sequential final state; 100 seeded live games equal their 001 simulations and replay
cleanly.

### Tests for User Story 5

- [X] T056 [P] [US5] Write `tests/Belote.Table.Tests/Concurrency/BurstTests.cs` (FR-014, SC-005): fire N parallel `SubmitAsync` calls (mix of legal, illegal, out-of-turn, and two identical plays of the same card); assert N outcomes, exactly one of the duplicate pair accepted, and a final state equal to applying the accepted intents sequentially in the order the table processed them
- [X] T057 [P] [US5] Write `tests/Belote.Table.Tests/Concurrency/ClientVersusBotTests.cs`: with delay 0, run the client's intents concurrently with bot activity for a full game under a heavy-parallelism harness; assert no exception, no `Faulted` lifecycle, strictly increasing versions, and a valid audited log
- [X] T058 [P] [US5] Write `tests/Belote.Table.Tests/Integration/ReplayTests.cs` (FR-024, SC-004): for 20 seeds, take the finished game's `GameLog`, run `GameAuditor.Audit` and assert zero issues, and assert replayed final score and winner equal the live game's
- [X] T059 [P] [US5] Write `tests/Belote.Table.Tests/Integration/EqualsSimulationTests.cs` (clarification Q1, SC-001): for 20 seeds play a live game in-process with South choosing uniformly via `SeededRandom(Seeds.ForSeat(seed, South))` over the reported `legalActions`; run `GameRunner.RunAsync` with the `all-bot` preset for the same seed; assert the event line sequences are identical (header seat names excluded) and final scores equal
- [X] T060 [US5] Write `tests/Belote.Server.Tests/EndToEnd/HundredSeededGames.cs` (SC-001, SC-002, SC-004, SC-006): over one connection create 100 tables in sequence (base seed fixed, `Seeds.ForGame`), play each with the T059 policy via the hub; for every game assert zero rejected intents, `finished: true`, version contiguity, the privacy oracle over every received message and every `GetView`, events equal to the 001 all-bot run, and `GameAuditor` replay clean; the whole run completes in under 60 s (depends on T018, T019, T038)

### Implementation for User Story 5

- [X] T061 [US5] Configure the hub for concurrency in `src/Belote.Server/ServerHost.cs`: `HubOptions.MaximumParallelInvocationsPerClient` set to 16 so bursts reach the table, which serialises them (FR-014)
- [X] T062 [US5] Add optional log persistence in `src/Belote.Table/FinishedGames.cs`: when `LogDirectory` is set write `<seed>-<tableId>.jsonl` using `GameLog.ToJsonLines()`; failures to write must never affect the game (swallow and record a server log entry)
- [X] T063 [US5] Expose finished logs to tests only: `TableRegistry.FinishedGames` (public to `Belote.Server.Tests`), and assert in a hub reflection test (`tests/Belote.Server.Tests/EndToEnd/NoLogOnWireTests.cs`) that no hub method or `Update` payload type can carry a `GameEvent`, `GameLog`, or seed (FR-025)

**Checkpoint**: All five stories pass, including the 100-game run.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T064 [P] Add the new test projects to whichever CI workflow runs `dotnet test` (check for `.github/workflows/*`; if none exists, document the command in `README.md` under a "Live table" section) so the 100-game end-to-end run is part of every merge (Principle V)
- [X] T065 [P] Document the hub protocol link, run command and configuration keys (`Table:BotDelayMs`, `Table:LogDirectory`) in `README.md`
- [X] T066 [P] Add structured logging for table lifecycle events (created, seated, paused, finished, faulted) in `src/Belote.Server/TableRegistry.cs` using `ILogger`; never log hands, the seed, or full views
- [X] T067 Verify `Belote.Core` independence still holds: run the existing `CorePurityTests` and confirm `src/Belote.Core/Belote.Core.csproj` and `git diff --stat src/Belote.Core src/Belote.Bots` show no changes (constitution Principle I)
- [X] T068 Run every command in [quickstart.md](quickstart.md) and fix any drift between the guide and the actual test names, filters, and CLI arguments
- [X] T069 Run `dotnet build Belote.sln -c Release` and `dotnet test Belote.sln -c Release`; confirm no warnings in non-test projects and all suites green

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies; T006 is independent of T001-T005.
- **Foundational (Phase 2)**: needs Phase 1; blocks all stories.
- **US1 (Phase 3)**: needs Phase 2. It builds the table, hub and client that every later story extends.
- **US2, US3 (Phases 4, 5)**: need US1; independent of each other (both edit `LiveTable.cs`, so coordinate: US2 changes `SubmitAsync`, US3 adds `GetView`).
- **US4 (Phase 6)**: needs US1 (`BotDriver`); independent of US2/US3.
- **US5 (Phase 7)**: needs US1; its 100-game test (T060) also needs T038 (US3 oracle) and the US2 rejection pipeline to prove "no rejected legal moves".
- **Polish (Phase 8)**: after all desired stories.

### Within each story

Tests first and failing, then implementation. Foundational DTOs/mapper before `LiveTable`;
`LiveTable` before `BotDriver`/hub; hub before end-to-end tests.

### Parallel opportunities

- Phase 1: T002, T003, T004, T006 in parallel after T001.
- Phase 2: T007, T008 in parallel; T009-T013 in parallel; T014/T015 after them.
- US1: T016-T019 in parallel; T025, T026, T028 in parallel after T021.
- US2: T031-T033 in parallel.
- US3: T038-T043 in parallel.
- US4: T048-T051 in parallel.
- US5: T056-T059 in parallel.

## Parallel Example: User Story 3

```text
Launch together:
  T038 PrivacyOracle in tests/Belote.Table.Tests/Support/PrivacyOracle.cs
  T040 LegalActionsViewTests in tests/Belote.Table.Tests/Unit/LegalActionsViewTests.cs
  T041 TrickVisibilityTests in tests/Belote.Table.Tests/Unit/TrickVisibilityTests.cs
  T042 GetViewTests in tests/Belote.Table.Tests/Unit/GetViewTests.cs
```

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 Setup, Phase 2 Foundational.
2. Phase 3 (US1): in-process full game, then the hub and SignalR client.
3. **Stop and validate**: T016, T017, T020 pass. A remote client can finish a game.

### Incremental delivery

1. US2 adds trust (rejections) - ship with US1 as the first usable build.
2. US3 adds the privacy proof and on-demand view - required before any UI work.
3. US4 adds human-followable pacing and disconnect pause.
4. US5 adds the concurrency proof, replay proof, and the 100-game acceptance run.

### Notes

- Do not modify `Belote.Core` or `Belote.Bots`; if a task seems to need it, stop and
  revisit the design.
- All randomness reaching the engine comes from the table `Seed`; only the server's
  "no seed supplied" default may use ambient randomness (T027).
- Commit after each task or logical group; keep test and implementation in the same commit
  once green.

---

## Implementation Notes

Deviations from the task text found while implementing (behaviour and coverage are unchanged):

- **T036 / T047**: no production "assert unchanged" or `ViewGuards` type was added. Atomic rejection
  is proven by `RejectionTests` (same snapshot, state, and event-list objects, no update) and
  `RejectionCatalogue`; hidden-information safety by the independent `PrivacyOracle` (built from the
  omniscient log, shared into the server tests) and the reflection test in
  `ConcurrencyDisconnectAndWire`.
- **T022 / T046**: last-completed-trick tracking lives in `LiveTable` (`NextLastTrick`) and
  `SeatViewMapper` reads it from the snapshot, as designed; `CompletedTrickDto` also carries
  `dealNumber` so a trick kept across a hand rollover is unambiguous.
- **T027 / T063**: the finished-game store is `TableRegistry.Finished`; the table calls the
  `onFinished` callback inside the actor before the final update is pushed.
- **T029**: `TakeSeat` returns `{ ok, version }` (the initial view arrives as the first `Update`),
  and `GetView` returns `{ update?, rejection? }`. Documented in `contracts/hub-protocol.md`.
- **T034**: the check order gained `paused` after `gameNotInProgress`; the pause rule applies to
  every sender so the table never branches on human versus bot.
- **GetView before seating** returns `GameNotStarted` (spec edge case reworded) because no game
  state exists until the client takes its seat.
- **T055**: the server default delay is applied when a request omits `botDelayMs`; configuration
  keys are `Table:BotDelayMs` and `Table:LogDirectory`.
