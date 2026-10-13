# Data Model: Rooms and Multiplayer

All server-side state is in process memory and owned by one `Room` actor per room. Nothing here is
persisted. Client-side persistence is listed at the end.

## Room

| Field | Type | Notes |
|-------|------|-------|
| `Id` | GUID | Random; the only secret needed to *find* a room. Never reused. |
| `Lifecycle` | `Lobby` \| `InGame` \| `Finished` \| `Ended` | See transitions below. |
| `Seats` | 4 × `SeatSlot` | Keyed by `Seat` (North/East/South/West). |
| `Participants` | map `ParticipantId → Participant` | Humans known to the room (seated or in the lobby). |
| `HostId` | `ParticipantId` | Always a connected-or-grace human while any human exists. |
| `Table` | `LiveTable?` | Present while `InGame`/`Finished`; replaced on Play again. |
| `Missing` | map `ParticipantId → PauseEntry` | Non-empty ⇒ table paused (only while `InGame`). |
| `Revision` | int | Increases on every room-state change; used by clients to order pushes. |
| `LastEvent` | `RoomEvent?` | Most recent notable change, for toasts / aria-live. |
| `IdleSince` | UTC time? | Set when no human is connected; cleared when one returns. |

### Lifecycle transitions

```text
Lobby ──host starts──► InGame ──game ends──► Finished ──host: play again──► InGame
  │                      │                       │
  └──── idle > 1 h ──────┴──── idle > 1 h ───────┴──────────────────────────► Ended
```

- `Lobby → InGame`: host starts; every non-human seat becomes a bot; the table is created with the
  room's rule set and a seed chosen by the room's caller (never the engine).
- `Finished → InGame`: same seats, names and hosts; new table and seed.
- `* → Ended`: only by idle expiry (FR-024). A deliberate "leave" never ends a room.

## SeatSlot

A seat is exactly one of:

| Kind | Fields | Meaning |
|------|--------|---------|
| `Free` | – | Lobby only. Anyone may claim it. |
| `Human` | `ParticipantId` | Controlled by that participant (while connected or in grace). |
| `Bot` | `Origin` | Controlled by a bot. `Origin` is one of: `Default` (never human; open to late join), `Left` (a human left deliberately; open to late join), `Replaced(ParticipantId)` (disconnect/timeout/skip; only that participant may reclaim, until the game ends). |

Invariant: at most one `Human` seat per participant; every seat is non-`Free` while `InGame`.

### Seat claim rules

| Situation | Rule |
|-----------|------|
| Lobby, seat `Free` | Claim succeeds for exactly one caller (actor order); loser gets `SeatTaken`. |
| `InGame`, seat `Bot(Default | Left)` | Claim records a `PendingTakeover`; applied when the table's deal number next increases. First claimant wins; others get `SeatTaken`. |
| `InGame`, seat `Bot(Replaced(p))` | Only participant `p` may claim; applied immediately (atomic controller swap). |
| `InGame`, seat `Human` | Rejected `SeatTaken`. |
| Participant already seated | Moving seats allowed in the lobby only; in a game a human must leave first. |

## Participant

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `ParticipantId` (GUID) | Public identifier shown in room state. |
| `TokenHash` | bytes | SHA-256 of the client's secret token; compared in constant time. |
| `Name` | string | Trimmed, 1–20 characters, unique within the room (case-insensitive). Plain text only. |
| `Seat` | `Seat?` | Null while only in the lobby. |
| `Connection` | `Connected(connectionId)` \| `Reconnecting(deadlineUtc)` \| `Away` | `Away` = replaced by a bot and not currently connected. |
| `PendingTakeover` | `Seat?` | Set while waiting for the next hand boundary. |

Validation: `Name` rules in `NameRules` (empty/whitespace, length, duplicate → localized error
code, never a thrown exception).

### Connection state transitions

```text
Connected ──socket closed──► Reconnecting(deadline = now + 60 s) ──returns──► Connected
                                   │
                                   ├── deadline reached ─────► Away (seat → Bot(Replaced))
                                   └── another human "Play with a bot now" ─► Away (same)

Away ──returns (token matches)──► Connected (seat reclaimed at once if still Bot(Replaced(me)))
Connected ──"Leave"──► (no participant seat; seat → Bot(Left) if in game)
```

Only participants who hold a seat while `InGame` can be `Reconnecting`; in the lobby a dropped
participant's seat is simply released after the same 60 s (FR-025) without pausing anything.

## PauseEntry

| Field | Type | Notes |
|-------|------|-------|
| `ParticipantId` | | Who is missing. |
| `Seat` | `Seat` | Their seat (for display). |
| `DeadlineUtc` | UTC time | Absolute; started once per disconnection (FR-021, no cumulative cap). |

`Room.Paused = Lifecycle == InGame && Missing.Count > 0` and is forwarded to
`LiveTable.SetPausedAsync`. Removing the last entry lifts the pause.

## RoomEvent (for notices)

| Field | Notes |
|-------|-------|
| `Kind` | `replaced`, `skipped`, `reclaimed`, `left`, `joined`, `joiningNextHand`, `hostChanged`, `nameChanged`, `gameStarted`, `gameFinished` |
| `Seat?` / `Name?` | Subject of the event. |
| `ByName?` | Acting participant (e.g., who skipped the wait). |
| `Revision` | Revision at which it happened. |

## Wire projections (not storage)

- **`RoomState`** (to everyone in the room): id, lifecycle, host, per-seat slot summaries
  (kind, name, bot-for-name, connection status, deadline), `missing[]`, `pendingTakeovers[]`,
  `revision`, `lastEvent`, `serverNowUtc`, and `you` (own participant id, seat, `isHost`).
- **`TableUpdate`** (to one participant): unchanged shape - `Projection.For(state, mySeat)`.
- **`awayRecap`** (once, to a returning replaced participant): number of moves played for them.

No `RoomState` field contains a hand, a seed, or another participant's token.

## Client-side persisted data (`localStorage`, per browser)

| Key | Value | Purpose |
|-----|-------|---------|
| `belote.identity.v1` | `{ token }` | Random 128-bit secret; proves "same person" on rejoin. |
| `belote.profile.v1` | `{ name }` | Remembered display name (FR-009). |
| `belote.session.v1` | `{ roomId, lastSeen }` | Powers "Return to your game" and auto-rejoin. |
| existing language key | `en` \| `bg` | Unchanged. |

All reads/writes are wrapped in try/catch; the app renders and works (without resume) when storage
is unavailable.
