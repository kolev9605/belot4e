# Contract: Hub protocol v2 (rooms)

Transport: the existing SignalR hub at `/table`. One connection per browser tab. JSON, camelCase.
Protocol version is **2** (v1 = single-table feature 002). Every push carries `protocolVersion`;
a client that sees an unsupported value disables input and shows an upgrade message.

Principle: **connection id is never identity.** After every connection (initial start, SignalR
automatic reconnect, page reload) the client calls `Rejoin` or `CreateRoom`/`JoinRoom`.

## Client → server methods

All methods return a result object; they never throw for expected conditions. `rejection` is
`{ category, code, detail }` as in v1.

| Method | Arguments | Success result | Notes |
|--------|-----------|----------------|-------|
| `CreateRoom` | `{ token, name }` | `{ ok, roomId, state }` | Creates a room; caller becomes host and is auto-seated at South (first free seat). |
| `JoinRoom` | `{ roomId, token, name }` | `{ ok, state }` | New participant, or an idempotent re-attach if the token is already known in that room. |
| `Rejoin` | `{ roomId, token }` | `{ ok, state, update?, awayRecap? }` | Re-attaches the connection to an existing participant and returns the current table view if a game is running. Never creates a participant. |
| `SetName` | `{ name }` | `{ ok, state }` | Validates per `NameRules`. |
| `ClaimSeat` | `{ seat }` | `{ ok, state, pendingNextHand? }` | Lobby: immediate. In game: bot seats open to takeover queue until the next hand; reclaim of own replaced seat is immediate. |
| `ReleaseSeat` | `{}` | `{ ok, state }` | Lobby only: stand up. |
| `StartGame` | `{}` | `{ ok, state }` | Host only; lobby only. Empty seats become bots. |
| `SkipWait` | `{ participantId? }` | `{ ok, state }` | While paused: any *other* connected human; replaces all (or the named) missing players with bots now. Rejected for a missing player. |
| `LeaveRoom` | `{}` | `{ ok }` | Deliberate leave; seat becomes `Bot(Left)` immediately (no wait). Clears the connection's room binding. |
| `PlayAgain` | `{}` | `{ ok, state }` | Host only; `Finished` only. |
| `SubmitIntent` | `IntentDto` | `IntentResult` (v1) | Validated against the participant's seat. |
| `GetView` | `{}` | `GetViewResult` (v1) | Resync of the caller's own seat view. |
| `TakeBack` | `{}` | `{ ok, state, update? }` | After a `Superseded` push: re-take control for this tab (supersedes the other). |

Legacy v1 methods `CreateTable` and `TakeSeat` remain as a shim: `CreateTable` = `CreateRoom`
with a server-generated token bound to the connection; `TakeSeat` = `StartGame` (bots fill the
rest). Their results keep the v1 shape.

### Rejection codes (new)

| Code | Category | Meaning / suggested UI |
|------|----------|------------------------|
| `RoomNotFound` | `room` | Unknown or ended room: show "room not found/ended" with menu link. |
| `RoomEnded` | `room` | Idle-expired; clears saved session. |
| `RoomFull` | `room` | No seat can be taken (FR-006). |
| `SeatTaken` | `seat` | Lost a race or seat held; pick another. |
| `NotHost` | `permission` | Host-only action. |
| `NameInvalid` | `name` | Empty/whitespace/too long. `detail` has the reason key. |
| `NameTaken` | `name` | Duplicate in this room. |
| `GameInProgress` | `state` | The game is already running. |
| `GameNotStarted` | `gameNotInProgress` | The action needs a running game (kept from v1). |
| `AlreadySeated` | `seat` | A seated human tried to claim another seat in a running game. |
| `NotInRoom` | `identity` | The token is not a participant of that room (rejoin first, or join). |
| `NotMissing` | `state` | `SkipWait` with nothing to skip (race; ignore silently). |
| `Superseded` | `connection` | This tab was taken over (also returned by calls from the superseded connection); call `TakeBack` to resume. |
| `BadToken` | `identity` | Token malformed; client regenerates and rejoins as a new person. |

## Server → client pushes

### `Update` (unchanged shape, protocol v2)

`{ protocolVersion, version, finished, view }` where `view` is the recipient's own `SeatView`
projection (`view.seat` is now any seat, not always `south`). Delivered in version order. Sent
only to participants who hold a seat in a running game.

### `Room`

```json
{
  "protocolVersion": 2,
  "roomId": "3f2b...-...",
  "revision": 17,
  "serverNowUtc": "2026-10-13T12:00:00.000Z",
  "lifecycle": "inGame",
  "hostId": "p-1",
  "you": { "participantId": "p-2", "seat": "north", "isHost": false },
  "seats": [
    { "seat": "south", "kind": "human", "participantId": "p-1", "name": "Ivo",
      "connection": "connected" },
    { "seat": "north", "kind": "human", "participantId": "p-2", "name": "Mira",
      "connection": "reconnecting", "deadlineUtc": "2026-10-13T12:00:42.000Z" },
    { "seat": "east", "kind": "bot", "name": "Bot", "botFor": null, "origin": "default" },
    { "seat": "west", "kind": "bot", "name": "Bot", "botFor": "Dani", "origin": "replaced" }
  ],
  "missing": [ { "participantId": "p-2", "seat": "north", "deadlineUtc": "..." } ],
  "pendingTakeovers": [ { "seat": "east", "name": "Pavel" } ],
  "lastEvent": { "kind": "replaced", "seat": "west", "name": "Dani", "byName": null, "revision": 16 }
}
```

- Sent to every connected participant on every change (and once on attach).
- `missing` non-empty ⇒ the table is paused; clients render the pause overlay from it.
- Never contains cards, seeds, tokens or connection ids.

### `TableClosed` (v1, reused)

`{ protocolVersion, reason }` with `reason` ∈ `ended` (room idle-expired), `left` (acknowledges
`LeaveRoom` on other tabs of the same identity). v1's `replaced` is no longer sent.

### `Superseded`

`{ protocolVersion }` - the connection lost the participant to a newer connection with the same
token. The client must stop auto-rejoining until the user triggers `TakeBack`.

## Delivery and ordering guarantees

- Room pushes carry a monotonically increasing `revision`; clients ignore stale ones.
- Table `Update`s keep the v1 contract (strictly increasing `version`; a gap triggers `GetView`).
- On `Rejoin`, the server sends the current `Room` and `Update` *before* returning, so the client
  can render immediately from the result and then continue with pushes.
- A participant has at most one live connection; pushes go to that connection only.

## Timing parameters (server configuration, `Room` section)

| Setting | Default | Spec |
|---------|---------|------|
| `GraceSeconds` | 60 | FR-017/018 |
| `IdleExpiryMinutes` | 60 | FR-024 |
| `LobbySeatReleaseSeconds` | 60 | FR-025 |
| `MaxNameLength` | 20 | FR-010 |
| `KeepAliveSeconds` / `ClientTimeoutSeconds` | 5 / 15 | detection speed, SC-003 |
