# Bug Fix: Table and player boxes shift position as content changes

- **Slug**: ui-components-jumping
- **Fixed**: 2026-10-13
- **Assessment**: ./assessment.md
- **Status**: applied (not visually verified in a browser, see Follow-ups)

## Summary

Seat boxes, the score bar, the announcements strip, the status line and the notice bar no longer size themselves from their content, so the felt and the four seat boxes keep one size and position through bidding, dealing and play.

## Changes

| File | Change | Notes |
|------|--------|-------|
| `web/src/components/Seat.tsx` | modified | North/south seats have a fixed width (`w-64 max-w-full`) with reserved rows: single-line header (name truncates), a fixed-height chips row, a fixed-height calls row and a fixed-height fan slot (`0.5 × card height`). West/east seats fill the felt row height (`h-full`, fixed width) with the fan in a `flex-1` slot. Every seat is `overflow-hidden`. Calls on north/south are one clipped line that keeps the newest call visible. The dealer's deal-origin stack is absolutely positioned so it no longer adds height during dealing. |
| `web/src/components/Table.tsx` | modified | West/east wrappers stretch instead of `self-center`; the announcements strip sits in an always-present `min-h-8` slot (both phases); the south row is a fixed-width seat plus a fixed-size status block (stacked on phones, inline from `sm`); `NoticeBar` floats over the table instead of pushing it down. |
| `web/src/components/Chrome.tsx` | modified | `ScoreBoard` is a single line; the deal/held-over/contract text truncates instead of wrapping. |
| `web/tests/table-seats.test.tsx` | added test | Pins that seat boxes keep the same sizing classes across different dealer, turn, hand-size and call states. |

## Diff Highlights (optional)

```tsx
// Table.tsx: side seats now fill the row instead of centring at their content height
<div className="flex min-h-0"><SeatPanel seat="west" .../></div>

// Table.tsx: announcements slot is always present
<div className="min-h-8">{game.phase !== "bidding" && <AnnouncePanel .../>}</div>
```

## Tests Added or Updated

- `web/tests/table-seats.test.tsx` — "gives every seat box a fixed size that does not depend on its content": compares each seat's sizing classes before/after a change of dealer, turn, hand sizes and calls.

## Local Verification

- `npx tsc --noEmit -p .` (web) → clean.
- `npx vitest run` (web) → 17 files, 164 tests passed (before adding the new test); `tests/table-seats.test.tsx` → 11 passed with the new test.
- Playwright e2e not run (opt-in, per project practice).
- jsdom has no layout engine, so actual pixel stability was not measured.

## Deviations from Assessment

- The `Turn` chip and dealer marker remain conditionally mounted (existing tests assert their absence, e.g. one dealer marker, "to act" absent on other seats). Their space is reserved with fixed-height rows instead of always mounting them.
- Open questions in the assessment were not answered by the user; the fix proceeds on the stated assumptions: all of felt, seats, score bar and notice are in scope, and clipping older calls inside a fixed seat box is acceptable.
- `CardFan.tsx`, `ActionPanels.tsx`, `TrickArea.tsx` and `index.css` were not changed; the reserved fan slot lives in `Seat.tsx`.

## Follow-ups

- Run `/speckit-bug-test slug=ui-components-jumping` and check visually (or with the opt-in Playwright `table-polish` spec) at 1366×650, 1280×560 and a 375-wide phone. The reserved rows (north/south calls row, announcements slot) add roughly 2–4rem of permanent height, which could squeeze the felt on very short viewports; if so, trim the calls row for north/south or the announcements slot.
- Older calls are clipped on north/south; if the full bidding history matters, expose it in a tooltip or the bid panel.
- Consider a Playwright check that records `getBoundingClientRect()` of the felt and `seat-*` across phases.
