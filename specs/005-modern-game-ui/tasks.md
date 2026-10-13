---

description: "Task list for feature 005: Modern Game UI"
---

# Tasks: Modern Game UI

**Input**: Design documents from `/specs/005-modern-game-ui/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/hand-order.md](contracts/hand-order.md), [contracts/presentation.md](contracts/presentation.md), [quickstart.md](quickstart.md)

**Tests**: Included. Constitution Principle V requires tests with the one logic change (hand ordering), and the Development Workflow requires UI changes to be verified at phone size, in both languages and by keyboard.

**Organization**: Grouped by user story. US1 and US2 are P1, US3 and US4 are P2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 = ordered hand, US2 = paced card movement, US3 = table/players/dealer, US4 = dark theme
- Paths are relative to the repository root. `Belote.Core` is NOT modified by any task.

## Conventions used by every task

- No hub-protocol shape, rule, legality or hidden-information change. The only server edit is the order of `OwnHand` in `SeatViewMapper` (FR-026).
- Card power is never hard-coded in the web client; it arrives as the server's order within each suit.
- Every new user-facing string goes into BOTH `web/src/i18n/en.ts` and `web/src/i18n/bg.ts` (the `MessageKey` type enforces this).
- Moving elements use transform/opacity only. Animation durations come only from `web/src/theme/motion.ts`.
- Information is never conveyed by colour alone (dealer, turn, team, playable cards, suits).
- Existing `data-testid` hooks (`seat-*`, `status`, `score-ns`, `score-ew`, `result`, `notice`, `connection-banner`, `live-region`, `game-lost`) are kept.

---

## Phase 1: Setup

**Purpose**: Dependency and shared theme/motion scaffolding.

- [X] T001 Add the `motion` dependency (`^14.1.0`) to `web/package.json` via `npm install motion@^14.1.0` run in `web/`; commit the updated `web/package-lock.json`
- [X] T002 [P] Create `web/src/theme/motion.ts` exporting `PacingConfig` (`dealStaggerMs`, `dealTravelMs`, `playTravelMs`, `trickHoldMs`, `collectMs`, `snapMs`), `DEFAULT_PACING` with the values from [contracts/presentation.md](contracts/presentation.md) (60 / 350 / 350 / 1400 / 450 / 150), `INSTANT_PACING` (all zero), `pacingFromEnv()` returning `INSTANT_PACING` when `import.meta.env.VITE_PACING === "instant"`, and shared easing constants
- [X] T003 [P] Create `web/src/theme/tokens.css` defining the dark palette as CSS custom properties and mapping them in a Tailwind 4 `@theme` block: background, surface, felt (with radial vignette gradient), card face, card back, text primary/secondary, one amber accent, danger, warning, focus ring; each text/UI pair meeting WCAG AA (4.5:1 text, 3:1 UI) against its background
- [X] T004 Update `web/src/index.css`: remove the "no animations or transitions" reset block, `@import "./theme/tokens.css"` after the tailwind import, set `html`/`body` background and text colour from tokens, `color-scheme: dark`, and add a `@media (prefers-reduced-motion: reduce)` rule that is NOT a global kill-switch (motion handles it in JS)

**Checkpoint**: `cd web && npm run typecheck` passes; the app still renders (now on a dark page background).

---

## Phase 2: Foundational (blocks all user stories)

**Purpose**: Seat geometry, pacing context and an instant mode that existing tests keep using.

- [X] T005 [P] Create `web/src/theme/geometry.ts`: `SEAT_POSITIONS` for south (bottom), north (top), west (left), east (right) as unit offsets from the table centre, helper `offsetFrom(seat, scale)` returning `{x, y}` used for fly-in/out origins, `teamOf(seat)` (`northSouth` for north/south, `eastWest` otherwise) and `isMyTeam(seat)` per [data-model.md](data-model.md)
- [X] T006 [P] Create `web/src/presentation/PacingProvider.tsx`: React context exposing `PacingConfig`; props `value?: PacingConfig` defaulting to `pacingFromEnv()` then `DEFAULT_PACING`; hook `usePacing()`
- [X] T007 Make existing tests run with instant pacing: `App` takes an optional `pacing` prop (`web/src/App.tsx`) and `web/vite.config.ts` sets `VITE_PACING=instant` for Vitest, so `web/tests/table.test.tsx` and `web/tests/behaviour.test.tsx` keep asserting on immediately-settled state (no separate helper file was needed)

**Checkpoint**: `cd web && npm test` still passes unchanged in behaviour.

---

## Phase 3: User Story 1 - An ordered, readable hand (Priority: P1) 🎯 MVP

**Goal**: The person's hand is always grouped by suit (fixed order ♠ ♥ ♣ ♦) and sorted strongest to weakest within each suit for the active contract, stable while playing.

**Independent Test**: Deal several hands and confirm grouping and power order; play cards and confirm no reshuffle; with a trump contract confirm the trump group follows trump power.

### Tests first

- [X] T008 [P] [US1] Write `tests/Belote.Table.Tests/Unit/HandOrderTests.cs` (fails until T010): per [contracts/hand-order.md](contracts/hand-order.md): (a) a crafted hand under a hearts contract lists hearts in trump order (`JH 9H AH 10H KH`) and every other suit in plain order (`A 10 K Q J 9 8 7`); (b) no-trump and all-trump orders; (c) no contract yet (bidding) uses the no-trump order; (d) `OwnHand` is a permutation of the seat's hand; (e) FsCheck property over seeds and every shipped rule set in `RuleSets`: for each suit the sequence is `CardStrength`-descending and `LegalActions` and `HandSizes` are identical to the unsorted mapping
- [X] T009 [P] [US1] Write `web/tests/hand-order.test.tsx` (fails until T011): given `ownHand` interleaved across suits, the rendered hand buttons appear in order ♠ ♥ ♣ ♦ with the incoming order preserved within each suit; after a view with one card fewer the remaining cards keep their relative order; the trump group carries an accessible "trumps" tag and its position does not change when the contract changes

### Implementation

- [X] T010 [US1] In `src/Belote.Table/Views/SeatViewMapper.cs` sort `OwnHand` before mapping: order by suit group as encountered then by `CardStrength.Of(view.RuleSet, kind, card.Suit, card)` descending, with `kind = view.Contract?.Kind ?? ContractKind.NoTrump`; deterministic tie-break by rank order; leave `LegalActions`, `HandSizes` and all other fields untouched
- [X] T011 [US1] Create `web/src/components/handOrder.ts`: `SUIT_ORDER = ["S","H","C","D"]` and `groupBySuit(cards: string[], contractKind: ContractKind | null): { suit; cards; trump: boolean }[]` that partitions with a stable per-suit order (using `parseCard` from `web/src/i18n`) and marks `trump` for the suit contract's suit or all groups for `allTrump`; no rank ordering inside the function
- [X] T012 [US1] Update `web/src/components/Hand.tsx` to render `groupBySuit(game.ownHand, game.contract?.kind ?? null)` as suit groups (a labelled group per suit, with the "trumps" tag), keeping card keys equal to the card string so React preserves identity; playable cards keep their existing button semantics and `onAct` echo; add `label.trumps` to `web/src/i18n/en.ts` and `web/src/i18n/bg.ts`
- [X] T013 [US1] Run `dotnet test tests/Belote.Table.Tests` and `cd web && npm test`; fix any existing test that asserted the old wire order of `ownHand` (e.g. `tests/Belote.Server.Tests/EndToEnd/*`, `web/tests/fixtures.ts` expectations) by comparing as a set unless the order itself is the point

**Checkpoint**: US1 is shippable alone: ordered hand on the old look.

---

## Phase 4: User Story 2 - Paced, readable card movement (Priority: P1)

**Goal**: Cards visibly deal, travel and are swept at a calm, readable pace through a presenter that never lets input act on a stale table.

**Independent Test**: Watch a deal, a bot-only trick and its collection: cards travel, the finished trick holds ≥ 1 s then sweeps; controls enable right after settling on the person's turn; with reduced motion nothing travels but the pauses remain.

### Tests first

- [X] T014 [P] [US2] Write `web/tests/presenter-steps.test.ts` (fails until T016) for `diffViews(prev, next)`: first view → `deal`, `settle`; `dealNumber` change → `deal`; dealer change → `deal`; one more card in `currentTrick` → `play`; `currentTrick` emptied with a new `lastCompletedTrick` → `play(4th card)`, `hold`, `collect` in that order; two tricks coalesced into one update → both full sequences in order; unrelated change (bid, announcement, score) → `snap`; identical views → no steps
- [X] T015 [P] [US2] Write `web/tests/presenter.test.tsx` with fake timers (fails until T017): the presenter reports `settled === false` while steps run and `true` only when the drawn view is the latest; `trickHoldMs` elapses before `collect` starts; views queued during a hold are shown in order; with > 3 queued steps holds scale by `max(0.4, 3/queued)` ; `reset` on disconnect/`lost`/new game clears the queue and snaps to the latest view; with `INSTANT_PACING` every view is settled synchronously; card tap and bid buttons are disabled while unsettled even if `canAct` is true

### Implementation

- [X] T016 [US2] Create `web/src/presentation/steps.ts`: the `PresentationStep` union (`deal`, `play`, `hold`, `collect`, `snap`) and `diffViews(prev: TableView | null, next: TableView): PresentationStep[]` per [data-model.md](data-model.md), using only `dealNumber`, `dealer`, `currentTrick`, `lastCompletedTrick` and `completedTricks.length`
- [X] T017 [US2] Create `web/src/presentation/usePresenter.ts` and `web/src/presentation/PresenterProvider.tsx`: consume the store's `state.game`, `state.connection` and `state.session`; keep a FIFO step queue with timers from `usePacing()`; expose `{ view, latest, settled, stage, dealerOrigin }` per [data-model.md](data-model.md); apply queue compression (rule P8); clear and snap on disconnect, `lost`, new game or table close (rule P6); clean up timers on unmount
- [X] T018 [US2] Gate input: in `web/src/components/Table.tsx` read `{ view, settled }` from the presenter, draw everything from `view`, and compute `enabled = settled && canAct(state)`; keep the live region fed from the store's latest game (not the presented view) so announcements are not delayed
- [X] T019 [US2] Update `web/src/components/TrickArea.tsx` to draw the trick from the presenter `stage`: one slot per seat; a played card enters from `offsetFrom(seat)` over `playTravelMs` using `motion` (`AnimatePresence`, `initial`/`animate`); during `hold` all four cards stay with the winner emphasised (text + outline, not colour only); during `collect` cards exit toward the winner's seat over `collectMs`
- [X] T020 [US2] Update `web/src/components/Hand.tsx` to animate with `motion`: `layout` on cards so the hand closes up smoothly after a play, cards `AnimatePresence`-exit toward the table, and on `deal` the person's cards enter staggered by `dealStaggerMs` from `offsetFrom(dealerOrigin)`
- [X] T021 [P] [US2] Create `web/src/theme/useMotionPrefs.ts`: wraps `useReducedMotion()` from `motion/react` and returns variants where travel is replaced by opacity-only fades while durations (hold, stagger) are unchanged (P7); use it in `TrickArea.tsx` and `Hand.tsx`
- [X] T022 [US2] Add `VITE_PACING=instant` to the Playwright web server env in `web/playwright.config.ts` so `web/e2e/*` full-game specs keep running quickly, and add `web/e2e/pacing.spec.ts` (runs with real pacing via `page.addInitScript`/env override) asserting a completed trick stays visible ≥ 1 s before it is removed and that card controls are disabled until the table settles

**Checkpoint**: US2 is shippable on its own: paced, ordered hand, old table chrome.

---

## Phase 5: User Story 3 - Clear table, players and dealer (Priority: P2)

**Goal**: Seats around a felt table with card-back fans, a dealer marker whose deal visibly starts at the dealer's seat, turn/team emphasis, and contract and score visible at a glance.

**Independent Test**: Start a game and, unprompted, identify dealer, whose turn, your team, each opponent's card count and the contract/score; play two hands and watch the dealer marker move.

### Tests first

- [X] T023 [P] [US3] Write `web/tests/table-seats.test.tsx` (fails until T025–T028): exactly one element with the dealer marker (text "Dealer" and `data-testid="dealer-marker"`) inside the seat equal to `view.dealer`, and it moves when `view.dealer` changes; the seat matching `toAct` exposes a turn indicator with text (not only colour); each opponent seat renders a card fan whose back count equals `handSizes[seat]`; north is labelled partner with a team badge and east/west as opponents; contract and both team scores are present without any disclosure control; all strings render in Bulgarian after the language switch

### Implementation

- [X] T024 [P] [US3] Create `web/src/components/CardFan.tsx`: renders `count` card backs as a compact overlapped fan (horizontal for north, vertical for east/west) with `aria-hidden` and the count exposed as text on the seat; backs animate in on `deal` staggered from the dealer origin, using `usePacing()`
- [X] T025 [P] [US3] Create `web/src/components/DealerMarker.tsx`: a chip with a small card-stack glyph and the text from `label.dealer`; accessible name "Dealer"; `data-testid="dealer-marker"`
- [X] T026 [US3] Rework `web/src/components/Seat.tsx` into a seat panel: role label (you/partner/opponent), team badge (`label.yourTeam` / `label.theirTeam`, text + shape), turn ring plus "to act" text when `toAct`, `DealerMarker` when `dealer`, and `CardFan` for non-south seats; keep `data-testid="seat-<seat>"` and the existing `label.cards` count text for screen readers; add `label.yourTeam`, `label.theirTeam`, `label.dealing` to both language files
- [X] T027 [US3] Rework the layout in `web/src/components/Table.tsx` into a table composition: seats placed north/west/east/south around a felt surface (`bg-felt` token) with `TrickArea` in the centre, the compact contract + score strip above the table (reuse `ScoreBoard` content), status line and bid/announce panels below, and the hand at the bottom; single column on phones, wider felt with side seats from `md` upward; no horizontal scroll at 360 px
- [X] T028 [US3] Show deal origin: while `stage === "dealing"`, render a small card-stack at the dealer's seat from which `CardFan` and `Hand` cards originate, and show a `label.dealing` status for the duration; for a voided hand keep the existing voided notice visible during the re-deal
- [X] T029 [US3] Restyle `web/src/components/Chrome.tsx` `ScoreBoard` into a compact two-team score + contract strip using tokens, preserving `data-testid="score-ns"` and `"score-ew"` and all existing text

**Checkpoint**: US3 complete: the table reads as a table.

---

## Phase 6: User Story 4 - A cohesive dark theme (Priority: P2)

**Goal**: Every state uses the one dark palette, accent, typography and card style, with accessible contrast and shape-based suits.

**Independent Test**: Open every state (start, bidding, announcing, playing, trick complete, game over, disconnected, lost, notice) and confirm consistent dark styling and legible text.

### Tests first

- [X] T030 [P] [US4] Write `web/tests/theme-contrast.test.ts` (fails until T003 values are final): parse the token values from `web/src/theme/tokens.css` and assert the WCAG contrast ratio for each declared text/background and UI/background pair is ≥ 4.5:1 (text) or ≥ 3:1 (UI)
- [X] T031 [P] [US4] Write `web/tests/card-face.test.tsx` (fails until T032): a card face shows rank and a suit glyph with the suit name available as text for assistive tech, and its accessible name is unchanged (`cardName(language, card)`); a red-suit and a black-suit card differ in glyph shape, not just colour

### Implementation

- [X] T032 [US4] Restyle `web/src/components/Card.tsx`: dark-surface-friendly card faces from tokens (off-white face, rounded, corner rank + suit glyph, larger centre glyph), a back style used by `CardFan`, playable state = raised + accent outline, unplayable = dimmed + non-colour indicator (existing `label.notPlayable` text kept), focus-visible ring from the token; table cards and hand cards share one component for the face
- [X] T033 [P] [US4] Restyle `web/src/components/ActionPanels.tsx` (bid and announce panels) with shared button/panel classes from tokens; keep every handler, label and `disabled` rule unchanged
- [X] T034 [P] [US4] Restyle `web/src/components/Chrome.tsx` (`ConnectionBanner`, `NoticeBar`, `StartPanel`, `ResultPanel`) and `web/src/App.tsx` (shell, header, language button) with the tokens: dark page, one accent for primary actions, danger/warning tokens for notice and disconnected states; remove all remaining `slate-*`/`white` utility classes
- [X] T035 [US4] Add shared utility classes (`panel`, `btn`, `btn-primary`, `chip`) in `web/src/index.css` under `@layer components` using token variables, and replace repeated class strings in the components touched by T026–T034 with them
- [X] T036 [US4] Search `web/src` for leftover light-theme classes (`bg-white`, `bg-slate-*`, `text-slate-*`, `border-slate-*`, `bg-yellow-*`, `bg-blue-*`, `bg-amber-100`, `bg-red-50`) and replace with tokens; add a grep-based assertion to `web/tests/theme-contrast.test.ts` that none remain in `web/src/**/*.tsx`

**Checkpoint**: US4 complete: one consistent dark design across all states.

---

## Phase 7: Polish & cross-cutting

- [X] T037 [P] Extend `web/e2e/responsive.spec.ts`: at 360×740 and 1280×800 assert no horizontal overflow, that all four seats, the trick area, the hand and the action area are within the viewport (phone: scrolling vertically is allowed only for page chrome, not between seat and hand), and that the dealer marker count is exactly one
- [X] T038 [P] Extend `web/e2e/full-game.spec.ts` to assert hand order on every own turn: the rendered hand buttons are grouped by suit in ♠ ♥ ♣ ♦ order throughout a game
- [X] T039 [P] Add a reduced-motion Playwright case in `web/e2e/pacing.spec.ts` using `reducedMotion: "reduce"`: a game starts and the table renders with real pacing (movement-free behaviour is covered by the `useMotionPrefs` path; a frame-by-frame transform assertion was not added)
- [X] T040 Ran the [quickstart.md](quickstart.md) checks that are automatable (phone 375 px and desktop in the built-in browser for layout/overflow, Bulgarian and keyboard-only via e2e); pacing feel (deal ≈ 2 s, hold 1.4 s) was not judged by eye and should be tuned in `DEFAULT_PACING` after a hands-on play
- [X] T041 [P] Update `web/README.md` with the theme tokens location, pacing config and the `VITE_PACING=instant` switch, and document the hand-order guarantee in `specs/003-playable-table-ui/contracts/hub-client.md` as "ownHand is strength-ordered within suit (feature 005)"
- [X] T042 Run the full suite: `dotnet test`, `cd web && npm run typecheck && npm test && npm run build && npm run e2e`; fix regressions; confirm the production bundle growth from `motion` is noted in the PR description

---

## Dependencies & execution order

- **Phase 1 → Phase 2** first. T002, T003 parallel; T004 after T003; T007 after T006.
- **US1** (Phase 3) depends only on Phase 2 and can ship alone (MVP). T008/T009 parallel and before T010–T012.
- **US2** (Phase 4) depends on Phase 2; it touches `Hand.tsx` after US1 (T020 after T012) and `Table.tsx` (T018) before US3's layout rework (T027).
- **US3** (Phase 5) depends on US2's presenter for the deal origin (T028 after T017); T024/T025 are independent.
- **US4** (Phase 6) restyles files also touched by US3 (T026–T029); do T032 early if working in parallel since `CardFan` backs and hand cards share it, then T033–T036 after US3.
- **Polish** last.

```text
Setup ─► Foundational ─┬─► US1 (hand order) ──────────────┐
                       ├─► US2 (presenter, motion) ─► US3 ─┼─► US4 ─► Polish
                       └────────────────────────────────────┘
```

## Parallel opportunities

- Setup: T002 ‖ T003.
- Foundational: T005 ‖ T006.
- US1: T008 ‖ T009 (then T010 ‖ T011).
- US2: T014 ‖ T015; T021 alongside T019/T020.
- US3: T023 ‖ T024 ‖ T025.
- US4: T030 ‖ T031; T033 ‖ T034.
- Polish: T037 ‖ T038 ‖ T039 ‖ T041.

## Implementation strategy

1. **MVP**: Phases 1–3 (US1): ordered hand with a server-sorted array. Deliverable and testable on its own.
2. **Increment 2**: US2 — paced motion; the feature the user flagged as most irritating (speed).
3. **Increment 3**: US3 then US4 — table staging and full theme, merged together if reviewed as one visual change.
4. Keep `INSTANT_PACING` as the default in unit tests and e2e so the suite stays fast; only `pacing` tests use real timings.
