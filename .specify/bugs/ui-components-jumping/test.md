# Bug Verification: Table and player boxes shift position as content changes

- **Slug**: ui-components-jumping
- **Tested**: 2026-10-13
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: partial

## Summary

The fix compiles, type-checks and passes the full unit/component suite including the new regression test, and the new sizing classes are emitted in the production CSS. The original symptom (visual movement of the felt and seat boxes) was not exercised in a real browser, because jsdom has no layout engine and the Playwright e2e suite is opt-in and was not run, so the result is `partial`, not `verified`.

## Checks Performed

| Check | Command / Action | Result | Notes |
|-------|------------------|--------|-------|
| Reproduction (post-fix) | Measure `getBoundingClientRect()` of felt and `seat-*` across bidding/dealing/playing at 1366×650, 1280×560, 375-wide | not-run | Needs a browser plus the backend; e2e is opt-in and was skipped. No layout engine in Vitest. |
| New / updated tests | `npx vitest run tests/table-seats.test.tsx` (inside the full run) | pass | "gives every seat box a fixed size that does not depend on its content" passes; it asserts class-level sizing only, not pixels. |
| Regression suite | `npx vitest run` (web) | pass | 17 files, 165 tests passed. |
| Type-check | `npx tsc --noEmit -p .` | pass | exit 0, no output. |
| Production build | `npm run build` (tsc + vite build) | pass | 492 modules, built successfully. |
| Generated CSS | grep of `dist/assets/*.css` for new utilities | pass | `justify-center-safe`, `min-h-[3.2rem]`, `min-h-8`, `w-64`, `[direction:rtl]` and `h-[calc(var(--card-h)*0.5)]` (→ `height:calc(var(--card-h) * .5)`) are all emitted. |
| Playwright e2e | `npm run e2e` | skipped | Opt-in, slow, never in CI (project practice); not run without consent. |
| Backend tests | `dotnet test` | not-run | Change is front-end only; no server files touched. |

## Output Excerpts

```
Test Files  17 passed (17)
     Tests  165 passed (165)
tsc exit code: 0
✓ built in 197ms
```

## Residual Risks

- **Not visually verified.** Whether the seats and felt stay still in a real browser, and how `justify-center-safe` + `[direction:rtl]` (clip-older-calls) renders, is unconfirmed.
- **Vertical budget.** The reserved north/south calls row and the always-present `min-h-8` announcements slot add height permanently; on short viewports (e.g. 1280×560) the felt may be squeezed or the seats clipped (`overflow-hidden`). The existing e2e "fits without scrolling" checks use `scrollHeight` on an `overflow-hidden` shell and may not detect clipped seats.
- **Fixed seat widths** (`w-64`, `w-[5.5rem]/sm:w-28`) with long player names or longer Bulgarian strings may truncate/clip content; untested.
- **Side seats** stretch to the felt row; with 3+ chips plus a wrapped call list and an 8-card fan, the fan slot may be clipped on small heights.
- Older calls on north/south are clipped by design.

## Recommendation

Hold. Unit-level checks are green, but the bug is a visual one and has not been reproduced or disproved in a browser. Before closing, run the opt-in suite (`npm run e2e`, in particular `table-polish.spec.ts`) or eyeball the app at 1366×650, 1280×560 and a ~375px phone through bidding, dealing and play; ideally add a Playwright spec that asserts the felt and seat rectangles are identical across phases. If the felt is squeezed on short viewports, trim the north/south calls row or the announcements slot and re-test.
