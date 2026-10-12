# Belote web client

React 19 + TypeScript (strict) + Tailwind CSS 4, talking to the SignalR table hub at `/table`.
The client renders what the server sends and offers only the server's legal actions; it contains
no game rules.

## Commands

| Command | Purpose |
|---------|---------|
| `npm install` | install dependencies (Node.js 22+) |
| `npm run dev` | Vite dev server; proxies `/table` to `http://localhost:5080` (override with `BELOTE_SERVER`) |
| `npm run typecheck` | strict TypeScript check |
| `npm test` | Vitest unit and component tests |
| `npm run build` | typecheck and production build into `dist/` |
| `npm run e2e` | Playwright end-to-end tests; starts the server itself with zero bot delay and serves `dist/` (run `npm run build` first). Uses the installed Chrome locally and Playwright's Chromium when `CI` is set (`npx playwright install chromium`). |

## Layout

- `src/protocol` wire types; `src/connection` SignalR client with retry and status
- `src/state` reducer and selectors; `src/i18n` English and Bulgarian strings
- `src/components` the table UI
