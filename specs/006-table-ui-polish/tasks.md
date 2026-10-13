---

description: "Task list for feature 006: Table UI Polish"
---

# Tasks: Table UI Polish

**Input**: Design documents from `/specs/006-table-ui-polish/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/hand-summary.md](contracts/hand-summary.md), [contracts/presentation-steps.md](contracts/presentation-steps.md), [quickstart.md](quickstart.md)

**Tests**: Included. Constitution Principle V requires tests with the one logic addition (hand summary), and the Development Workflow requires UI changes to be verified at phone size, in both languages and by keyboard.

**Organization**: Grouped by user story. US1, US2 and US3 are P1; US4 and US5 are P2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 = fits one screen, US2 = aligned hand, US3 = cards normal while bidding, US4 = followable bidding, US5 = end-of-hand pop-up
- Paths are relative to the repository root. `Belote.Core` is NOT modified by any task.

## Conventions used by every task

- No new intents, rule, legality or hidden-information change. The only server edit is the additive, nullable `lastHand` field (Phase 2 / US5); protocol version stays `1`.
- Every new user-facing string goes into BOTH `web/src/i18n/en.ts` and `web/src/i18n/bg.ts` (the `MessageKey` type enforces this).
- No rule value (card points, bonuses, outcome) is hard-coded in the client; the pop-up shows what `lastHand` carries.
- Information is never conveyed by colour alone (bid chips, playable cards, team marker are text/shape as well).
- Animation durations come only from `web/src/theme/motion.ts`; moving elements use transform/opacity only.
- Existing `data-testid` hooks (`seat-*`, `status`, `score-ns`, `score-ew`, `result`, `notice`, `connection-banner`, `live-region`, `game-lost`, `hand`, `suit-group-*`) are kept.

---

## Phase 1: Setup

**Purpose**: Shared constants and types used by several stories.

- [X] T001 [P] Add `bidHoldMs` to `PacingConfig`, set it to `1100` in `DEFAULT_PACING` and `0` in `INSTANT_PACING` in `web/src/theme/motion.ts`
- [X] T002 [P] Add the `HandSummary`, `TeamHand`, `HeldOverSummary` types and `lastHand: HandSummary | null` on `TableView` in `web/src/protocol/types.ts`, matching [contracts/hand-summary.md](contracts/hand-summary.md); add `lastHand: null` to `makeView` in `web/tests/fixtures.ts`
- [X] T003 [P] Add sizing tokens to `web/src/theme/tokens.css`: `--card-h` derived from the viewport height with a clamped minimum and maximum (e.g. `clamp(3.4rem, 11dvh, 5.6rem)`), `--card-w` as `calc(var(--card-h) * 0.72)`, `--card-overlap`, `--suit-gap` (one fixed value used only at suit boundaries)

**Checkpoint**: types and tokens exist; nothing user-visible changes yet.

---

## Phase 2: Foundational (blocking prerequisite)

**Purpose**: Nothing blocks US1-US4 beyond Phase 1. The server-side `lastHand` blocks only US5 and is placed in US5's phase so P1 stories can ship first.

- [X] T004 Confirm Phase 1 compiles: run `npm run build` and `npm test` in `web/` and fix any type errors caused by `TableView.lastHand` in test fixtures and builders under `web/tests/`

**Checkpoint**: Foundation ready - user story phases may begin.

---

## Phase 3: User Story 1 - Everything fits on one laptop screen (Priority: P1) 🎯 MVP

**Goal**: The table, status and the person's whole hand are visible without vertical scrolling at 1366x768 and up, in every phase.

**Independent Test**: Open at 1366x768 and 1920x1080, go through bidding and playing: no vertical scrollbar, hand fully visible.

### Tests for User Story 1

> Write these first and confirm they fail.

- [X] T005 [P] [US1] Add a Playwright test "table fits the viewport height in every phase" to `web/e2e/responsive.spec.ts`: at 1366x768 and 1920x1080 assert `document.documentElement.scrollHeight <= window.innerHeight` and that the last card in `[data-testid="hand"]` has a bounding box inside the viewport during bidding and during play
- [X] T006 [P] [US1] Add a Playwright test "layout adapts to short and tall viewports" to `web/e2e/responsive.spec.ts` at 1280x600 and 1280x1000 with the same assertions (no overlap between `seat-south` box and the hand)

### Implementation for User Story 1

- [X] T007 [US1] Make the shell a viewport-height column in `web/src/App.tsx` and `web/src/index.css`: `main` uses `h-dvh` with `flex flex-col`, `overflow-hidden` on the page, and `min-h-0` on the flexible table region; remove `min-h-screen` and the stacked `mt-*` spacing
- [X] T008 [US1] Merge the header, connection banner and score into one compact top bar in `web/src/App.tsx` and `web/src/components/Chrome.tsx` (`ConnectionBanner` becomes a small status chip; `ScoreBoard` becomes a single-row bar keeping `data-testid="score-ns"` and `"score-ew"`, deal number, held-over and contract)
- [X] T009 [US1] Restructure `web/src/components/Table.tsx` into: seats + felt area (`flex-1 min-h-0`), a slim status line, and a bottom region holding the announcement strip and the hand; move the south seat box into the bottom region on the same row as the status so it no longer adds a separate block
- [X] T010 [US1] Make `web/src/components/TrickArea.tsx` height-driven: remove `min-h-[13.5rem]`/`sm:min-h-[16.5rem]`, size the grid from the available height and keep play/hold/collect offsets proportional (use values from the felt's measured size or CSS variables instead of the fixed `SLOT_SCALE`/`SEAT_SCALE` pixels)
- [X] T011 [P] [US1] Compact the seat box in `web/src/components/Seat.tsx`: one row for name + role, a single chip row (team, dealer, turn), remove the stand-alone "N cards" line (show the count inside the card fan), reduce padding; keep `data-testid="seat-*"` and `data-turn`
- [X] T012 [P] [US1] Size the opponents' card fans from the same height tokens in `web/src/components/CardFan.tsx`
- [X] T013 [P] [US1] Render the announcement panel as a compact one-line strip above the hand in `web/src/components/ActionPanels.tsx` (`AnnouncePanel`), returning `null` when there is nothing to show; keep `aria-labelledby="announce-heading"`
- [X] T014 [US1] Move the bidding controls into the felt centre while `game.phase === "bidding"` in `web/src/components/Table.tsx` and `web/src/components/ActionPanels.tsx` (`BidPanel` becomes a compact block shown in place of the empty trick area: legal-bid buttons plus a short "voided" notice; the call history moves to seat boxes in US4); keep `aria-labelledby="bid-heading"`
- [X] T015 [US1] Update `web/tests/table.test.tsx` and `web/tests/behaviour.test.tsx` for the moved bid/announcement regions (selectors by role/label, not by position)

**Checkpoint**: US1 passes its Playwright tests and the existing unit tests; the game is playable on a 1366x768 screen without scrolling.

---

## Phase 4: User Story 2 - A tidy, evenly aligned hand (Priority: P1)

**Goal**: All cards in the hand share one baseline with uniform spacing; no suit/rank/phase-dependent offsets.

**Independent Test**: Deal hands with different suit mixes; every card has the same top coordinate and adjacent gaps are equal.

### Tests for User Story 2

- [X] T016 [P] [US2] Add Vitest test `web/tests/hand-layout.test.tsx`: render `Hand` with hands covering 1, 2, 3 and 4 suits and a trump contract; assert every `[data-card]` element's wrapper has the same `style.transform`/class set (no translate), the "trumps" marker does not add an element that changes a group's height (group elements share the same structure), and the count of `-translate-y` classes on cards is zero outside hover
- [X] T017 [P] [US2] Add Playwright test "hand cards share one baseline and equal gaps" to `web/e2e/responsive.spec.ts`: read `getBoundingClientRect` of all `[data-card]` buttons during play and bidding; assert identical `top`, and that gaps between adjacent cards within a suit are equal and gaps at suit boundaries are equal to each other (the fixed `--suit-gap`)

### Implementation for User Story 2

- [X] T018 [US2] Rewrite the hand container in `web/src/components/Hand.tsx` as one non-wrapping row (`flex flex-nowrap items-end justify-center`), suit groups as inline wrappers with no per-group vertical content, constant overlap via `--card-overlap` and `--suit-gap`; ensure the row never wraps by letting overlap tighten with available width (computed in CSS from container width / card count)
- [X] T019 [US2] Replace the "Trumps" chip above a suit group with an in-card or underline marker that does not add height, in `web/src/components/Hand.tsx` and `web/src/components/Card.tsx` (keep an accessible text equivalent via `label.trumps`)
- [X] T020 [US2] In `web/src/components/Card.tsx`, remove the `-translate-y-0.5` for playable cards and the `-translate-y-1` hover on non-hovered/other cards; keep a lift only for the single hovered or keyboard-focused card, applied with `transform` so neighbours do not move; keep `ring`/bottom-indicator highlighting for playable cards
- [X] T021 [US2] Size `CardFace` from `--card-w`/`--card-h` in `web/src/components/Card.tsx` instead of the fixed `h-[4.25rem] w-12 sm:...` classes; keep `TableCard` sizes proportional
- [X] T022 [US2] Update `web/tests/hand-order.test.tsx` and `web/tests/card-face.test.tsx` for the new structure; keep ordering assertions unchanged

**Checkpoint**: US2 tests green; hand looks even in all phases.

---

## Phase 5: User Story 3 - Cards look normal while bidding (Priority: P1)

**Goal**: No card appears disabled during bidding; playing-phase highlighting is unchanged apart from the layout rules.

**Independent Test**: In bidding no `[data-card]` has `data-playable="false"` styling or the "not playable" label; in play, legal cards are highlighted and illegal ones visibly unavailable.

### Tests for User Story 3

- [X] T023 [P] [US3] Add Vitest tests in `web/tests/behaviour.test.tsx`: with `phase: "bidding"` no card has accessible name containing the "not playable" text and none renders the strike indicator or `opacity-45`; with `phase: "playing"` and legal plays, legal cards are `data-playable="true"` and others `"false"`
- [X] T024 [P] [US3] Add Playwright check to `web/e2e/rules.spec.ts` that during the bidding phase all `[data-card]` buttons are visually identical (same class list apart from `data-card`) and none carries `data-playable="false"`

### Implementation for User Story 3

- [X] T025 [US3] Add an `inert`/`state` prop to `HandCard` in `web/src/components/Card.tsx` with three states: `inert` (normal face, no ring, `aria-label` is just the card name, `disabled`/not tabbable, no strike line, no "not playable" sr text), `playable`, `unplayable`; keep `data-playable` only meaningful in `playing`
- [X] T026 [US3] Pass the phase into `Hand` and choose the state in `web/src/components/Hand.tsx` and `web/src/components/Table.tsx`: `inert` when `game.phase !== "playing"`; when `playing` but it is not the person's turn show cards `inert` too (no misleading "playable" highlighting), and `playable`/`unplayable` only when it is the person's turn and the presenter is settled
- [X] T027 [P] [US3] Add EN/BG strings if a new accessible label is needed for inert cards (otherwise reuse `card.name`) in `web/src/i18n/en.ts` and `web/src/i18n/bg.ts`

**Checkpoint**: US3 tests green; bidding hand looks normal.

---

## Phase 6: User Story 4 - Followable bidding with visible bid history (Priority: P2)

**Goal**: A perceptible pause between bot calls and each seat box shows that seat's calls in order.

**Independent Test**: Start a hand with bots: about a second between calls; each seat box lists its calls, latest emphasised; boxes clear on the next deal.

### Tests for User Story 4

> Write these first and confirm they fail.

- [X] T028 [P] [US4] Add cases to `web/tests/presenter-steps.test.ts`: `diffViews` yields one `bid` step per new `biddingHistory` entry, in order, before other steps of the same diff; no `bid` step when history is unchanged or shrinks (voided hand)
- [X] T029 [P] [US4] Add cases to `web/tests/presenter.test.tsx` with fake timers: with default pacing consecutive bid steps are `bidHoldMs` apart; with instant pacing they are immediate; the person's own call adds no delay; `settled` is false while a bid step runs
- [X] T030 [P] [US4] Add Vitest tests to `web/tests/table-seats.test.tsx`: a seat box lists its calls in order with the latest marked (e.g. `data-latest="true"`), shows several calls for one seat, shows nothing before the first call, and is empty after a voided/new deal

### Implementation for User Story 4

- [X] T031 [US4] Add the `bid` step kind to `web/src/presentation/steps.ts`: in `diffViews`, when `next.biddingHistory.length > prev.biddingHistory.length`, emit one `{ kind: "bid", frame }` per new entry (frame view = next as of that call) ahead of the other steps of that diff, per [contracts/presentation-steps.md](contracts/presentation-steps.md)
- [X] T032 [US4] Handle `bid` in `stepDuration` in `web/src/presentation/usePresenter.ts`: `pacing.bidHoldMs`, or `0` when the new call was made by the person's own seat (`prev.toAct === "south"`); keep `compressionScale` behaviour
- [X] T033 [P] [US4] Add `callLabel(language, entry)` to `web/src/components/labels.ts` returning the call text without the seat name (reuse the same keys as `historyLabel`)
- [X] T034 [US4] Show calls in `web/src/components/Seat.tsx`: derive `calls` for the seat from `game.biddingHistory`, render as chips (text labels via `callLabel`, `data-testid="seat-calls-<seat>"`), latest gets `data-latest="true"` plus an emphasis style that is not colour-only; hide the container when empty
- [X] T035 [US4] Remove the call-history list from `BidPanel` in `web/src/components/ActionPanels.tsx` (history now lives in seat boxes), keeping the "no bids yet"/voided notice and the legal-bid buttons
- [X] T036 [P] [US4] Add EN/BG strings for the seat-calls group label (screen-reader label such as "Calls") in `web/src/i18n/en.ts` and `web/src/i18n/bg.ts`; extend `web/tests/i18n.test.ts` if it enumerates keys

**Checkpoint**: US4 tests green; bidding is readable at the default pace and instant in tests.

---

## Phase 7: User Story 5 - End-of-hand results pop-up (Priority: P2)

**Goal**: After each hand a modal shows each team's won cards in one row, the hand score with breakdown and the game totals; at game end it names the winner and offers "Play again".

**Independent Test**: Play a hand to its end: pop-up with two labelled team rows (32 cards total), breakdown, hand and match scores; dismissing continues; finishing the game shows the winner.

### Tests for User Story 5 (server first, per Principle V)

> Write these first and confirm they fail.

- [X] T037 [P] [US5] Add `tests/Belote.Table.Tests/Unit/HandSummaryTests.cs`: after a hand is scored, `lastHand` is non-null with `dealNumber` of that hand; both teams' `wonCards` are disjoint and total 32 cards; for each team `cardPoints + lastTrickBonus + announcementPoints + belotePoints + allTricksBonus == rawTotal`; `matchScore` equals the previous match score plus each team's `gamePoints`; `lastHand` is null before the first hand ends; a voided hand does not replace it
- [X] T038 [P] [US5] Add a case to `tests/Belote.Table.Tests/Unit/ViewPrivacyTests.cs` asserting `lastHand` contains only cards that were played in that hand and is identical for every seat's view
- [X] T039 [P] [US5] Add `web/tests/hand-result.test.tsx`: `HandResultDialog` renders two labelled team sections each with a single row of `TableCard`s, marks the person's team, shows breakdown lines (card points, last trick, announcements, belote, all tricks, made/failed), hand game-points and match totals; with `winner` set shows the winning team and a "Play again" button; Escape and the primary button call `onDismiss`
- [X] T040 [P] [US5] Add cases to `web/tests/presenter-steps.test.ts`: `diffViews` emits a `result` step after the last trick's `play/hold/collect` and before `deal` when `lastHand.dealNumber` changes; none when `prev` is null or `lastHand` is unchanged; in `web/tests/presenter.test.tsx` the presenter does not advance past `result` until `release()` is called, and `settled` is false meanwhile

### Implementation for User Story 5

- [X] T041 [US5] Add `HandSummary`, `TeamHand`, `HeldOver` records in `src/Belote.Table/TableSnapshot.cs` (or a new `src/Belote.Table/HandSummary.cs`) and `LastHand: HandSummary?` to `TableSnapshot`, with `null` initial value per [data-model.md](data-model.md)
- [X] T042 [US5] In `src/Belote.Table/LiveTable.cs` build the summary when `accepted.Events` contains `HandScored`: collect the hand's plays (the last trick from `before.Hand.CurrentPlays` plus the played intent card, earlier tricks from `before.Hand.Tricks`) and `TrickWon` winners to compute `wonCards` per team in trick order; copy values from `HandResult`; read `matchScore` from `MatchScoreUpdated`; set `snapshot.LastHand`; leave it unchanged for other moves and for `HandVoided`
- [X] T043 [US5] Add `HandSummaryDto`, `TeamHandDto`, `HeldOverDto` and `LastHand` to `SeatViewDto` in `src/Belote.Table/Views/Dtos.cs`; map in `src/Belote.Table/Views/SeatViewMapper.cs` (new `lastHand` parameter next to `lastCompleted`) and pass `snap.LastHand` from `LiveTable.BuildUpdate`; use the existing `JsonNames` helpers for team/seat/contract names
- [X] T044 [US5] Add the blocking `result` step to `web/src/presentation/steps.ts`: extend `Frame` with `result: HandSummary | null`; in `diffViews`, emit `{ kind: "result", frame }` after the last trick steps (both the new-deal branch and the completed-tricks branch) when `next.lastHand` is non-null and `next.lastHand.dealNumber !== prev.lastHand?.dealNumber`; never when `prev` is null
- [X] T045 [US5] Make the presenter honour blocking steps in `web/src/presentation/usePresenter.ts`: `stepDuration` returns no timer for `result`; `pump` sets the frame and stops without scheduling; expose `release()` that clears the block and resumes `pump`; `clear()` also drops the block; `settled` is false while blocked
- [X] T046 [US5] Expose `release` from `PresenterProvider` in `web/src/presentation/PresenterProvider.tsx` (`PresentedTable.release`)
- [X] T047 [US5] Create `web/src/components/HandResultDialog.tsx`: native `<dialog>` opened via `showModal()`, labelled by its heading, two team sections (labelled; the person's team marked via text), each with a single wrapping row of `TableCard`s (scaled so the dialog never scrolls the page), breakdown list, hand score, match total, contract made/failed text, winner statement when `lastHand.winner` is set; primary button "Continue" (or "Play again" calling `startGame`) with initial focus; Escape and primary action call `onDismiss`; keep `data-testid="result"` on the dialog only when the game has finished
- [X] T048 [US5] Host the dialog in `web/src/components/Table.tsx`: render `HandResultDialog` when `frame?.result` is set, wiring `onDismiss` to `presented.release`; fold the existing `ResultPanel` into the dialog for the finished state and keep a compact "Play again" bar after the final dialog is dismissed (`web/src/components/Chrome.tsx`)
- [X] T049 [P] [US5] Add EN/BG strings in `web/src/i18n/en.ts` and `web/src/i18n/bg.ts`: dialog titles, team section labels, breakdown labels (card points, last trick, announcements, belote, all tricks), outcome text (made/failed/tied), "Hand score", "Game total", "Continue", empty-team text, accessible labels for the won-cards rows
- [X] T050 [US5] Update e2e helpers in `web/e2e/helpers.ts`: `playOneStep` also clicks the dialog's primary button when a hand-result dialog is open (unless the game is over), and `playToEnd` still waits for `data-testid="result"`; adjust `web/e2e/full-game.spec.ts` and `web/e2e/pacing.spec.ts` expectations (pop-up appears after each hand; no deal/bids proceed behind it)

**Checkpoint**: US5 tests green; per-hand and end-of-game pop-ups work in both languages.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T051 [P] Verify in `web/tests/theme-contrast.test.ts` that new text/UI pairs (call chips, dialog surfaces, breakdown text, inert vs playable ring) meet the existing contrast thresholds; add pairs as needed
- [X] T052 [P] Add a Playwright pass in `web/e2e/responsive.spec.ts` at 360x640 (phone) through a hand and the pop-up: no horizontal scroll, hand and pop-up usable
- [X] T053 [P] Add a keyboard-only Playwright pass in `web/e2e/responsive.spec.ts`: focus is trapped in the dialog while open and returns to the table after dismissal; a full hand is playable by keyboard
- [X] T054 [P] Add a Bulgarian-language Playwright pass checking seat call chips and the pop-up are localized and do not overflow
- [X] T055 [P] Update `web/README.md` with the one-screen layout notes, the `bidHoldMs` pacing value and the `lastHand` field
- [X] T056 Run `dotnet test`, `npm test` and `npx playwright test` and walk through [quickstart.md](quickstart.md) scenarios 1-13; fix regressions
- [X] T057 Run the bot-vs-bot simulations (`Belote.Simulation.Tests`) to confirm no engine/projection regression from the table changes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies; T001-T003 parallel.
- **Foundational (Phase 2)**: T004 after Phase 1.
- **US1 (Phase 3)**: after Phase 2. Touches `Table.tsx`, `Seat.tsx`, `ActionPanels.tsx`, `Chrome.tsx`, `App.tsx`.
- **US2 (Phase 4)**: after Phase 2; shares `Hand.tsx`/`Card.tsx` with US3 - do US2 before US3 to avoid conflicts. Independent of US1 except sizing tokens (T003).
- **US3 (Phase 5)**: after US2 (same files).
- **US4 (Phase 6)**: after US1 (needs the compact seat box and moved bid panel).
- **US5 (Phase 7)**: server tasks T037-T038, T041-T043 can start right after Phase 2 in parallel with client stories; client tasks T044-T050 after US1 (Table layout) and, for presenter files, after US4 (T031-T032 touch the same files).
- **Polish (Phase 8)**: after all desired stories.

### User Story Dependencies

- **US1, US2**: independent of each other (US2 uses tokens from T003).
- **US3**: builds on US2's `Hand`/`Card` changes.
- **US4**: depends on US1's layout for placement; otherwise independent.
- **US5**: server half independent; client half depends on US1 layout and on presenter edits of US4 (same files, sequence them).

### Within Each User Story

- Tests first and failing, then implementation.
- Types/DTOs before mappers, mappers before UI, UI before e2e helper updates.

### Parallel Opportunities

- T001-T003 together.
- T005/T006 together; T011-T013 together after T009.
- T016/T017 together; T023/T024 together.
- T028-T030 together; T033/T036 with T031-T032.
- T037-T040 together; server tasks T041-T043 run parallel to client work in other stories.
- T051-T055 together.

---

## Parallel Example: User Story 5

```text
# Tests first, all in parallel:
T037  tests/Belote.Table.Tests/Unit/HandSummaryTests.cs
T038  tests/Belote.Table.Tests/Unit/ViewPrivacyTests.cs
T039  web/tests/hand-result.test.tsx
T040  web/tests/presenter-steps.test.ts + web/tests/presenter.test.tsx

# Server and client halves in parallel:
T041-T043  (Belote.Table: snapshot, builder, DTO/mapper)
T049       (i18n strings)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 and 2.
2. Phase 3 (US1): the table fits a laptop screen.
3. **Stop and validate** at 1366x768; ship if wanted.

### Incremental Delivery

1. Setup + Foundational.
2. US1 (fit) -> US2 (aligned hand) -> US3 (normal cards while bidding): all P1, mostly layout and card styling.
3. US4 (bidding pace and history) -> US5 (pop-up with server `lastHand`).
4. Polish and the full quickstart walk-through.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- [Story] label maps each task to its user story.
- Tasks in `Hand.tsx`, `Card.tsx`, `Table.tsx`, `Seat.tsx`, `steps.ts` and `usePresenter.ts` are sequenced across stories because they edit the same files.
- Stop at any checkpoint to validate the story independently.
