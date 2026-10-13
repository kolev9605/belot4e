# Quickstart: Playable Table UI

Validation guide for feature 003. Contracts: [hub-client.md](contracts/hub-client.md),
[client-state.md](contracts/client-state.md).

## Prerequisites

- .NET 10 SDK, Node.js 22+ and npm (checked: Node 26.5, npm 11.17).
- Dependencies: `npm install` in `web/`; `npx playwright install chromium` once for e2e.

## 1. Run in development

```bash
dotnet run --project src/Belote.Server --urls http://localhost:5080
```

```bash
cd web && npm run dev
```

Open the printed Vite URL (default `http://localhost:5173`). Vite proxies `/table` to the
server. Expected: status shows connected; a Start game button is visible.

## 2. Run as one origin (production-style)

```bash
cd web && npm run build
```

```bash
dotnet run --project src/Belote.Server --urls http://localhost:5080 -- --Web:RootPath=<absolute path to web/dist>
```

Open `http://localhost:5080`. Use an absolute path: a relative one resolves against the server's
working directory, and the server logs a warning if the directory does not exist.

## 3. Manual validation scenarios

| # | Scenario | Expected |
|---|----------|----------|
| 1 | Start game, play to the end | Winner and final score shown; Play again starts a new game (US1) |
| 2 | Try an off-suit card / act on bots' turn | Not played; localized reason shown; table unchanged (US2) |
| 3 | Stop server mid-game, restart it | Disconnected banner within 5 s; after restart connected within 10 s; "game lost" + Start game (US3) |
| 4 | Open page before server runs, then start server | Shows connecting/unavailable, becomes usable without reload (US3) |
| 5 | Refresh page mid-game | New game can be started immediately (FR-023) |
| 6 | Open second tab and start a game | First tab shows "game lost"; no dead table (edge case) |
| 7 | 375 px viewport, touch/keyboard only, both languages | Full game playable, no horizontal scroll, all text localized (US4) |

## 4. Automated checks

```bash
dotnet test Belote.sln -c Release           # includes handSizes and replace-on-create tests
cd web && npm run typecheck && npm test      # reducer, connection manager, components, i18n parity
cd web && npm run build && npm run e2e       # Playwright: full game, restart recovery, phone viewport
```

Expected: all green; e2e runs with `Table:BotDelayMs=0` so a game finishes in seconds.
