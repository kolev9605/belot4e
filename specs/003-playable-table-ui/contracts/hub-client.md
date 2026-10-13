# Contract: Client use of the table hub, and additive server changes

Base protocol: [002 hub-protocol (v1)](../../002-live-table-vs-bots/contracts/hub-protocol.md).
`protocolVersion` remains `1`; every change below is additive.

## Client call sequence

| Step | Call | Success | Failure handling |
|------|------|---------|------------------|
| Connect | `start()` with backoff loop | status `connected` | stay `connecting`/`unavailable`, retry forever |
| Sync | `GetView` | `update` -> apply; `notSeated` -> `idle` (or `lost` if a game was active) | retry on transport error |
| Start game | `CreateTable({})` then `TakeSeat({seat:"south"})` | first `Update` (version 0) arrives as a push | `tableExists` -> `GetView` and resync; others -> notice, `idle` |
| Act | `SubmitIntent(echo of a legalActions entry)` | `{accepted:true}`; the `Update` renders | rejection -> localized notice keyed by `code`, state untouched |
| Resync | `GetView` | on version gap, after reconnect, when tab becomes visible | as above |

The client only sends an intent copied from a `legalActions` entry; it never constructs bids
or cards itself.

## Additive server changes

### 1. `handSizes` in the view

```json
"handSizes": { "north": 8, "east": 8, "south": 8, "west": 8 }
```

Cards currently held per seat (viewer included). Counts only; no card identities. Reflects the
5-card deal before a contract exists.

### 2. `CreateTable` replacement semantics (FR-023)

| Situation | Result |
|-----------|--------|
| No table, or table finished | create (unchanged) |
| Unfinished table owned by **this** connection | `tableExists` (unchanged) |
| Unfinished table owned by **another** connection | old table discarded, new table created; displaced connection notified (below) |

### 3. `TableClosed` server push

Sent to a connection whose table was replaced while it was still connected.

```json
{ "protocolVersion": 1, "reason": "replaced" }
```

Afterwards that connection owns no table: `SubmitIntent` and `GetView` return `notSeated`.
Clients treat it as `session = lost`.

## Hosting

- `Web:RootPath` (string, optional): when set, `Belote.Server` serves static files from that
  directory with `index.html` fallback, same origin as `/table`.
- Dev: Vite proxies `/table` to the server with WebSocket upgrade enabled.

## Hidden-information review (constitution gate)

New wire content is `handSizes` (counts) and `TableClosed` (no game data). A test asserts every
update/view contains no card outside `ownHand`, `currentTrick`, `lastCompletedTrick`,
`completedTricks`, `legalActions` and `ownDeclared`.

## Addendum (feature 005): order of `ownHand`

`ownHand` lists the person's cards strongest-first within each suit for the contract in force
(no-trump order while none is decided). The order across suits is unspecified; clients group by
suit themselves. See `specs/005-modern-game-ui/contracts/hand-order.md`.

