# Contract: Table hub protocol (v1)

SignalR hub at `/table`, JSON protocol, WebSocket transport. All names are camelCase; seats,
suits, contracts, and cards use the 001 log vocabulary (`north`..`west`, `hearts`/`noTrump`/
`allTrump`, `AS`, `10H`, ...). `protocolVersion` is `1`; incompatible changes bump it.

## Client to server (hub methods)

| Method | Arguments | Returns | Notes |
|--------|-----------|---------|-------|
| `CreateTable` | `{ seed?: uint64, botDelayMs?: int }` | `{ ok, tableId?, rejection? }` | Creates the table in `AwaitingSeat`. Refused with `tableExists` while any game at this server is unfinished (there is one table per server). A finished table is closed first. `botDelayMs` is clamped to 0..5000; when omitted the server default (`Table:BotDelayMs`) applies. When `seed` is omitted the server chooses one. |
| `TakeSeat` | `{ seat: "south" }` | `{ ok, version?, rejection? }` | Only `south` is accepted; any other known seat is `notOwner`, an unknown seat is `malformed`. Success starts the game and returns `version: 0`; the initial view arrives as the first `Update`. |
| `SubmitIntent` | `IntentDto` | `IntentResult` | One intent per call. |
| `GetView` | none | `{ update?: TableUpdate, rejection? }` | Pure read; never changes state. Same content as the update for the current version. Before seating it returns `gameNotInProgress` / `GameNotStarted`; with no table, `notSeated`. After the game finishes it returns the final view. |

### IntentDto

```json
{ "kind": "bid",       "seat": "south", "contract": "hearts" }
{ "kind": "pass",      "seat": "south" }
{ "kind": "contra",    "seat": "south" }
{ "kind": "recontra",  "seat": "south" }
{ "kind": "announce",  "seat": "south", "announcement": { "kind": "sequence", "suit": "hearts", "rank": "Q", "length": 3 } }
{ "kind": "announce",  "seat": "south", "announcement": { "kind": "carre", "rank": "J", "length": 4 } }
{ "kind": "play",      "seat": "south", "card": "AS" }
```

The three intent families in the spec map as: **bid** = `bid`/`pass`/`contra`/`recontra`,
**declare** = `announce`, **play** = `play`. Entries in `legalActions` use these exact shapes,
so a client may echo one back unchanged.

### IntentResult

```json
{ "accepted": true,  "version": 17 }
{ "accepted": false, "rejection": { "category": "illegal", "code": "MustFollowSuit",
                                    "detail": "South must follow the led suit (hearts)." } }
```

`detail` is English developer text; clients must key behaviour and localization off `code`.

## Server to client

### `Update(TableUpdate)`

Pushed once per state change (accepted intent, or seating), to the seated connection only.

```json
{
  "protocolVersion": 1,
  "version": 17,
  "finished": false,
  "view": {
    "seat": "south", "phase": "play", "dealer": "west", "toAct": "south", "dealNumber": 3,
    "ownHand": ["AS","10S","QH","7H","8C"],
    "biddingHistory": [{"seat":"north","action":"pass"}, {"seat":"east","action":"bid","contract":"hearts"}],
    "standingBid": "hearts", "standingBidder": "east", "contra": false, "recontra": false,
    "contract": {"kind":"hearts","declarer":"east","multiplier":1},
    "leader": "west",
    "currentTrick": [{"seat":"west","card":"9H"},{"seat":"north","card":"JH"}],
    "lastCompletedTrick": {"dealNumber":3,"trickNumber":2,"leader":"east","plays":[{"seat":"east","card":"KD"}, "..."],"winner":"north"},
    "completedTricks": [ "..." ],
    "announcements": [{"seat":"east","kind":"sequence","length":3,"revealed":null,"points":null}],
    "ownDeclared": [],
    "belotes": [],
    "matchScore": {"northSouth":34,"eastWest":51}, "heldOverPoints": 0,
    "winner": null,
    "legalActions": [ {"kind":"play","seat":"south","card":"QH"}, {"kind":"play","seat":"south","card":"7H"} ]
  }
}
```

Guarantees:

1. **Order**: `version` increases by exactly 1 per update; clients can detect a gap or a
   duplicate.
2. **Seat-only**: contains only the viewing seat's data plus public table information
   (see [data-model.md](../data-model.md) visibility rules). No other hand, undealt card,
   deck order, seed, or log event is ever sent.
3. **Legal actions**: non-empty only when `toAct == seat` and the game is in progress; in the
   exact order `LegalActions.For` produces (needed for 001-equivalent client choices).
4. **Trick completion** (FR-027): the update caused by the fourth card of a trick contains
   all four cards and the winner in `lastCompletedTrick`, which stays present until the next
   card is played. `currentTrick` is empty in that update.
5. **Finish**: the final update has `finished: true`, `view.phase: "finished"`,
   `view.winner`, and the final `matchScore`; `legalActions` is empty. Later intents are
   rejected with `gameNotInProgress`.

## Rejection categories

| `category` | `code` values | Meaning |
|------------|---------------|---------|
| `malformed` | `Malformed` | Intent could not be parsed |
| `notSeated` | `NotSeated` | This connection has not created a table (or is not the table creator) |
| `gameNotInProgress` | `GameNotStarted`, `GameFinished`, `TableFaulted` | Game not accepting moves |
| `notOwner` | `NotOwner` | Seat not controlled by this client |
| `outOfTurn` | `NotYourTurn` | Not this seat's turn |
| `illegal` | 001 `RejectionReason` names | Rules forbid the action |
| `tableExists` | `TableExists`, `SeatTaken` | `CreateTable` while a game is unfinished; `TakeSeat` when the seat is already taken |
| `paused` | `TablePaused` | The table is paused because the client is disconnected (internal; a connected client never sees it) |

Every rejection leaves table state, version, and log byte-identical; nothing is pushed.

## Connection behaviour

- One table at a time per server; the creating connection owns South.
- Connection loss pauses the table (FR-026). Resuming a connection to the table is not part
  of protocol v1.
- The hub allows concurrent invocations on one connection so bursts reach the table, which
  serialises them (FR-014).

## Amendments from feature 003 (additive, protocol stays v1)

- The view carries `handSizes` (`{north,east,south,west}` card counts; counts only).
- `CreateTable` from a connection other than the current owner replaces an unfinished table;
  the owner's own `CreateTable` while unfinished still returns `tableExists`.
- A displaced, still-connected owner receives `TableClosed { protocolVersion, reason: "replaced" }`
  and afterwards gets `notSeated`.

See [003 hub-client contract](../../003-playable-table-ui/contracts/hub-client.md).
