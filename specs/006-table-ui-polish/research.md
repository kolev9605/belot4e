# Research: Table UI Polish

Findings come from reading the current client (`web/src`) and the table projection
(`src/Belote.Table`). There were no open `NEEDS CLARIFICATION` items.

## R1 - Why the page does not fit a laptop screen

- **Finding**: `App.tsx` renders a `min-h-screen` column; `Table.tsx` stacks scoreboard, a 3-row
  seat/trick grid (trick area has `min-h-[16.5rem]` on `sm`), a status line, the full bidding or
  announcement panel, the south seat box and finally the hand. Roughly 900-1000 px tall on desktop.
- **Decision**: make the shell a `h-dvh` flex column: top bar (title, language, connection, score,
  contract in one row) -> table area (`flex-1 min-h-0`, felt with seats and centre) -> hand strip.
  Bidding controls render inside the felt centre while bidding (the trick area is empty then);
  announcement controls become a one-line strip above the hand. Card dimensions come from CSS
  variables derived from `dvh` with a clamped min/max, so the hand scales with the screen.
- **Alternatives**: shrinking everything with `transform: scale` (blurry, breaks hit areas);
  keeping the stacked layout and just reducing padding (cannot reach 768 px).

## R2 - Why cards are misaligned and unevenly spaced

- **Finding**: three causes in `Hand.tsx` / `Card.tsx`: (a) each suit is its own flex item in a
  `flex-wrap` list with `gap-x-4`, so groups wrap and gaps differ from in-group overlap
  (`-space-x-3`); (b) a group containing trumps has an extra `chip` above its cards, making that
  group taller so its cards sit lower than others; (c) playable cards get `-translate-y-0.5`
  and hover `-translate-y-1`, so legal cards float above illegal ones.
- **Decision**: one non-wrapping row (`flex-nowrap`, `items-end`); constant overlap across and
  within suit groups with a slightly larger, constant gap only at suit boundaries *as a fixed token*
  (so spacing is uniform and predictable, not content-dependent); trumps indicated on the
  cards themselves (small badge inside the card edge / group underline) with no change to height;
  playable shown by ring + bottom indicator, no translate. Only the pointed-at card lifts.
  Overlap is computed from available width so a full hand never wraps.
- **Alternatives**: CSS grid with equal columns (no overlap needed but cards too small on phone);
  removing suit grouping (spec 005 requires grouping).

## R3 - Cards shown as illegal while bidding

- **Finding**: `HandCard` derives `playable` from "a `play` legal action exists for this card".
  During bidding there are none, so every card renders as not playable (dimmed, struck line,
  "not playable" label).
- **Decision**: pass the phase to the hand; outside `playing` (or when the table is not
  waiting on the person) cards use an "inert" style: normal face, no playability semantics,
  non-focusable, accessible name is just the card name. In `playing`, behaviour is unchanged
  except the layout rules from R2.
- **Alternatives**: hiding the hand during bidding (the person needs it to bid).

## R4 - Pacing of bot bids

- **Finding**: server `BotDelay` is 600 ms per bot move for every kind of move; the client
  presenter has steps for deal/play/hold/collect but nothing for calls, so a bid appears and
  is superseded almost immediately, and the bid list in a block below the table is easy to miss.
- **Decision**: add a presenter step `bid` (~1100 ms) emitted whenever `biddingHistory` grows, so
  presented views advance one call at a time; the person's own buttons only enable once the
  presenter is settled (existing gating), which also means the person sees the preceding bot calls
  before being asked. Hold times compress when many steps queue (existing `compressionScale`).
  Value lives in `PacingConfig.bidHoldMs`, so instant mode (tests) removes it.
- **Alternatives**: raising the server delay for bids (couples pacing to the server and affects
  non-UI clients and tests); a fixed client `setTimeout` outside the presenter (would not respect
  the queue or reduced-motion).

## R5 - What a seat box shows for bids

- **Finding**: `view.biddingHistory` already holds every call in order with seat; it persists
  after bidding closes and empties on a new deal (the client already detects a voided hand from
  that).
- **Decision**: derive per-seat call lists on the client; show them as compact chips in the
  seat box (e.g. `Pass`, `Hearts`, `Pass`), the latest one emphasised; clear on new deal. No wire
  change. Chips carry text, never colour alone.

## R6 - Where the end-of-hand data comes from

- **Finding**: the view exposes `completedTricks` for the current deal only and `matchScore` as a
  running total; when the last trick of a hand is played, the very next view already belongs to the
  next deal (see `diffViews`, "newDeal"), and `HandResult` (card points, last-trick bonus,
  announcements, belote, all-tricks bonus, outcome, game points, held-over) lives only in the event
  log (`HandScored`), never in the projection.
- **Decision**: build a `HandSummary` in `Belote.Table` when an accepted move contains `HandScored`
  (the table already keeps the full event list and `before.Hand.CurrentPlays` for the last trick),
  store it in `TableSnapshot.LastHand`, and send it as `lastHand` on every subsequent view until
  replaced. It holds only played cards (public once played) and values the engine already computed.
- **Alternatives**: client reconstruction from `completedTricks` (impossible at deal boundary,
  and would duplicate scoring rules in the UI, violating Principle IV); a new hub event (more
  surface area for the same data; would need ordering against view updates).

## R7 - Showing the pop-up at the right moment

- **Finding**: the presenter already serialises "what the table draws"; showing a modal from
  the latest store view would pop it up before the last trick has been seen.
- **Decision**: add a blocking presenter step `result`, emitted by `diffViews` right after the
  last trick's steps whenever `next.lastHand.dealNumber` differs from `prev.lastHand?.dealNumber`.
  The step has no timer; the presenter resumes when the dialog calls `release()`. This also stops
  the next deal/bids from running under the modal. Never emitted when `prev` is null (page load or
  reconnect), so reloading does not re-open old results.
- **Alternatives**: server-side pause (only the table creator can pause; adds a round trip and
  protocol surface); showing the dialog from store state (wrong timing, bids hidden behind it).

## R8 - Modal behaviour and accessibility

- **Decision**: native `<dialog>` opened with `showModal()` (focus trap, Escape, inert background
  for free), labelled by its heading, initial focus on the primary button ("Continue" or "Play
  again"). Team sections are labelled regions; each won-card row uses the existing `TableCard`
  with rank/suit accessible names. Row wraps within the dialog and the dialog height is capped
  with the row scaling (no page scroll). Reduced motion: no entrance animation.
- **Alternatives**: custom overlay div (needs hand-rolled focus management); a library dialog
  (new dependency, not justified).

## R9 - Compatibility with existing tests

- Existing e2e `playToEnd` waits for `data-testid="result"`; the modal keeps that test id on the
  final-game pop-up. A new helper dismisses the per-hand pop-up so automated play continues.
  Instant pacing (`?pace=instant`) still makes the `result` step blocking, since it is user-driven.
