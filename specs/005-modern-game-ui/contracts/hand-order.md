# Contract: Hand order in the seat projection

**Scope**: `SeatViewDto.OwnHand` as produced by `SeatViewMapper` (`Belote.Table`). No schema or
protocol-version change; this tightens the guarantee on the array's order.

## Guarantee

For every `SeatViewDto`:

1. `OwnHand` is a permutation of the seat's hand (same cards, same count).
2. For any two cards of the same suit, the stronger card appears first, where strength is
   `CardStrength.Of(ruleSet, kind, suit, card)` with `kind = Contract.Kind`, or `NoTrump` when no
   contract has been decided.
3. Ties (none exist in the standard rule sets) fall back to the engine's rank order, so the
   output is deterministic.
4. Order of cards of *different* suits relative to each other is unspecified; the client groups
   by suit itself.
5. `LegalActions`, `HandSizes` and every other field are unchanged.

## Non-goals

- No new field, no rule data on the wire, no change to hidden-information projection.

## Test obligations

- Unit: a known hand under suit-trump, no-trump and all-trump yields the expected per-suit order.
- Property: for random seeds and each shipped rule set, the output is a permutation and each
  suit's sequence is strength-descending.
- Existing mapper tests (`OwnHand` matches the view's hand as a set) keep passing.
