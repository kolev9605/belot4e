---

description: "Task list for feature 003: Playable Table UI"
---

# Tasks: Playable Table UI

**Input**: Design documents from `/specs/003-playable-table-ui/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/hub-client.md](contracts/hub-client.md), [contracts/client-state.md](contracts/client-state.md), [quickstart.md](quickstart.md)

**Tests**: Included. The plan and constitution (Principle V, UI gates, hidden-information review) require them; server tasks are test-first.

**Organization**: Grouped by user story. US1-US3 are all P1; US4 is P2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 = play a complete game, US2 = rules enforced / mistakes explained, US3 = reliable connection, US4 = phone/keyboard/bilingual
- Client paths are under `web/`; server paths under `src/` and `tests/`

---

## Phase 1: Setup

**Purpose**: Create the `web/` package and tooling.

- [X] T001 Create `web/package.json` with scripts `dev`, `build`, `typecheck`, `test`, `e2e`, and dependencies: react@19, react-dom@19, @microsoft/signalr, vite, @vitejs/plugin-react, typescript, tailwindcss@4, @tailwindcss/vite, vitest, jsdom, @testing-library/react, @testing-library/user-event, @playwright/test; run `npm install` in `web/`
- [X] T002 [P] Create `web/tsconfig.json` (`"strict": true`, `noUncheckedIndexedAccess`, jsx react-jsx) and `web/vite.config.ts` (react + tailwind plugins, dev proxy `/table` -> `http://localhost:5080` with `ws: true`, Vitest `environment: "jsdom"`)
- [X] T003 [P] Create `web/index.html`, `web/src/main.tsx` (renders `<App />`), `web/src/index.css` (`@import "tailwindcss";`, no animations/transitions)
- [X] T004 [P] Add `web/node_modules/`, `web/dist/`, `web/playwright-report/`, `web/test-results/` to `.gitignore`
- [X] T005 [P] Create `web/playwright.config.ts` with a `webServer` that runs `dotnet run --project ../src/Belote.Server --urls http://localhost:5080 -- --Table:BotDelayMs=0 --Web:RootPath=../web/dist` and `baseURL: http://localhost:5080`; projects: desktop Chromium and a 375x812 phone viewport

---

## Phase 2: Foundational (blocks all user stories)

**Purpose**: Server changes required by the spec, plus the shared client skeleton.

### Server (test-first)

- [X] T006 [P] Write failing tests in `tests/Belote.Table.Tests/Unit/HandSizesTests.cs`: `handSizes` is 5 per seat before a contract and 8 after the full deal, decreases by one per card played, includes the viewer's own seat, and a serialized update/view contains no card string outside `ownHand`, `currentTrick`, `lastCompletedTrick`, `completedTricks`, `legalActions`, `ownDeclared` (hidden-information gate)
- [X] T007 [P] Write failing tests in `tests/Belote.Server.Tests/EndToEnd/ReplaceOnCreateTests.cs`: (a) a second connection's `CreateTable` replaces an unfinished game owned by another connection (connected or disconnected) and succeeds; (b) the displaced connected client receives `TableClosed` with `reason: "replaced"` and afterwards gets `notSeated` from `SubmitIntent` and `GetView`; (c) the same connection's `CreateTable` while its game is unfinished still returns `tableExists`; (d) the new table plays normally
- [X] T008 Add `HandSizes` (`ImmutableArray<int>` indexed by `Seat`) to `SeatView` in `src/Belote.Core/State/State.cs` and populate it in `src/Belote.Core/State/Projection.cs` as the count of each seat's hand, capped at `hand.DealtCount` while `hand.Contract is null`; no new dependencies, I/O, clock or randomness in `Belote.Core`
- [X] T009 Add `HandSizes` (`{ north, east, south, west }` counts) to `SeatViewDto` in `src/Belote.Table/Views/Dtos.cs`, map it in `src/Belote.Table/Views/SeatViewMapper.cs`, and add a `TableClosedDto { protocolVersion, reason }` type
- [X] T010 Implement replace-on-create in `src/Belote.Server/TableRegistry.cs`: when an unfinished table exists and `connectionId` differs from `_ownerConnectionId`, discard it through the existing disposal path, create the new table, and push `TableClosed` (`reason: "replaced"`) to the displaced connection via `IHubContext<TableHub>` if still connected; keep `tableExists` for the same connection; make T007 pass
- [X] T011 [P] Add optional static hosting to `src/Belote.Server/ServerHost.cs`: when configuration `Web:RootPath` is set, call `UseDefaultFiles`/`UseStaticFiles` for that directory and fall back to `index.html`; `/table` must remain mapped to the hub
- [X] T012 Update any existing 002 test in `tests/Belote.Server.Tests/EndToEnd/` and `tests/Belote.Table.Tests/` that asserts `TableExists` for a second *connection*, and add the additive changes (handSizes, replace semantics, `TableClosed`) to `specs/002-live-table-vs-bots/contracts/hub-protocol.md` with a link to `specs/003-playable-table-ui/contracts/hub-client.md`; run `dotnet test Belote.sln -c Release`

