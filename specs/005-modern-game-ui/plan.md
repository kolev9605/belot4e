# Implementation Plan: Modern Game UI

**Branch**: `005-modern-game-ui` | **Date**: 2026-10-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/005-modern-game-ui/spec.md`

## Summary

Restyle and re-stage the existing React table (`web/`) as a dark, minimal game surface.
Three pieces of real work sit under the visuals:

1. **Hand order** — the server projection sorts `ownHand` by card strength within each suit for
   the active contract (rule data stays in `Belote.Core`, per Principle IV); the client groups
   those cards by suit in one fixed, alternating-colour suit order and never re-sorts on its own.
2. **Presentation pacing** — a client-side "presenter" sits between the store (latest server
   truth) and the table (what is drawn). It turns the stream of server views into a queue of
   timed presentation steps (deal, card played, trick held, trick collected) and shows them
   one at a time. Input is enabled only when the presented view *is* the latest view.
3. **Table staging** — seats around a felt surface, card-back fans for opponents, a dealer
   marker, team/turn emphasis and a deal that visibly originates at the dealer's seat, all
   driven by data the client already receives.

One small dependency (`motion`) is added for card movement; everything else is Tailwind (already
used) with CSS custom properties for the theme. No hub-protocol shape change.

## Technical Context

**Language/Version**: TypeScript 7 (strict) + React 19 on the client; C# / .NET 10 for the one
server-side ordering change

**Primary Dependencies**: existing React, Tailwind 4, Vite; **new: `motion` (^14, imported from
`motion/react`)** for enter/exit/layout card movement. No game engine, no canvas, no icon set.

**Storage**: N/A

**Testing**: Vitest + Testing Library (existing); fake timers for the presenter; Playwright e2e
(existing) at phone and desktop viewports; xUnit for the projection ordering

**Target Platform**: modern evergreen browsers; phone-first (≥ 360 px) through desktop

**Project Type**: web application (existing `web/` client + .NET server)

**Performance Goals**: 60 fps card motion using transform/opacity only; input available ≤ 0.5 s
after the table settles on the person's turn (SC-004); presenter lag never exceeds a few seconds
behind truth (it compresses its holds when the queue is long, see research R3)

**Constraints**: presentation only — no rule, legality or hidden-information change; reduced
motion honoured; EN/BG strings; keyboard operable; no horizontal scroll at 360 px

**Scale/Scope**: ~8 components restyled/new, 1 presenter module, 1 theme stylesheet, 1 sort
helper in the server mapper, ~4 new/updated test files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle / Constraint | Status | Notes |
|------------------------|--------|-------|
| I. Pure Rules Engine | Pass | `Belote.Core` untouched; it already exposes `CardStrength.Of`, which the mapper calls. |
| II. Server-Authoritative | Pass | Intents, legality and projection contents unchanged. The presenter only delays *drawing*; submission still checks the latest server `legalActions` and is blocked until the table has settled. |
| III. Humans and Bots Interchangeable | Pass | No branch on human vs bot; seats are rendered from the same view data. |
| IV. Variants Are Configuration | Pass (by design) | Card power is **not** hard-coded in the UI: the order comes from the server via the rule set. The client only owns a presentational suit-group order. |
| V. Test-First Game Logic | Pass | The only logic change (hand ordering) gets tests written first, incl. a property test over rule sets/contracts that the order is a permutation and strength-descending. |
| VI. Mobile-First and Accessible | Pass | Phone-first layout, contrast tokens, suit shape + text, rank/suit labels kept, reduced-motion path, EN/BG strings for every new label. |
| Technology constraints | **Justified exception** | `motion` is outside the listed stack. See Complexity Tracking. |
| UI verification gate | Planned | Phone viewport, both languages, keyboard navigation are explicit quickstart steps. |

Post-design re-check: unchanged; the data model adds no wire fields, only a presentation state
machine on the client.

## Project Structure

### Documentation (this feature)

```text
specs/005-modern-game-ui/
├── plan.md              # This file
├── research.md          # Phase 0 decisions
├── data-model.md        # Presentation state and hand-order model
├── quickstart.md        # Validation guide
├── contracts/
│   ├── hand-order.md        # Server-side ordering guarantee
│   └── presentation.md      # Presenter steps, timings, input gating
├── checklists/requirements.md
└── tasks.md             # Created by /speckit-tasks
```

### Source Code (repository root)

```text
src/Belote.Table/Views/
└── SeatViewMapper.cs                 # sort OwnHand by strength within suit (contract-aware)

tests/Belote.Table.Tests/Unit/
└── HandOrderTests.cs                 # NEW: ordering + property test

web/src/
├── theme/
│   ├── tokens.css                    # NEW: dark palette, accent, spacing, felt, card tokens
│   └── motion.ts                     # NEW: durations/eases, reduced-motion helpers
├── presentation/
│   ├── usePresenter.ts               # NEW: queue + timed steps over store views
│   ├── steps.ts                      # NEW: diff(prev,next) -> PresentationStep[]
│   └── PresenterProvider.tsx         # NEW: exposes presented view + settled flag
├── components/
│   ├── Table.tsx                     # layout: felt, 4 seats, centre trick, hand, actions
│   ├── Seat.tsx                      # avatar/role, team, turn ring, dealer chip, card fan
│   ├── CardFan.tsx                   # NEW: opponent card backs
│   ├── Card.tsx                      # dark card face, suit shape + text, back face
│   ├── Hand.tsx                      # grouped-by-suit fan, layout animation, playable state
│   ├── TrickArea.tsx                 # per-seat slots, fly-in, hold, sweep to winner
│   ├── DealerMarker.tsx              # NEW
│   ├── ActionPanels.tsx, Chrome.tsx  # restyled to tokens (behaviour unchanged)
│   └── labels.ts                     # (unchanged helpers)
├── index.css                         # remove "no animation" reset; import tokens
└── i18n/{en,bg}.ts                   # new labels (dealer, team, your team, dealing…)

web/tests/
├── presenter.test.ts                 # NEW: step diffing + pacing with fake timers
├── hand-order.test.tsx               # NEW: suit grouping and stability while playing
└── table.test.tsx, behaviour.test.tsx# updated for settled/instant-pacing helper
web/e2e/                              # extend responsive + full-game specs
```

**Structure Decision**: keep the existing single `web/` client and add two small folders
(`theme/`, `presentation/`) rather than new packages. The presenter is a separate module from
the store so server truth, rule enforcement and reconnect logic in `StoreProvider` are untouched.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| New front-end library `motion` (outside the Technology Constraints list) | Card flight between seat and table, exit-to-winner sweeps, and smooth hand re-flow need coordinated enter/exit/layout animation with reduced-motion support | Hand-rolled CSS transitions cannot animate elements that are unmounted (trick sweep) or reflow siblings (hand closing the gap after a play) without re-implementing a layout/presence system; ~100+ lines of fragile code versus one small, well-known dependency |
