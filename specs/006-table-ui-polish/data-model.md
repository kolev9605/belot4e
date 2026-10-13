# Data Model: Table UI Polish

## Server: `HandSummary` (new, in `Belote.Table`)

Built once when an accepted move contains a `HandScored` event; stored on `TableSnapshot.LastHand`;
unchanged until the next hand is scored. Null before the first hand is scored and when a hand was
voided (everyone passed) - a voided hand never replaces an earlier summary.

| Field | Type | Source / meaning |
|-------|------|------------------|
| `dealNumber` | int | Deal the summary describes; the client's "have I shown this?" key |
| `contract` | kind, declarer, multiplier | `HandResult.Kind/DeclaringTeam`, multiplier |
| `outcome` | `made` / `failed` / `tied` | `HandResult.Outcome` |
| `declaringTeam` | team | `HandResult.DeclaringTeam` |
| `teams[northSouth|eastWest]` | `TeamHand` | see below |
| `matchScore` | `{northSouth, eastWest}` | Running totals *after* this hand (from `MatchScoreUpdated`) |
| `heldOver` | `{before, awarded, awardedTo, newlyHeld, after}` | Tied-hand carry-over, as scored |
| `winner` | team or null | Set when this hand ended the game |

### `TeamHand`

| Field | Type | Meaning |
|-------|------|---------|
| `wonCards` | string[] | Every card the team won, in trick order (flattened; 0..32 cards) |
| `cardPoints` | int | `HandResult.CardPoints[team]` |
| `lastTrickBonus` | int | `LastTrickBonus` if this team won the last trick, else 0 |
| `announcementPoints` | int | `AnnouncementPoints[team]` |
| `belotePoints` | int | `BelotePoints[team]` |
| `allTricksBonus` | int | `AllTricksBonus` if this team swept, else 0 |
| `rawTotal` | int | `RawTotals[team]` (sum of the parts above) |
| `gamePoints` | int | `GamePoints[team]` awarded for the hand (after multiplier/conversion/held-over) |

Validation rules:

- `wonCards` of both teams are disjoint and together hold exactly the 32 cards of the hand.
- `cardPoints + lastTrickBonus + announcementPoints + belotePoints + allTricksBonus == rawTotal`
  for each team.
- `matchScore` equals the previous match score plus `gamePoints` for each team.

## Client: types and state

- `TableView.lastHand: HandSummary | null` - mirrors the wire shape (`protocol/types.ts`).
- **Seat call history** (derived, not stored): `callsBySeat(view.biddingHistory)` ->
  `Record<Seat, BidEntry[]>`; the last entry of a seat is the "latest" call. Empty on a new deal.
- **Presentation steps** (see `contracts/presentation-steps.md`): `bid` (timed) and `result`
  (blocking until released). `Frame` gains `result: HandSummary | null`.

## State transitions

```text
hand being played
   |  last trick played (server: HandScored -> LastHand set, next deal or finished)
   v
presenter: play -> hold -> collect  (last trick)
   v
presenter: result  (blocking; dialog open)  --dismiss-->  deal (next hand)  or  end of game
```

If a view arrives that does not change `lastHand.dealNumber`, no `result` step is produced.