### Client skeleton

- [X] T013 [P] Create wire types in `web/src/protocol/types.ts` mirroring [contracts/hub-client.md](contracts/hub-client.md) and the 002 hub protocol (`TableUpdate`, `TableView` incl. `handSizes`, `IntentDto`, `IntentResult`, `Rejection` categories/codes, `TableClosed`) plus `PROTOCOL_VERSION = 1` and a `isSupportedUpdate` guard
- [X] T014 [P] Create `web/src/i18n/en.ts`, `web/src/i18n/bg.ts` and `web/src/i18n/index.ts` (typed key set, `t(key)`, language detection from `navigator.language`, `localStorage` preference wrapped in try/catch) with label tables for ranks, suits (`S H D C`), seats, teams, contracts, phases, rejection `code`s (`NotYourTurn`, `WrongPhase`, `BidNotHigher`, `BidNotAllowed`, `ContraNotAllowed`, `RecontraNotAllowed`, `CardNotInHand`, `MustFollowSuit`, `MustTrump`, `MustOvertrump`, `AnnouncementNotInHand`, `AnnouncementNotAllowed`, `AnnouncementWindowClosed`, `GameFinished`, `NotSeated`, `GameNotStarted`, `NotOwner`, `TableExists`, `TablePaused`, `Malformed`, unknown fallback) and UI strings
- [X] T015 [P] Write `web/tests/i18n.test.ts`: `en` and `bg` define identical keys, no value is empty, every rejection code has a message in both
- [X] T016 [P] Write failing reducer tests in `web/tests/reducer.test.ts` covering [contracts/client-state.md](contracts/client-state.md): next-version applied, duplicate dropped, gap flagged for resync, `finished` -> `session = finished`, `TableClosed`/`notSeated` -> `lost` clearing `game`/`lastVersion`/`pending`, `protocolVersion` mismatch -> notice
- [X] T017 Implement `web/src/state/reducer.ts` and `web/src/state/selectors.ts` (`isMyTurn`, `playableCards`, `bidActions`, `announceActions`, `trickToShow`) per [data-model.md](data-model.md); pure functions, no rule logic; make T016 pass
- [X] T018 Create `web/src/connection/hubClient.ts`: a `HubClient` interface (`connect`, `createTable`, `takeSeat`, `submitIntent`, `getView`, `onUpdate`, `onTableClosed`, `onStatus`) and a SignalR implementation using `HubConnectionBuilder().withUrl("/table", { transport: WebSockets })`, JSON protocol; basic start only (reliability is US3)
- [X] T019 Create `web/src/App.tsx` and `web/src/state/StoreProvider.tsx`: `useReducer` store, `HubClient` injected through context, wires hub events to reducer actions; renders a placeholder until US1 components exist

**Checkpoint**: `dotnet test` green; `npm run typecheck && npm test` green; `npm run dev` shows the shell connected to the server.

---

## Phase 3: User Story 1 - Play a complete game at the table (Priority: P1) MVP

**Goal**: Start a game and play every turn from the page until a winner is shown.

