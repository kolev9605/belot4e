# Implementation Plan: Playable Table UI

**Branch**: `003-playable-table-ui` | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-playable-table-ui/spec.md`

## Summary

Add a bare-bones React + Tailwind web client (`web/`) that plays one human (South) against three
bots through the existing feature 002 SignalR hub. The client is a thin renderer: it shows the
server's per-seat view, offers only the server's `legalActions`, and contains no rules. A
connection manager provides reliable connect, automatic retry with backoff, ordered-update
handling with resync via `GetView`, and a clear status. Three small, additive server changes are
required: (1) a displaced or disconnected player's unfinished game no longer blocks a new one
(FR-023), with a `TableClosed` push to a displaced connection; (2) the seat view carries public
per-seat hand sizes (FR-003); (3) the server can serve the built client so the page and hub are
same-origin.

## Technical Context

**Language/Version**: TypeScript 5.x (strict) on Node.js 22+ (26.5 installed locally) for the
client; C# / .NET 10 for the existing server (unchanged)

**Primary Dependencies**: React 19, Vite, Tailwind CSS 4, `@microsoft/signalr`; dev: Vitest,
React Testing Library, Playwright

**Storage**: N/A. In-memory server table (002); client keeps only the chosen language in
`localStorage` (optional, failure-tolerant)

**Testing**: Vitest + Testing Library (reducer, connection manager with a fake hub, component
rendering, i18n completeness); xUnit for server changes (`Belote.Server.Tests`,
`Belote.Table.Tests`); Playwright end-to-end smoke against the real server (full game,
server-restart recovery, phone viewport)

**Target Platform**: Evergreen browsers, phone width 375 px up; server on Linux/Windows via
Kestrel

**Project Type**: Web application: existing .NET server + new `web/` front-end

**Performance Goals**: connected and ready within 3 s on a local server (SC-004); reconnect
recovery within 10 s of server availability (SC-005); a view applies and re-renders instantly
(hand is at most 8 cards)

**Constraints**: no animations/menus/sound; no rule logic in client; no authentication; all
strings localized (en, bg); keyboard operable; no horizontal scroll at 375 px

**Scale/Scope**: one table, one player; one screen, ~10 components, ~40 localized strings

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle / Constraint | Status | Notes |
|------------------------|--------|-------|
| I. Pure Rules Engine | Pass | No rules added. The one `Belote.Core` touch (public `HandSizes` on `SeatView`) adds no dependency, I/O, clock or randomness. |
| II. Server-Authoritative | Pass | Client sends intents only and renders the projection. `handSizes` is public (counts, not cards). A leak-review test covers the new wire content. |
| III. Humans and Bots Interchangeable | Pass | Untouched; replace-on-create keys on connection identity, never human vs bot. |
| IV. Variants Are Configuration | Pass | Client hard-codes no rule values; contracts, announcements and legality come from the server. |
| V. Test-First Game Logic | Pass | No game logic changed; server changes and the client state machine are test-first. |
| VI. Mobile-First and Accessible | Pass | Phone-first layout, bg/en from first release, keyboard operation, text labels for every card and state change. Verified at phone viewport in both languages and by keyboard. |
| Tech: React + TypeScript strict | Pass | |
| Tech: typed, versioned real-time contracts | Pass | Wire types mirrored in `web/src/protocol/`; `protocolVersion` checked on every update; additive changes in [contracts/hub-client.md](contracts/hub-client.md). |
| Tech: technology outside the list | Justified | Tailwind (user-requested), Vite/Vitest/Playwright toolchain. See Complexity Tracking. |

**Post-design re-check**: unchanged; the design adds no violations.

## Project Structure

### Documentation (this feature)

```text
specs/003-playable-table-ui/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── hub-client.md      # client use of the hub + additive server changes
│   └── client-state.md    # connection and game state machine
├── checklists/requirements.md
└── tasks.md               # /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── Belote.Core/State/                # SeatView gains HandSizes (public counts)
├── Belote.Table/Views/               # wire view: handSizes; TableClosed DTO
└── Belote.Server/
    ├── TableRegistry.cs              # replace unfinished table on create from another connection; notify displaced
    ├── ServerHost.cs                 # optional static-file hosting of web/dist
    └── TableHub.cs                   # method surface unchanged

tests/
├── Belote.Table.Tests/               # handSizes correctness + no-leak tests
└── Belote.Server.Tests/EndToEnd/     # replace-on-create, TableClosed, same-connection still tableExists

web/
├── package.json  tsconfig.json  vite.config.ts  index.html
├── src/
│   ├── main.tsx  App.tsx  index.css
│   ├── protocol/        # wire types, protocolVersion guard
│   ├── connection/      # hub connection manager, backoff, resync
│   ├── state/           # reducer + selectors (pure, no rules)
│   ├── i18n/            # en.ts, bg.ts, card/seat/contract/rejection labels
│   └── components/      # Table, Seat, Hand, Card, TrickArea, BidPanel, AnnouncePanel,
│                        # ScoreBoard, ConnectionBanner, ResultPanel, Notice
├── tests/               # Vitest unit + component tests
└── e2e/                 # Playwright specs
```

**Structure Decision**: web application layout: the existing .NET projects are the backend and
a single new `web/` package is the front-end. The built client is served by `Belote.Server`
in production and proxied by Vite in development, so the browser always talks to one origin
(no CORS configuration).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Tailwind CSS not in constitution's technology list | Explicitly requested for styling | Plain CSS contradicts the request |
| Vite, Vitest, Testing Library, Playwright toolchain | A React + TS client needs a bundler and test runner; Playwright is the only way to verify connection recovery and the phone-viewport gate end to end | CRA is unmaintained; skipping e2e leaves SC-005 and the viewport gate unverified |
| Touches `Belote.Core` (`SeatView.HandSizes`) | FR-003 needs other seats' card counts | Client-side derivation breaks FR-008 (no rules in client) and is wrong across the 5-then-3 deal |
