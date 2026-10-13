# Quickstart: validating Rooms and Multiplayer

How to run the app and prove each user story. Contracts: [hub-protocol](contracts/hub-protocol.md),
[client-storage](contracts/client-storage.md). Model: [data-model](data-model.md).

## Prerequisites

- .NET 10 SDK and Node 22+
- Two different browser profiles (or one normal and one private window) so each has its own
  `localStorage` identity

## Run

```bash
dotnet run --project src/Belote.Server
```

```bash
cd web && npm install && npm run dev
```

Open the Vite URL (the dev proxy forwards `/table` to the server).

## Automated checks (preferred)

```bash
dotnet test
```

```bash
cd web && npm run typecheck && npm test
```

Playwright e2e is opt-in and not part of verification.

Expected coverage of this feature:

| Area | Where |
|------|-------|
| Lobby, seat races, names, host handover | `tests/Belote.Table.Tests/Rooms/RoomLobbyTests` |
| Pause, countdown, replace, skip-wait, multi-missing | `.../PauseAndReplaceTests` (fake clock) |
| Reclaim handover, no lost/duplicated move | `.../ReclaimTests`, `.../HandoverPropertyTests` |
| Late join at next hand, play again, expiry | `.../LateJoinTests`, `.../ExpiryTests` |
| Multi-client hub, reload/reconnect, takeover, hidden info, concurrent rooms | `tests/Belote.Server.Tests/EndToEnd/*` |
| Link parsing, storage failure, countdown clock, reducer | `web/tests/*` |

## Manual walkthrough

| # | Story | Steps | Expect |
|---|-------|-------|--------|
| 1 | US1 | Browser A: **Start game**, enter a name. | Lobby with you seated; link + code shown; **Copy link** confirms. |
| 2 | US2 | Browser B: open the copied link, enter a name, pick a free seat. | Both lobbies show both players within a second. |
| 3 | US2 | Browser B: use **Join game** with the bare guid instead. | Same lobby; a nonsense code shows an inline message. |
| 4 | US3 | A: **Start**. | B sees only its own hand; names (not compass labels) at the seats; bots fill the rest. |
| 5 | US4 | B: reload mid-hand. | A sees a pause with B's name + countdown; B returns to the same seat and hand automatically; play resumes. |
| 6 | US4 | B: close the tab, wait 10 s, reopen `/`. | Menu offers **Return to your game**; it returns to the seat. |
| 7 | US5 | B: close the tab and wait 60 s. | A sees "a bot is playing for B"; play continues. |
| 8 | US5 | B reopens the room. | B regains the seat with the current hand and a short "what you missed" note. |
| 9 | US5 | B drops again; on A press **Play with a bot now**. | Bot takes B's seat immediately; B can still reclaim. |
| 10 | US6 | Change name in the lobby; reload. | New name visible to all; remembered after reload. |
| 11 | US7 | A leaves mid-game. | A's seat becomes a plain bot seat; room continues; host passes to B. |
| 12 | FR-008a | Browser C opens the link mid-game and picks a bot seat. | "You'll join at the start of the next hand"; takes over then. |
| 13 | FR-022 | Open the room again in a second tab of A. | The first tab shows "open in another tab - Take back". |
| 14 | FR-006 | Open a random guid. | "Room not found" with a menu link; never a blank page. |

## Simulating bad networks

- Chrome DevTools → Network → **Offline** for 20 s, then back to online: expect the reconnecting
  overlay on that tab and the pause on others; recovery within 5 s of coming online.
- Stop and restart the server: all tabs end at "This room has ended" within the reconnect window
  and clear their saved session.
