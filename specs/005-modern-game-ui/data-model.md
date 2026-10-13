# Data Model: Modern Game UI

No persistent data and no new wire fields. This describes the client-side presentation state
and the ordering guarantee on an existing field.

## Hand order

| Item | Definition |
|------|------------|
| Input | `TableView.ownHand: string[]` (e.g. `"10H"`) |
| Server guarantee | For each suit, cards appear strongest → weakest under the active contract (`contract.kind`, or no-trump when none). Suits may be interleaved. |
| Client grouping | Stable partition by suit in the order `S, H, C, D`; within a suit the server order is kept. |
| Trump tag | A group is tagged "trumps" when `contract.kind` is that suit, or all groups when all-trump. Display only. |
| Stability | Removing a played card never reorders the others; dealing a new hand re-sorts once. |

## Presenter

### `PresentedState`

| Field | Meaning |
|-------|---------|
| `view` | The `TableView` currently drawn (may be older than the store's latest). |
| `latest` | The store's latest `TableView`. |
| `settled` | `true` when the queue is empty, no step is running and `view === latest`. |
| `stage` | Which step is on screen: `idle`, `dealing`, `playing(seat, card)`, `holding(winner)`, `collecting(winner)`. |
| `dealerOrigin` | Seat the current deal started from (the view's `dealer`). |

### `PresentationStep` (derived by diffing `prev → next` views)

| Step | Trigger | Visual | Min duration |
|------|---------|--------|--------------|
| `deal` | no previous view, or `dealNumber`/`dealer` changed | cards leave dealer seat to each seat, staggered; own hand revealed on arrival | ≈ 2.0 s |
| `play` | one more card in the trick (or the fourth card of a completed trick) | card travels seat → its table slot | ≈ 0.35 s |
| `hold` | trick complete | all four cards visible, winner emphasised | ≈ 1.4 s |
| `collect` | after `hold` | four cards sweep toward the winner's seat | ≈ 0.45 s |
| `snap` | anything else (bidding entry, announcement, phase change, game over) | immediate state swap with a short fade | ≈ 0.15 s |

Step order for a completed trick: `play (4th card) → hold → collect`.

### Rules

1. Steps run strictly in order; the queue is never reordered or dropped, only compressed
   (holds ×0.4–1.0) when more than three steps are waiting.
2. A `view` older than the latest is never exposed to input handlers.
3. A connection loss, `lost` session or new game clears the queue and snaps to the latest view.
4. `finished` phase snaps immediately after the last `collect`.

## Seat geometry (presentation constants)

| Seat | Position | Card fan | Role label (EN) |
|------|----------|----------|-----------------|
| south | bottom | own hand (face up) | You |
| north | top | horizontal backs | Partner |
| west | left | vertical backs | Opponent |
| east | right | vertical backs | Opponent |

Team of a seat: `northSouth` for north/south, `eastWest` for east/west; the person's team is
`northSouth`.

## Dealer marker

Exactly one seat per view (`view.dealer`). Rendered as a chip with text "D"/"Dealer" (and a
small card-stack glyph), never colour-only; moves when `view.dealer` changes.
