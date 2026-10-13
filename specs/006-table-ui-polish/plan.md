# Implementation Plan: Table UI Polish

**Branch**: `006-table-ui-polish` | **Date**: 2026-10-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/006-table-ui-polish/spec.md`

## Summary

Five fixes to the existing table, almost all in the React client (`web/`), with one small additive
server change so the client can show a finished hand:

1. **One-screen layout** - the page becomes a viewport-height column (`dvh`): a single compact top
   bar (title, language, connection, score, contract), a flexible table area, and a hand strip
   pinned at the bottom. The bidding and announcement panels stop being stacked blocks below the
   table; they sit inside the felt's centre / a slim strip. Card size derives from available
   height so the hand always fits.
2. **Aligned hand** - one non-wrapping row, constant overlap, no per-group offsets. The two
   causes of today's misalignment are removed: the "Trumps" chip that makes one suit group taller,
   and the `-translate-y` used to flag playable cards. Playability is shown with ring/underline
   only.
3. **Neutral cards while bidding** - the card component gets a third state, "inert", used outside
   the playing phase; "playable / not playable" is only decided during play.
4. **Followable bidding** - a new presenter step (`bid`) holds each new call on screen for about a
   second before the next one, and every seat box lists that seat's calls, derived from the
   `biddingHistory` the client already receives.
5. **Hand results pop-up** - a new blocking presenter step (`result`) opens a modal after the last
   trick of a hand has been shown. Content comes from a new nullable `lastHand` field on the table
   view (server-built from the game events: won cards per team, score breakdown, totals).

No new dependencies. Protocol stays at version 1 (additive, nullable field).

## Technical Context

**Language/Version**: TypeScript (strict) + React 19 on the client; C# / .NET 10 for the one
server-side addition

**Primary Dependencies**: existing React, Tailwind 4, Vite, `motion` (already present). None added.

**Storage**: N/A

**Testing**: Vitest + Testing Library (fake timers for the presenter); Playwright e2e at laptop,
desktop and phone viewports; xUnit for the `lastHand` projection (written first)

**Target Platform**: modern evergreen browsers; laptop-height fit is the new primary target,
phone (>= 360 px) support is preserved

**Project Type**: web application (existing `web/` client + .NET server)

**Performance Goals**: pop-up visible within 1 s of the last trick settling (SC-006); no layout
shift when highlighting cards; presenter queue still compresses holds when long

**Constraints**: presentation-only on the client; no rule or legality change; no hidden-information
exposure (only already-played cards); EN/BG strings for every new label; keyboard operable and
focus-trapped modal; reduced motion honoured; no horizontal or vertical page scroll at 1366x768

**Scale/Scope**: ~6 client components changed/added, 1 presenter step pair, 1 server projection
addition (+ DTOs, mapper), ~6 test files touched, i18n EN/BG

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle / Constraint | Status | Notes |
|------------------------|--------|-------|
| I. Pure Rules Engine | Pass | `Belote.Core` is not modified. The hand summary is assembled in `Belote.Table` from `HandScored`/`CardPlayed`/`TrickWon` events that the engine already emits. |
| II. Server-Authoritative | Pass | No new intents. `lastHand` is data the server derives; it contains only cards already played face-up, so it leaks nothing. Client still enables input only from the server's `legalActions`. |
| III. Humans and Bots Interchangeable | Pass | Seat boxes and pop-up are built from the same per-seat data regardless of who controls a seat. |
| IV. Variants Are Configuration | Pass | Breakdown values (card points, bonuses, outcome, game points) come from the engine's `HandResult`; the UI hard-codes no rule value. |
| V. Test-First Game Logic | Pass | The only logic added (hand summary assembly) gets xUnit tests first, including "won cards of both teams total 32 and equal the cards played" and "breakdown sums to the raw totals". |
| VI. Mobile-First and Accessible | Pass | Layout is designed phone-first and scaled up; modal has dialog semantics, focus trap, Escape/Enter dismissal and screen-reader labels per card; seat bids are text, not colour; EN/BG strings. |
| Technology constraints | Pass | No technology outside the list. |
| Wire contract versioning | Pass (note) | Additive, nullable field; old clients ignore it, new client tolerates its absence. Protocol version unchanged; documented in `contracts/hand-summary.md`. |
| UI verification gate | Planned | Quickstart includes phone viewport, both languages and keyboard-only passes. |

Post-design re-check: unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/006-table-ui-polish/
├── plan.md              # This file
├── research.md          # Phase 0 decisions
├── data-model.md        # Hand summary, seat call history, new presenter steps
├── quickstart.md        # Validation guide
├── contracts/
│   ├── hand-summary.md      # `lastHand` field on the table view
│   └── presentation-steps.md # `bid` and `result` steps, gating rules
├── checklists/requirements.md
└── tasks.md             # Created by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── Belote.Table/
│   ├── TableSnapshot.cs          # + LastHand (summary of the most recently scored hand)
│   ├── LiveTable.cs              # builds the summary when an accepted move contains HandScored
│   └── Views/
│       ├── Dtos.cs               # + HandSummaryDto, TeamHandDto; SeatViewDto.LastHand
│       └── SeatViewMapper.cs     # maps summary -> DTO
└── (Belote.Core, Belote.Bots, Belote.Server: unchanged)

tests/ (existing .NET test projects)
└── Belote.Table.Tests/           # summary tests, written first

web/
├── src/
│   ├── protocol/types.ts         # + HandSummary, TableView.lastHand
│   ├── presentation/
│   │   ├── steps.ts              # + "bid" and "result" steps in diffViews
│   │   ├── usePresenter.ts       # blocking step + release()
│   │   └── PresenterProvider.tsx # exposes release
│   ├── theme/motion.ts           # + bidHoldMs
│   ├── components/
│   │   ├── Table.tsx             # one-screen layout, modal host
│   │   ├── Hand.tsx              # single aligned row, inert state
│   │   ├── Card.tsx              # HandCard "inert" state, height-driven sizing
│   │   ├── Seat.tsx              # compact box + call history
│   │   ├── ActionPanels.tsx      # compact bid / announce strips
│   │   ├── Chrome.tsx            # top bar; ResultPanel folded into the modal
│   │   ├── HandResultDialog.tsx  # new: results pop-up
│   │   └── labels.ts             # + callLabel
│   ├── i18n/{en,bg}.ts           # new strings
│   ├── App.tsx / index.css       # viewport-height shell and sizing tokens
└── tests/ , e2e/                 # updated and new tests (see quickstart)
```

**Structure Decision**: keep the existing `web/` client and `Belote.Table` server projection; no
new projects or layers. The modal is one new component; everything else edits existing files.

## Complexity Tracking

No constitution violations to justify.
