# Contract: Client URLs, storage and reconnect behaviour

## URLs

| Form | Meaning |
|------|---------|
| `/` | Main menu: **Start game**, **Join game**, and **Return to your game** when a saved session exists. |
| `/room/<guid>` | Deep link. Opens the lobby (or the running game) for that room. |

Join box input is normalised by `parseRoomLink(text)`: trims whitespace and surrounding braces,
accepts a bare GUID (dashes optional, case-insensitive), or any text containing `/room/<guid>`
(full link, with or without scheme/query). Anything else yields the inline message
"That doesn't look like a room code or link." and no network call.

Navigation uses the History API; the server falls back to `index.html` for `/room/*`.

## Storage keys (`localStorage`)

See [data-model.md](../data-model.md#client-side-persisted-data-localstorage-per-browser).
Rules:

1. Every access is inside try/catch; failures degrade to "no resume", never to a crash.
2. The token is generated once with `crypto.getRandomValues` (16 bytes, hex) and never placed in a
   URL, log, or analytics event.
3. `belote.session.v1` is written when a room is entered and cleared on `LeaveRoom`, on
   `RoomNotFound`/`RoomEnded`, and when the user chooses "Back to menu" from a reconnect failure.

## Reconnect behaviour

| Trigger | Client action |
|---------|---------------|
| Page load at `/room/<guid>` | Connect → `Rejoin` if the saved session matches, else show name entry then `JoinRoom`. |
| Page load at `/` with saved session | Show "Return to your game"; do not auto-jump (the person may have meant to leave). A hard reload *inside* a room route rejoins automatically. |
| SignalR `onreconnected` | Call `Rejoin` immediately; keep the table on screen (marked stale) until it returns. |
| SignalR `onclose` (gave up) | Existing connect loop (0 s, 1 s, 2 s, 5 s, then every 10 s forever) then `Rejoin`. |
| `Rejoin` → `RoomNotFound`/`RoomEnded` | Clear session; show "This room has ended" with a button to the menu. |
| `Superseded` push | Stop auto-rejoin; overlay "Open in another tab - Take back". |
| Visibility change to visible / `online` event | Nudge the connect loop to attempt immediately (mobile wake-up). |

The reconnecting overlay never blocks forever: it always offers **Back to menu**.

## Countdown rendering

`remaining = deadlineUtc - (Date.now() + offset)` where `offset = serverNowUtc - Date.now()`
captured when the latest `Room` message arrived. Re-rendered every second; clamped at 0; at 0 the
UI waits for the next `Room` message (it never replaces a player locally).
