<!--
Sync Impact Report
- Version change: (unfilled template) → 1.0.0
- Modified principles: none renamed (initial adoption; all placeholders filled)
- Added principles: I. Pure Rules Engine; II. Server-Authoritative; III. Humans and Bots
  Are Interchangeable; IV. Variants Are Configuration; V. Test-First Game Logic
  (NON-NEGOTIABLE); VI. Mobile-First and Accessible
- Added sections: Technology Constraints; Development Workflow & Quality Gates; Governance
- Removed sections: none
- Follow-up TODOs: none. Note: the input numbered its principles 1–5 and 8; they were
  adopted as six consecutive principles (I–VI). No content was assumed for the missing
  items 6 and 7; add them via a MINOR amendment if intended.
- Templates: plan/spec/tasks templates read this file at runtime and were not modified.
-->
# Belot4e Constitution

A web-based Belote card game supporting human multiplayer and AI bots.

## Core Principles

### I. Pure Rules Engine
All game rules — deck, dealing, bidding, legal moves, trick resolution, announcements, and
scoring — MUST live in a dependency-free .NET library, `Belote.Core`.

- `Belote.Core` MUST NOT perform I/O, read the clock, or use ambient randomness.
- Randomness (e.g., shuffling) MUST come only from an explicitly injected seed.
- Given the same inputs (state, action, rule set, seed), the engine MUST always produce the
  same outputs.
- `Belote.Core` MUST NOT reference ASP.NET Core, SignalR, persistence, or any UI package.

Rationale: a deterministic, side-effect-free engine can be tested exhaustively, replayed from
a seed to reproduce any bug, and trusted by both server and bots.

### II. Server-Authoritative
Clients send intents only; the server validates every intent against the rules engine before
it changes state.

- The server MUST reject any intent that is illegal, out of turn, or from a seat the sender
  does not control.
- Hidden information (other players' hands, undealt cards, remaining deck order, the seed)
  MUST NEVER be sent to a client not entitled to see it.
- Each player MUST receive a per-player projection of the game state; full state MUST NOT
  be serialized to clients.
- Client-side rule checks are a UX convenience only and MUST NOT be relied upon for
  correctness.

Rationale: cheating and information leaks are structurally impossible when clients are
untrusted and only ever see their own projection.

### III. Humans and Bots Are Interchangeable
Every seat is driven through the same player interface, whether controlled by a human or a bot.

- A bot MUST implement the same player interface as a human seat.
- A bot MUST act only on its seat's projected view, never on full game state.
- The engine MUST NOT know or care who is playing a seat; no engine or server rule may
  branch on "human" versus "bot".
- Replacing a seat's controller (e.g., a bot taking over a disconnected human) MUST require
  no change to game state or engine code.

Rationale: a single seat abstraction keeps bots honest, enables mixed tables and seat
takeover, and makes bot-vs-bot simulation a faithful test of the real system.

### IV. Variants Are Configuration
Rule variants MUST be expressed as data in a rule-set object, never as code forks.

- Bulgarian/French differences, contracts, announcements, and scoring options MUST be
  selected through the rule set passed to the engine.
- Rule values MUST NOT be hard-coded in engine logic, server code, or UI code.
- Adding a variant MUST be achievable by defining a new rule set (plus new engine
  capability only where a genuinely new mechanic is required).
- The active rule set MUST be an explicit input to every engine operation and recorded with
  the game so it can be replayed.

Rationale: one engine and one test suite serve every variant, avoiding divergent forks.

### V. Test-First Game Logic (NON-NEGOTIABLE)
Game logic is developed test-first, and no rule changes merge without tests.

- Every rule MUST have unit tests, written before or together with the implementation.
- Invariants (e.g., a legal move always exists for the player to act; scores always sum
  correctly) MUST be covered by property-based tests.
- Bot-vs-bot simulations MUST run in CI across supported rule sets to surface engine bugs;
  any simulation failure MUST fail the build and be reproducible from its seed.
- A pull request that changes rule behavior without accompanying tests MUST NOT be merged.

Rationale: card-game rules are intricate and a single scoring or legality bug invalidates
games; automated, seed-reproducible checks are the only scalable defense.

### VI. Mobile-First and Accessible
The UI MUST be designed for phone screens first and enhanced for larger screens.

- The UI MUST support Bulgarian and English from the first release; all user-facing strings
  MUST be localized, with none hard-coded in components.
- The UI MUST meet basic accessibility standards: sufficient color contrast, full keyboard
  operability, and meaningful screen-reader labels for every card (rank and suit) and
  game-state change.
- Information MUST NOT be conveyed by color or suit symbol alone.

Rationale: the primary audience plays on phones, and a card game that only some players can
see, read, or operate is incomplete.

## Technology Constraints

- Back-end: ASP.NET Core with SignalR for real-time communication.
- Rules engine: .NET library `Belote.Core` (see Principle I).
- Front-end: React with TypeScript (strict mode); the web client is the only supported
  client.
- Real-time contracts (intents, projected state, events) MUST be explicit, typed, and
  versioned so server and client can evolve safely.
- Introducing a technology outside this list requires an amendment or a documented,
  justified exception in the feature plan.

## Development Workflow & Quality Gates

- Every feature plan MUST include a Constitution Check against Principles I–VI before design
  begins and again after design.
- CI MUST run unit tests, property-based tests, and bot-vs-bot simulations, and MUST pass
  before merge.
- Changes touching `Belote.Core` MUST demonstrate its independence (no new dependencies and
  no I/O, clock, or unseeded randomness).
- Changes to state serialization or hub messages MUST be reviewed for hidden-information
  leaks.
- UI changes MUST be verified at phone viewport size, in both languages, and with keyboard
  navigation.
- Simplicity is preferred: complexity beyond these principles MUST be justified in writing.

## Governance

This constitution supersedes all other practices and conventions in this project.

- **Amendments**: proposed via pull request that states the change, rationale, and any
  migration needed for existing code; requires approval from a project maintainer and an
  updated version and Last Amended date.
- **Versioning**: semantic. MAJOR for backward-incompatible principle removals or
  redefinitions; MINOR for new principles/sections or materially expanded guidance; PATCH
  for clarifications and wording fixes.
- **Compliance**: every pull request and review MUST verify compliance with these
  principles; violations MUST be fixed or explicitly justified and recorded before merge.

**Version**: 1.0.0 | **Ratified**: 2026-10-12 | **Last Amended**: 2026-10-12
