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
- `src/presentation` presenter: turns the server's views into timed steps (deal, play, hold, collect) so cards
  are shown at a readable pace; input is enabled only once the table has settled on the latest view
- `src/theme` design tokens (`tokens.css`, one dark palette and accent), pacing values (`motion.ts`) and geometry

## Pacing and theme

- Card pacing defaults live in `src/theme/motion.ts` (`DEFAULT_PACING`). `?pace=instant` in the page address
  (or `VITE_PACING=instant` at build/test time) turns every pause off; unit tests and most e2e specs use it.
- Colours come only from the tokens in `src/theme/tokens.css`; `tests/theme-contrast.test.ts` checks every
  text/UI pair against WCAG AA and fails if light-theme utility classes creep back in.
- The hand is grouped by suit in the fixed order spades, hearts, clubs, diamonds. Order inside a suit is the
  server's (strongest first for the contract in force); the client never computes card power.

