# Research: Playable Table UI

All items below were open design questions; none remain unresolved.

## R1. Build tooling and framework versions
- **Decision**: Vite + React 19 + TypeScript (strict) + Tailwind CSS 4 (`@tailwindcss/vite`),
  Node.js 22+ (26.5 / npm 11 installed locally), `npm`.
- **Rationale**: Smallest standard setup for a React SPA; Tailwind 4 needs no config file for a
  bare-bones theme.
- **Alternatives**: Next.js (SSR/routing unneeded); Create React App (unmaintained).

## R2. Same-origin vs CORS
- **Decision**: One origin always. Dev: Vite dev server proxies `/table` (WebSocket upgrade
  enabled) to Kestrel. Prod/local run: `Belote.Server` serves `web/dist` when `Web:RootPath`
  is configured.
- **Rationale**: Removes CORS and WebSocket-origin failure modes, directly serving "SignalR
  must connect reliably".
- **Alternatives**: CORS on the hub (more config, more ways to fail); separate hosting
  (unneeded for one table).

## R3. Reliable SignalR connection
- **Decision**: One connection manager owning a `HubConnection`:
  - WebSocket transport (server protocol v1 is WebSocket), library negotiation otherwise.
  - Initial-connect loop: `start()` retried with capped backoff (0, 1, 2, 5, 10 s, then every
    10 s) because built-in automatic reconnect does not cover a failed first start.
  - `withAutomaticReconnect` with the same never-give-up schedule; on `onclose` fall back to
    the start loop.
  - Default keep-alive/timeout (15 s / 30 s); status `connecting | connected | reconnecting |
    unavailable`.
  - After any (re)connect, call `GetView` before enabling input. A reconnect yields a new
    connection id, so the old table is not owned by it: `GetView` returns `NotSeated`, and the
    UI reports the lost game and offers a new one (spec clarification A).
  - Re-sync when the tab becomes visible again (timers are throttled in background tabs).
- **Rationale**: Covers initial start, mid-game drop, server restart, laptop sleep.
- **Alternatives**: Library default reconnect (4 tries then dead); polling (loses push model).

## R4. Ordered updates and resync
- **Decision**: Client keeps `lastVersion`. `version == last + 1` applies; `<= last` is
  dropped as duplicate; `> last + 1` triggers `GetView` and applies its result if newer.
  Push and `GetView` results are reconciled identically, so a race can never move the table
  backwards. `protocolVersion` mismatch shows a blocking "reload" error.
- **Rationale**: Server guarantees gapless increasing versions (002); the client verifies
  rather than trusts (FR-015).

## R5. Abandoned or displaced games (FR-023)
- **Decision**: `CreateTable` from a connection **other than the current owner** replaces an
  unfinished table; the displaced connection, if still connected, receives `TableClosed`
  (`reason: "replaced"`). `CreateTable` from the owning connection while its game is
  unfinished still returns `tableExists` (002 behaviour retained).
- **Rationale**: After a network cut the server may take up to the keep-alive timeout to notice
  the old owner is gone; "latest connection wins" never blocks the player. Reuses the existing
  disposal path.
- **Alternatives**: Replace only when the owner is detected disconnected (can block ~30 s
  after a refresh or cut); a resume protocol (rejected by the clarification).

## R6. Other seats' card counts (FR-003)
- **Decision**: Add public `handSizes` (seat -> count) to the seat view, produced by
  `Projection.For` from the hand arrays, respecting the 5-card initial deal before a contract.
- **Rationale**: Counts are public in real play; computing server-side keeps rule knowledge
  out of the client. Additive field; `protocolVersion` stays `1`.
- **Alternatives**: Client derives "8 minus plays" (wrong during bidding, duplicates deal rules).

## R7. Card and rejection presentation
- **Decision**: Pure label tables keyed by wire codes (ranks, suits `S H D C`, seats,
  contracts, rejection `code`s such as `MustFollowSuit`, phases). Each card is a `<button>`
  whose text is "<rank> of <suit>" plus a suit glyph marked `aria-hidden`; unplayable cards are
  `disabled`, visibly dimmed and struck through, and their accessible name says "not playable".
- **Rationale**: Principle VI without a UI library; not conveyed by color alone.

## R8. Localization
- **Decision**: Typed `en.ts` / `bg.ts` dictionaries in-repo plus a test that both define
  identical keys; default from `navigator.language`; one small switch; preference in
  `localStorage` if available.
- **Rationale**: Two languages, ~40 strings; an i18n library is unjustified weight.

## R9. State management
- **Decision**: One `useReducer` store `{connection, session, game, pending, notice}`; the
  reducer is pure and unit-tested; no external state library.

## R10. Testing strategy
- **Decision**: Vitest for reducer and connection manager (fake hub injected); Testing Library
  for components (a11y names, disabled states); Playwright for (a) a full game against the
  real server with `Table:BotDelayMs=0`, (b) server kill/restart recovery, (c) 375 px
  viewport + keyboard-only, (d) both languages. Server changes via the existing xUnit
  end-to-end harness.

## R11. CI
- **Decision**: Add a `web` job (install, typecheck, Vitest, build) and an `e2e` job (build
  server + client, start with zero bot delay, run Playwright) to `.github/workflows/ci.yml`.
