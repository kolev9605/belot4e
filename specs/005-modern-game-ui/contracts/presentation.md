# Contract: Presentation pacing and input gating (client)

## Inputs and outputs

- **Input**: the stream of `TableView` values the store already exposes (`state.game`), plus
  connection/session status.
- **Output**: `{ view, settled, stage }` consumed by `Table` and its children.
- The presenter has no network access and never calls `submit`.

## Behaviour

| # | Rule |
|---|------|
| P1 | Every distinct view is presented in arrival order; none is skipped except when superseded while still queued by a view of the same step kind that adds no new visual (e.g. repeated identical snapshot after a resync). |
| P2 | `settled` is true only when the drawn view is the store's latest view and no step is running. |
| P3 | Card taps and bid/announce buttons are enabled iff `settled && canAct(state)`. |
| P4 | Within 500 ms of becoming `settled` on the person's turn, controls are interactive. |
| P5 | A completed trick is on screen for at least the hold duration (≈ 1.4 s) before `collect` starts. |
| P6 | Disconnect, `lost` session, new game or table close clears the queue and shows the latest state immediately. |
| P7 | With reduced motion, positions are not animated (fade only), but hold and deal durations are kept. |
| P8 | Queue compression: with > 3 waiting steps, hold/deal durations are scaled by `max(0.4, 3 / queued)`. |

## Defaults (single source: `theme/motion.ts`)

| Name | Value |
|------|-------|
| `dealStaggerMs` | 60 |
| `dealTravelMs` | 350 |
| `playTravelMs` | 350 |
| `trickHoldMs` | 1400 |
| `collectMs` | 450 |
| `snapMs` | 150 |

## Accessibility

- The live region still announces the *latest* state change immediately (not after the animation),
  so screen-reader users are never behind.
- Moving elements carry no focus; focus stays on the hand/actions and is not reset by steps.
- Hand cards keep their button semantics, rank/suit name and playable/not-playable wording.
