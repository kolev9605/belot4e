# Data Model: Playable Table UI

Client-side state only; server entities are in
[002 data-model](../002-live-table-vs-bots/data-model.md). Wire shapes are in
[contracts/hub-client.md](contracts/hub-client.md); transitions in
[contracts/client-state.md](contracts/client-state.md).

## AppState

| Field | Type | Notes |
|-------|------|-------|
| `connection` | `ConnectionStatus` | `connecting` \| `connected` \| `reconnecting` \| `unavailable` |
| `session` | `Session` | `idle` \| `starting` \| `playing` \| `finished` \| `lost` |
| `game` | `TableView \| null` | Latest applied server view; never modified locally |
| `lastVersion` | `number \| null` | Version of `game`; drives ordering |
| `pending` | `PendingAction \| null` | At most one in-flight intent |
| `notice` | `Notice \| null` | Transient localized message |
| `language` | `"en" \| "bg"` | Display language |

## TableView (mirror of server `view`)

`seat`, `phase`, `dealer`, `toAct`, `dealNumber`, `ownHand`, `handSizes`, `biddingHistory`,
`standingBid`, `standingBidder`, `contra`, `recontra`, `contract`, `leader`, `currentTrick`,
`lastCompletedTrick`, `completedTricks`, `announcements`, `ownDeclared`, `belotes`,
`matchScore`, `heldOverPoints`, `winner`, `legalActions`.

Derived selectors (presentation only, no rules):

| Selector | Source |
|----------|--------|
| `isMyTurn` | `toAct === "south"` and `phase` not `finished` |
| `playableCards` | cards in `legalActions` entries of kind `play` (set membership) |
| `bidActions` / `announceActions` | `legalActions` filtered by `kind` |
| `trickToShow` | `currentTrick` if non-empty, else `lastCompletedTrick` |

## PendingAction

`{ intent, sentAt }`. Set when an intent is sent, cleared on its result. Blocks further sends
(FR-010). The following `Update`, not the result, changes the view.

## Notice

`{ kind: "rejection" | "lost" | "versionMismatch" | "error", code?: string }`; rendered through
localization tables keyed by `code`, never from server `detail` text.

## Validation rules

- Update applied only if `protocolVersion === 1` and `version` is next in sequence (or newer
  via resync).
- Intents sent only from `playing` + `connected` + `isMyTurn` + no `pending`, and only as an
  echo of a current `legalActions` entry.
- Transition into `lost` clears `game`, `lastVersion`, `pending`.