**Independent Test**: Open the page, press Start game, play only through the page UI until a winner and final score appear; press Play again and a fresh game starts.

### Tests for US1

- [X] T020 [P] [US1] Write component tests in `web/tests/table.test.tsx` rendering `Table` from a fixture `TableView`: four labelled seats with team, own hand cards, other seats' counts from `handSizes` (no card faces), table cards, dealer, contract/bidder, public announcements, both team scores, whose turn
- [X] T021 [P] [US1] Write `web/e2e/full-game.spec.ts`: open page, click Start game, loop clicking the first enabled bid/card/announce control until the result panel appears; assert winner and final score visible; click Play again and assert a new hand is dealt

### Implementation for US1

- [X] T022 [P] [US1] Create `web/src/components/Card.tsx`: `<button>` whose text is "<rank> <suit name>" (localized) plus `aria-hidden` suit glyph; `disabled` when not playable; no color-only information
- [X] T023 [P] [US1] Create `web/src/components/Hand.tsx`: South's cards from `ownHand`, marks playable ones using `playableCards`, click sends the matching `legalActions` entry unchanged
- [X] T024 [P] [US1] Create `web/src/components/Seat.tsx`: seat name, team, dealer marker, turn marker (text), remaining-card count from `handSizes`
- [X] T025 [P] [US1] Create `web/src/components/TrickArea.tsx`: shows `trickToShow` with each card labelled by seat, and the trick winner when the trick is complete (FR-004)
- [X] T026 [P] [US1] Create `web/src/components/BidPanel.tsx`: one button per `bid`/`pass`/`contra`/`recontra` entry in `legalActions`, labelled via i18n; shows bidding history and standing bid
- [X] T027 [P] [US1] Create `web/src/components/AnnouncePanel.tsx`: one button per `announce` entry in `legalActions`, labelled via i18n; lists public announcements and own declared ones
- [X] T028 [P] [US1] Create `web/src/components/ScoreBoard.tsx` (both teams' `matchScore`, `heldOverPoints`, deal number) and `web/src/components/ResultPanel.tsx` (winner and final score when `finished`, Play again button)
- [X] T029 [US1] Create `web/src/components/Table.tsx` composing Seat/TrickArea/Hand/BidPanel/AnnouncePanel/ScoreBoard/ResultPanel, and a Start game control shown in `idle`/`lost`; layout skeleton only (responsive polish is US4)
- [X] T030 [US1] Wire game actions in `web/src/App.tsx` / store: Start game = `createTable({})` then `takeSeat({seat:"south"})` (session `starting` until first update); intent submit sets `pending` and echoes a `legalActions` entry; Play again repeats start; make T020 and T021 pass

**Checkpoint**: A full game can be played end to end through the UI against the real server.

---

## Phase 4: User Story 2 - Rules enforced and mistakes explained (Priority: P1)

**Goal**: Nothing illegal can be played; refusals are explained and leave the table untouched.

**Independent Test**: Try to act off-turn, play a rule-breaking card, double-click a card; confirm no state change, a localized reason, and normal play afterwards.

### Tests for US2

- [X] T031 [P] [US2] Extend `web/tests/reducer.test.ts`: rejected `IntentResult` clears `pending`, sets `notice: rejection(code)`, leaves `game`/`lastVersion` unchanged; a subsequent accepted intent clears the notice; second send while `pending` is refused by the store
- [X] T032 [P] [US2] Write `web/tests/rules-ui.test.tsx`: off-turn view renders all cards disabled and no bid/announce controls, with a "not your turn" indicator; playable cards enabled only for entries in `legalActions`; notice shows localized text for `MustFollowSuit` in both languages
- [X] T033 [P] [US2] Write `web/e2e/rules.spec.ts`: during a game, assert disabled cards cannot be clicked into play (forced click leaves hand count unchanged), rapid double-click on a legal card plays exactly once, and a voided all-pass hand shows the "hand voided / redealt" note and play continues

### Implementation for US2

- [X] T034 [P] [US2] Create `web/src/components/Notice.tsx`: localized message from `notice.code` via i18n (never server `detail` text), `role="status"`/`aria-live`, dismissible, auto-cleared on next accepted intent
- [X] T035 [US2] Enforce `isMyTurn` + `connected` + no `pending` + entry-must-be-in-`legalActions` in the submit path in `web/src/state/StoreProvider.tsx`; surface "not your turn" and "action pending" states in `Table.tsx`
- [X] T036 [US2] Show a "hand voided, redealt" note when `dealNumber` advances with no contract set and bidding history was all passes (display only), in `web/src/components/BidPanel.tsx`; make T031-T033 pass

**Checkpoint**: US1 + US2 pass independently; illegal attempts are explained, never applied.

---

## Phase 5: User Story 3 - Reliable connection with clear status (Priority: P1)

**Goal**: Connect automatically, retry forever, show status, never present stale state as current, always reach a startable state.

**Independent Test**: Start the page before the server; stop/restart the server mid-game; confirm banner states, disabled input, and a startable "game lost" state with no manual reload.

### Tests for US3

- [X] T037 [P] [US3] Write `web/tests/connection.test.ts` with a fake `HubClient`/transport: initial-start retries with the 0/1/2/5/10 s schedule (fake timers) and never stops; drop -> `reconnecting` -> `connected`; every entry to `connected` calls `getView` before input is allowed; `getView` -> `notSeated` while playing -> `lost`; version gap triggers `getView`; push and `getView` race never lowers the version
- [X] T038 [P] [US3] Write `web/e2e/connection.spec.ts`: (a) load page with server not running, then start server -> becomes connected without reload; (b) mid-game stop and restart server -> banner shows loss within 5 s, connected again within 10 s of restart, "game lost" with working Start game; (c) page reload mid-game -> Start game works immediately; (d) second tab starting a game shows "game lost" in the first tab

### Implementation for US3

- [X] T039 [US3] Implement reliability in `web/src/connection/hubClient.ts`: initial-connect loop with backoff (0, 1, 2, 5, then every 10 s), `withAutomaticReconnect` using the same never-give-up schedule, `onclose` falling back to the start loop, status events `connecting | connected | reconnecting | unavailable`
- [X] T040 [US3] Implement resync in `web/src/connection/resync.ts` and wire into the store: `getView` after every entry to `connected`, on version gap, and on `document.visibilitychange` to visible; apply results through the same version-ordered reducer path (R4); handle `TableClosed` and `notSeated` as `lost`
- [X] T041 [P] [US3] Create `web/src/components/ConnectionBanner.tsx`: always-visible localized status (connecting, connected, reconnecting, unavailable) with a "state may be out of date" warning and `aria-live="polite"`; `Table.tsx` disables all input unless `connected`
- [X] T042 [US3] Add the `lost` experience in `web/src/components/Table.tsx`: clear "your game was lost" message (dropped, replaced or server restarted) and a Start game button enabled whenever `connected` (FR-016); make T037 and T038 pass

**Checkpoint**: All three P1 stories pass; the page recovers from every connection failure scenario in the quickstart.

---

## Phase 6: User Story 4 - Readable and operable on phone and desktop (Priority: P2)

**Goal**: Phone-first, keyboard-operable, accessible, English and Bulgarian.

**Independent Test**: Play a full game at 375 px using touch only, then at desktop width by keyboard only; switch language and confirm every visible string changes.

### Tests for US4

- [X] T043 [P] [US4] Write `web/tests/a11y.test.tsx`: every card button has an accessible name with rank and suit in both languages; state changes (turn, trick won, notice, connection) are announced in an `aria-live` region; no information is color-only (disabled cards also expose "not playable" text)
- [X] T044 [P] [US4] Write `web/e2e/responsive.spec.ts`: on the 375x812 project assert `document.documentElement.scrollWidth <= innerWidth` through a game; on desktop play a full game using only `Tab`/`Enter`/`Space`; toggle the language and assert no visible English strings remain in Bulgarian mode on the table, notices, banner and result

### Implementation for US4

- [X] T045 [US4] Make `web/src/components/Table.tsx` and children phone-first with Tailwind utilities: single column at <640 px with South hand at the bottom wrapping cards, opponent seats compact; enhance at `sm:`/`lg:`; no animations, transitions or hover-only affordances; 16 px side gutters, minimum 44 px touch targets
- [X] T046 [P] [US4] Create `web/src/components/LanguageSwitch.tsx` (single small control; persists via i18n helper) and render it in `App.tsx`; ensure `<html lang>` follows the selection
- [X] T047 [P] [US4] Add keyboard support and visible focus styles: logical tab order (bid/announce controls, then hand), focus kept on the next actionable control after a play, no focus traps; add an `aria-live` region in `web/src/components/Table.tsx` announcing turn changes, trick results and game end
- [X] T048 [US4] Audit contrast of the Tailwind palette used (WCAG AA) and fix in `web/src/index.css`/components; make T043 and T044 pass
  - Done by inspection only (secondary text and disabled states darkened to slate-600); no automated contrast tool was run.

**Checkpoint**: All four stories pass; phone viewport, keyboard and both languages verified.

---

## Phase 7: Polish & Cross-Cutting

- [X] T049 [P] Add CI jobs to `.github/workflows/ci.yml`: `web` (Node 22: `npm ci`, `typecheck`, `test`, `build`) and `e2e` (setup-dotnet + setup-node, build client, `npx playwright install --with-deps chromium`, `npm run e2e`, upload report on failure)
- [X] T050 [P] Add `web/README.md` (prerequisites, dev/prod run, test commands) and update the root `README.md` with the UI run instructions from [quickstart.md](quickstart.md)
- [X] T051 [P] Add a Playwright check in `web/e2e/hidden-info.spec.ts` that records every WebSocket frame over a full game and asserts no card appears outside the viewer's hand and public table fields (SC-006)
- [X] T052 Run all quickstart scenarios 1-7 from [quickstart.md](quickstart.md) manually at phone viewport in English and Bulgarian and with keyboard; record results in `specs/003-playable-table-ui/quickstart.md` notes
  - Done by automation, not by eye: scenarios 1-7 are covered by the Playwright specs (phone project at 375 px, keyboard-only game, language switch, restart/offline recovery) and unit tests; no human visual pass was made.
- [X] T053 Run 50 consecutive e2e games (SC-002) with zero stuck games and confirm `dotnet test Belote.sln -c Release`, `npm test`, and `npm run e2e` are all green; fix any flake at its source

---

## Dependencies & Execution Order

- **Phase 1 -> Phase 2 -> user stories -> Polish.** Phase 2 blocks everything.
- Within Phase 2: T006/T007 (failing tests) before T008-T010; T008 -> T009 -> T010; T011 independent; T012 after T010. Client skeleton T013-T019 is independent of the server tasks, except T019 needs T018 and T017.
- **US1** needs Phase 2 only. **US2** extends US1's store/components (T034-T036 build on T029/T030). **US3** extends `hubClient.ts` and `Table.tsx` from Phase 2/US1, and can start after T030. **US4** styles the components from US1-US3, so it runs last.
- Stories remain independently *testable*: each checkpoint has its own e2e spec.

## Parallel Opportunities

- Setup: T002-T005 together after T001.
- Foundational: T006, T007, T011, T013, T014, T015, T016 together.
- US1: T022-T028 are separate component files and can be built together; T020/T021 tests in parallel.
- US2: T031-T034 together. US3: T037, T038, T041 together. US4: T043, T044, T046, T047 together.
- Polish: T049, T050, T051 together.

### Parallel Example: User Story 1

```text
Parallel: T020, T021, T022, T023, T024, T025, T026, T027, T028
Then:     T029 (Table.tsx) -> T030 (wire actions)
```

## Implementation Strategy

- **MVP**: Phases 1-3 (through T030): a playable game over a basic connection.
- **Increment 2**: US2 (rules feedback) then US3 (reliable connection). The user explicitly requires both, so the feature is not "done" at MVP.
- **Increment 3**: US4 (phone, keyboard, bilingual) and Polish.
- Server changes (T006-T012) go first and ship with their own tests because they alter the 002 contract.
