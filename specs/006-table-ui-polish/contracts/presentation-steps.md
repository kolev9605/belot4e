# Contract: presentation steps `bid` and `result`

Extends `specs/005-modern-game-ui/contracts/presentation.md`. All other steps (`deal`, `play`,
`hold`, `collect`, `snap`, `settle`) are unchanged.

## `bid`

- **Emitted by** `diffViews(prev, next)` when `next.biddingHistory.length > prev.biddingHistory.length`
  (one step per new entry, in order), before any other steps the same diff produces.
- **Frame**: the view as of that call (`view` = next, `trick` empty, `dealing` false).
- **Duration**: `PacingConfig.bidHoldMs` (default 1100 ms; 0 in instant mode). Subject to
  `compressionScale` when more than three steps are queued.
- **Input**: not enabled while a `bid` step is running (existing rule: input only when the presenter
  is settled), so the person always sees bot calls before being asked to act.
- **Not applied** to the person's own call: a step for it still exists, but when the previous
  frame's `toAct` was the person's seat its duration is 0, so the person's own action is never
  delayed.

## `result`

- **Emitted by** `diffViews(prev, next)` when `prev` is non-null and
  `next.lastHand` is non-null with `next.lastHand.dealNumber !== prev.lastHand?.dealNumber`.
  It comes after the last trick's `play`/`hold`/`collect` steps and before `deal` (or before the
  end of the game).
- **Frame**: carries `result: HandSummary`; the view drawn behind the dialog is the final view
  of the finished hand (empty hands, trick collected).
- **Duration**: none - the step is *blocking*. The presenter does not start the next step until
  `release()` is called by the dialog (dismissal), or the presenter is cleared (connection lost).
- **Not emitted** when `prev` is null (first view after load/reconnect) or when `lastHand` did not
  change.
- **Settled**: while a `result` step is current, `settled` is false, so no intents are accepted.

## Dialog behaviour (UI contract)

- Opens when the current frame has `result`; closes on dismiss (button, Escape, or Enter on the
  primary button); dismissing calls `release()`.
- If `lastHand.winner` is set, the primary action is "Play again" (starts a new game) and the
  winning team is stated; otherwise it is "Continue".
- Content per team: label (marked as "your team" for the person's team), won cards as one row,
  breakdown lines (card points, last trick, announcements, belote, all tricks, contract
  made/failed), hand game-points, and the running match total.
