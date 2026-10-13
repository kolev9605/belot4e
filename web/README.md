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
| `npm run e2e` | Playwright end-to-end tests (slow, opt-in, never run in CI: with `CI` set no spec is picked up); starts the server itself with zero bot delay and serves `dist/` (run `npm run build` first). Uses the installed Chrome locally and Playwright's Chromium when `CI` is set (`npx playwright install chromium`). |

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

## One-screen table, hand and results (spec 006)

- The page is a viewport-height column (`h-dvh`): top bar, table, status row and hand. Card size comes from
  `--card-h`/`--card-w` in `src/theme/tokens.css` (a clamp on the viewport height, also capped by the width so
  eight cards fit a phone), so the whole hand stays visible on a laptop screen without scrolling.
- The hand is one non-wrapping row with a constant overlap and one fixed gap between suits. Highlighting a
  playable card never moves it; only the card under the pointer lifts. Outside the playing phase (bidding, or
  another seat's turn) cards use the neutral `inert` mode and look normal.
- Bidding is paced by the presenter's `bid` step (`bidHoldMs` in `src/theme/motion.ts`); the person's own call is
  never delayed. Each seat box lists the calls that seat made (`callLabel`, derived from `biddingHistory`).
- A finished hand opens `HandResultDialog` through the presenter's blocking `result` step: the next deal does not
  start until it is dismissed. Its data is the server's `lastHand` view field
  (`specs/006-table-ui-polish/contracts/hand-summary.md`); the client shows it and computes no scores.
