# Research: Modern Game UI

## R1. Where does the hand order come from?

- **Decision**: The server's `SeatViewMapper` orders `OwnHand` by strength (strongest first)
  within each suit using `CardStrength.Of(view.RuleSet, kind, card.Suit, card)`, where `kind` is
  the contract in force, or `NoTrump` while none is decided. The client groups by suit in a fixed
  presentational order and keeps each group's incoming order.
- **Rationale**: Principle IV forbids hard-coding rank power in UI code, and the wire carries no
  rule data. Sorting the already-sent array exposes no new information and needs no new field or
  protocol version; clients that ignore order are unaffected.
- **Alternatives**: (a) hard-code rank orders in the client — violates IV and breaks for new
  variants; (b) add a `cardOrder` rule field to the wire — larger contract change for no gain;
  (c) sort client-side from a copied constant — same as (a).

## R2. Suit-group order

- **Decision**: Fixed order ♠ ♥ ♣ ♦ (alternating black/red) every deal, with the trump suit
  *not* moved, so positions never jump between deals. A trump suit is signalled by a small
  "trumps" tag on its group, not by reordering.
- **Rationale**: Alternating colours make adjacent groups easy to tell apart; a fixed order
  builds muscle memory (FR-002, FR-003).
- **Alternatives**: trump-first ordering (jumps once per deal, surprising); sorting by suit
  strength (not meaningful in Belote).

## R3. Pacing: where to slow things down

- **Decision**: Client-side presenter between store and table. The server's `BotDelay` (600 ms)
  and protocol stay as they are. The presenter diffs consecutive server views into steps and
  plays each with minimum durations. Starting values: deal ≈ 2.0 s total (stagger ≈ 60 ms per
  card), card travel ≈ 350 ms, completed-trick hold ≈ 1.4 s, sweep ≈ 450 ms. When more than
  three steps are queued, holds shrink proportionally (floor: 40 %) so lag stays bounded (SC-004).
- **Rationale**: Raising the server delay would slow the *person's* turns too and cannot hold a
  finished trick on screen (the next bot move would still replace it). The presenter can hold the
  full trick, also when moves arrive back-to-back after a tab regains focus.
- **Alternatives**: longer `BotDelayMs` (affects all turns, no trick hold); server-emitted
  "trick complete" pause event (protocol change, FR-026).

## R4. Input gating while presenting

- **Decision**: The table gets `{ view, settled }`. Controls and card taps are enabled only when
  `settled` (queue empty and the presented view equals the latest view) *and* the store's own
  `canAct` is true. Submission still uses the store's latest `legalActions`.
- **Rationale**: Prevents acting on a stale-looking table and keeps Principle II intact: truth is
  always the store's latest view; the presenter is a pure display delay.
- **Alternatives**: allow input mid-animation (confusing, may click a card that is about to be
  swept); drop animations for the human's own turn (inconsistent).

## R5. How the trick is shown

- **Decision**: The latest update for a completed trick carries an empty `currentTrick` and a
  `lastCompletedTrick`. The step diff converts it into: (1) play of the fourth card into the
  seat's slot, (2) hold with a winner highlight, (3) sweep toward the winner. The next trick's
  first card is a separate step, so it can never overlap the sweep.
- **Rationale**: Reuses only existing view fields (`currentTrick`, `lastCompletedTrick`,
  `completedTricks.length`) to detect trick completion even when intermediate updates were
  coalesced into one view.

## R6. Deal detection and dealer origin

- **Decision**: A deal step is emitted when there is no previous view, when `dealNumber` changes,
  or when the dealer changes. Cards travel from the dealer's seat position (computed from the
  fixed seat geometry) to each seat; the person's own cards are revealed face-up on arrival.
  Opponent card fans fill with backs in stagger order. A voided re-deal uses the same step and
  shows the existing "voided" notice first.
- **Rationale**: Everything needed (`dealer`, `dealNumber`, `handSizes`) is already in the view.

## R7. Animation library

- **Decision**: `motion` (React package imported from `motion/react`), using `AnimatePresence`,
  `layout`, and `useReducedMotion`. Use the slim `LazyMotion`/`m` components to keep the bundle
  small.
- **Rationale**: Handles presence (cards leaving the DOM), reflow of the hand, and reduced
  motion in one place; widely used and small when lazy-loaded.
- **Alternatives**: CSS-only transitions (cannot animate removal/reflow cleanly); React Spring
  (heavier API for this need); GSAP (imperative, not React-idiomatic, licence considerations).

## R8. Theme

- **Decision**: CSS custom properties in `theme/tokens.css`, surfaced to Tailwind via
  `@theme`. Palette: near-black blue-grey background, a deep-green felt surface with subtle
  radial vignette, one accent (warm amber) for turn/dealer/primary actions, neutral card faces
  (off-white) so cards read well on dark. Suits use shape glyph + rank text; red suits use a
  contrast-checked red on card faces. Contrast target: WCAG AA (4.5:1 text, 3:1 UI).
- **Rationale**: Dark, minimal, single accent per the brief; tokens keep the "one palette"
  requirement checkable.
- **Alternatives**: image-based felt/textures (heavier, less crisp, harder to theme).

## R9. Reduced motion

- **Decision**: `useReducedMotion()` switches card travel to opacity cross-fades with the same
  hold durations; the deal stagger is kept but cards appear without travel.
- **Rationale**: Keeps pacing information (SC-007) without movement.

## R10. Test strategy for time

- **Decision**: Presenter timings are injected through a `PacingConfig` (default real values;
  tests pass zero-duration or fake timers). Existing component tests render with an
  `instant` presenter so they keep asserting on the immediate state; new tests assert the step
  sequence and settle behaviour with fake timers.
