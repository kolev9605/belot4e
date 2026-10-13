# Bug Assessment: Table and player boxes shift position as content changes

- **Slug**: ui-components-jumping
- **Created**: 2026-10-13 (re-assessed after switching to `main` at `c1fbc74`, which includes the merged 007 rooms/multiplayer work; the `web/` code is identical to the earlier 007 branch, so findings are unchanged)
- **Source**: pasted text
- **Verdict**: likely valid, needs reproduction
- **Severity**: medium

## Report (verbatim or summarized)

> "The table and the player boxes are not static - they are moving as the elements change - they shouldn't do that - they should remain still"

## Symptom

The felt (trick/bid area) and the four seat boxes (north/west/east/south) change size and/or position while the game progresses (calls appear, turn chip toggles, dealer marker, card counts, announcements, status text). Expected: the table and seat boxes keep a fixed position and size regardless of content.

## Reproduction

Static code analysis only; not reproduced in a browser.

1. Start a game (or join a room) and watch the table during bidding.
2. Observe seat boxes and felt as bids are added to a seat (call chips), the "Turn" chip appears/disappears, and the dealer marker/deal-origin stack shows during dealing.
3. Move into the playing phase: the Announcements panel appears/disappears, the status line text changes, and card counts shrink.
4. Observe the felt and seat boxes resizing/shifting.

[NEEDS CLARIFICATION: which phase(s) and viewport size show the jumping most (bidding, dealing, playing; desktop vs. phone)?]

## Suspected Code Paths

All layout is in `web/src/components/Table.tsx` and `Seat.tsx`; the page shell is `web/src/App.tsx:34` (`h-dvh ... overflow-hidden flex-col`).

- `Table.tsx:96` — main grid uses `grid-cols-[auto_minmax(0,1fr)_auto] grid-rows-[auto_minmax(0,1fr)]`. The `auto` tracks size to their content, so any change in a seat box's content resizes the north row / west & east columns, which resizes and moves the felt (`1fr`).
- `Seat.tsx:31-33` — seat box height/width are content-driven: call chips list (`calls.length > 0` conditionally rendered, `flex-wrap`), `Turn` chip (`toAct &&`), `DealerMarker` (`isDealer &&`), `deal-origin` stack (`dealing && isDealer`), `CardFan` (renders `count` cards, so the fan shrinks as cards are played), and the "N cards" label. Only `west`/`east` have a fixed width (`w-[5.5rem] sm:w-28`); north and south have none and no fixed/min height for any seat.
- `Table.tsx:114` — `AnnouncePanel` is rendered conditionally (and returns `null` when empty, `ActionPanels.tsx:55`), so it adds/removes a row between the felt and the south seat, changing the height available to the `1fr` grid row and therefore the felt size.
- `Table.tsx:115-120` — south seat and `status` text share a `flex-wrap` row; status strings differ in length (`status.yourTurn`, `status.waitingFor`, `label.dealing`, ...), so the row can wrap to 2 lines and change height; the south `SeatPanel` also gains/loses the Turn chip and calls.
- `Table.tsx:103-109` — felt switches between the `BidPanel` container and `TrickArea` (different internal content/heights), and `TrickArea.tsx:44-47` adds a `min-h-5` winner line and a `col-span-3` placeholder.
- `Chrome.tsx:33` — `ScoreBoard` is `flex-wrap` and its contract text varies in length (`heldOver`, contract kind/doubled/redoubled), so it can wrap and change height, pushing everything below.
- `Table.tsx:147` — `NoticeBar` appears above the body when a notice is set, pushing the whole table down.

## Root Cause Hypothesis

The table is laid out with content-sized (`auto`) grid tracks and unconstrained seat boxes, with several conditionally rendered or variable-length children (chips, call lists, fans, announcement row, wrapping status/score text). Whenever content changes, intrinsic sizes change and the `1fr` felt and neighbouring boxes reflow. Nothing reserves space for the maximum content. Confidence: high that this is the mechanism; medium on which specific element is most visible to the reporter until reproduced.

## Proposed Remediation

**Preferred**: Make geometry independent of content by reserving space for the worst case.
- Give seat boxes a fixed size per role (fixed width for north/south as well; fixed or `min-h` height sized for name + chips row + one calls row + fan + count) and let call chips scroll/clip/truncate within it rather than grow. Always render the Turn/dealer slots (e.g. `visibility: hidden` / `opacity` toggle or a fixed-height chip row) instead of conditionally mounting them.
- Replace `auto` grid tracks with fixed tracks (`west`/`east` column width equal to the fixed seat width; north row height equal to the fixed seat height), so the felt size is constant.
- Keep the Announcements area, status line, notice bar, and score bar at reserved heights (e.g. `min-h` + `truncate`/single-line, or always mount a fixed-height placeholder) so the vertical budget of the felt never changes.
- Keep the CardFan container at the full-hand size (reserve space for 8 cards) so shrinking counts don't resize it.

**Alternatives**:
- Position seats absolutely over a fixed-aspect-ratio table container (`aspect-ratio` + absolute anchors). Strongest guarantee, but a bigger rewrite and trickier on small viewports and with the existing trick-travel geometry (`theme/geometry.ts`).
- Only fix the worst offender (e.g. the announcements row and seat chips). Cheaper, but the report suggests general instability.

**Files likely to change**:
- `web/src/components/Table.tsx`
- `web/src/components/Seat.tsx`
- `web/src/components/CardFan.tsx`
- `web/src/components/ActionPanels.tsx` (AnnouncePanel reserved space)
- `web/src/components/Chrome.tsx` (ScoreBoard / NoticeBar reserved height)
- `web/src/components/TrickArea.tsx`
- `web/src/index.css`

**Tests to add or update**:
- A Vitest/RTL check that seat boxes render the same structural/size classes regardless of calls, turn, dealer, and card count (jsdom has no layout, so assert on fixed-size class/slot presence).
- An opt-in Playwright check (per project policy, e2e is not run in CI) that records `getBoundingClientRect()` of the felt and each `seat-*` across bidding, dealing, and playing and asserts they are unchanged at desktop and mobile widths.
- Update existing tests that rely on conditional rendering of the Turn chip or the Announcements section.

## Risks & Considerations

- Fixed sizes must still fit small phones inside the `h-dvh overflow-hidden` shell (`App.tsx:34`); reserved space reduces the room for the hand. Verify at 375×667-class viewports.
- Long player names (rooms/multiplayer) and the i18n strings (bg/en) must truncate or wrap within the fixed box, not grow it.
- Trick-card travel animation uses fixed `SEAT_SCALE`/`SLOT_SCALE` distances (`TrickArea.tsx:15`); a stable layout should keep these values valid but they need a visual check.
- Existing tests (e.g. `data-testid="deal-origin"`, `seat-calls-*`) must keep passing; don't remove test ids when always-mounting slots.
- Reserving space for 8-card fans and call lists may make seats larger than today; balance against the one-screen layout from feature 006.

## Open Questions

- [NEEDS CLARIFICATION: Does "the table" mean only the felt/trick area, or the whole screen including the score bar and hand? The assessment treats all as in scope.]
- [NEEDS CLARIFICATION: Which viewport/device and which phase show the worst jumping?]
- [NEEDS CLARIFICATION: Is clipping or truncating long call lists acceptable inside a fixed-size seat box?]
