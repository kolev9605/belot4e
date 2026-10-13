# Implementation Plan: Rooms and Multiplayer

**Branch**: `007-rooms-multiplayer` | **Date**: 2026-10-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/007-rooms-multiplayer/spec.md`

## Summary

Turn the single-client "one human vs three bots" table into **rooms**: a room has a lobby, up to
four human seats, bots in the rest, a host, and a connection lifecycle that survives reloads.

The approach keeps the rules engine untouched and layers new behaviour around it:

1. **Seat control becomes dynamic.** `LiveTable` today hard-codes South as the human and
   North/East/West as bots. It is generalised so each seat has a *controller* (a connection-bound
   human principal or a bot) that can be swapped atomically inside the table's actor loop. Swapping
   controllers never touches `GameState` (Constitution III). `BotDriver` plays whichever seats are
   currently bot-controlled.
2. **A new `Room` aggregate** (in `Belote.Table`, no ASP.NET types) owns participants, names, seat
   claims, host, connection state, the pause with per-player deadlines, bot replacement, seat
   reclaim, next-hand late join, play-again, and idle expiry. It is a single-reader actor like
   `LiveTable`, driven by an injected `TimeProvider`, so every timing rule is unit-testable with a
   fake clock.
3. **The server hosts many rooms.** `TableRegistry` (one global table) is replaced by a
   `RoomRegistry`; the existing hub gets room-oriented methods and pushes two messages: the existing
   per-seat `Update` (hidden-information projection unchanged) and a new room-wide `Room` state
   (seats, names, who is missing and until when, host, notices).
4. **Identity lives in the browser.** The client mints a random secret token once, keeps it with
   its name and last room in `localStorage`, and presents it on every (re)connect. The token is the
   only thing that proves "same person", so reload, tab-restore and network flaps all re-attach to
   the same participant and seat with no database.
5. **The web client gains routes and a lobby**: main menu (Start / Join / Return to your game),
   `/room/:id` deep links, a lobby with seat picking, name entry, share/copy, a reconnecting
   overlay, a pause overlay with countdown and "Play with a bot now", and a seat-relative table so
   whichever seat you hold is drawn at the bottom.

No database is introduced. Live room state is in server memory only; a server restart ends rooms
(the spec's flagged consequence). The legacy single-player hub methods are kept as a thin shim so
earlier features keep working while the web client moves to the room API.

## Technical Context

**Language/Version**: C# / .NET 10 (server, `Belote.Table`); TypeScript (strict) + React 19 (client)

**Primary Dependencies**: existing only: ASP.NET Core + SignalR, `@microsoft/signalr`, React,
Tailwind 4, Vite, `motion`. None added. Routing uses the History API directly (two routes do not
justify a router dependency).

**Storage**: none server-side beyond process memory. Client: `localStorage` (identity token, name,
last room, language) - per-viewer convenience data, always read inside try/catch.

**Testing**: xUnit + `FakeTimeProvider` (existing `Belote.Table.Tests` harness) for room/pause
logic; in-process SignalR end-to-end tests in `Belote.Server.Tests` with several clients; FsCheck
property for "any interleaving of disconnect/reconnect/skip never loses or duplicates a move";
Vitest + Testing Library for client state and screens. Playwright e2e stays opt-in (project
memory) - verification relies on `dotnet test`, Vitest and `tsc`.

**Target Platform**: modern evergreen browsers (phone-first); single ASP.NET Core process

**Project Type**: web application (existing `web/` client + .NET server)

**Performance Goals**: reconnect-and-resume in < 5 s after the connection is available (SC-002);
pause visible to others in < 2 s of a clean close (SC-003); 3+ concurrent 4-human rooms without
interference (SC-008)

**Constraints**: server-authoritative; per-seat projection only (no hidden-information leak,
SC-009); rules engine unchanged and still I/O-free; EN/BG strings for all new text; keyboard
operable; no horizontal scroll at phone width; in-memory state only

**Scale/Scope**: tens of concurrent rooms per process; ~1 new aggregate (`Room`), 1 registry, ~8
hub methods, 2 push messages, ~6 new client screens/components, seat-perspective refactor of the
table components

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (see end of section).*

| Principle | Status | How the plan satisfies it |
|-----------|--------|---------------------------|
| I. Pure Rules Engine | PASS | No change to `Belote.Core`. Rooms, timers, tokens and connections live in `Belote.Table`/`Belote.Server`. Time comes only from an injected `TimeProvider`; room ids and seeds are produced by the caller, not the engine. |
| II. Server-Authoritative | PASS | Every intent is still validated by `LiveTable` against engine + seat ownership; the client token is only used to map a connection to a participant. Each participant receives only `Projection.For(state, theirSeat)`; room state carries names, occupancy and connection status only. |
| III. Humans and Bots Interchangeable | PASS | Seat controller swap (human ↔ bot) is a single actor operation that does not change game state; bots continue to act only on their `SeatView`. `SmartBot` already derives its memory from the view (`CardMemory.From(view)`), so a bot taking over mid-hand needs no handed-over state. The engine never learns who controls a seat. |
| IV. Variants Are Configuration | PASS | Rule set still passed through `TableOptions`; the 60 s grace, 1 h idle expiry and bot pacing are configuration (`RoomOptions`), not literals in logic. |
| V. Test-First Game Logic | PASS | Room/pause/reclaim logic is specified as failing tests first (fake clock). A property test plus a simulated-disconnect game run to completion guard "no lost/duplicated move". The existing seeded bot-vs-bot simulation suite is untouched. |
| VI. Mobile-First and Accessible | PASS (with work) | New screens are phone-first, EN/BG, keyboard operable, with `aria-live` announcements for pause/resume/replace/reclaim. Information is never colour-only (status uses text + icon). |

**Technology constraints**: ASP.NET Core + SignalR + React/TS only - satisfied. Real-time
contracts are explicit, typed and versioned: protocol version is raised to 2 (see
`contracts/hub-protocol.md`).

**Gate result**: no violations; Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/007-rooms-multiplayer/
├── plan.md              # This file
├── research.md          # Phase 0 decisions
├── data-model.md        # Phase 1: Room, Seat, Participant, Pause, client storage
├── quickstart.md        # Phase 1: how to run and validate the scenarios
├── contracts/
│   ├── hub-protocol.md      # Hub methods, push messages, DTOs, error codes (protocol v2)
│   └── client-storage.md    # localStorage keys, URL forms, reconnect behaviour
└── tasks.md             # Phase 2 (/speckit-tasks - not created here)
```

### Source Code (repository root)

```text
src/
├── Belote.Core/                      # unchanged
├── Belote.Bots/                      # unchanged
├── Belote.Table/
│   ├── LiveTable.cs                  # CHANGED: dynamic seat controllers, room-driven pause, snapshot observers
│   ├── BotDriver.cs                  # CHANGED: plays whichever seats are bot-controlled; tolerates NotOwner after handover
│   ├── Intent.cs                     # CHANGED: HumanPrincipal(ParticipantId) alongside ConnectionPrincipal (kept for shim)
│   ├── TableOptions.cs               # unchanged
│   ├── Rooms/                        # NEW (no ASP.NET dependency)
│   │   ├── Room.cs                   #   actor: lobby, seats, host, pause, replace/reclaim, play again, expiry
│   │   ├── RoomOptions.cs            #   GraceSeconds=60, IdleExpiry=1h, MaxNameLength, TimeProvider
│   │   ├── RoomModel.cs              #   Participant, SeatSlot, PauseEntry, RoomLifecycle (immutable records)
│   │   ├── RoomState.cs              #   room-wide snapshot + RoomEvent (what the hub serialises)
│   │   ├── NameRules.cs              #   trim/length/uniqueness validation
│   │   └── RoomObserver.cs           #   ITableObserver successor: receives snapshot + per-seat projections
│   └── Views/                        # CHANGED: RoomStateDto & friends (additive); TableUpdate shape unchanged
└── Belote.Server/
    ├── RoomRegistry.cs               # NEW: id -> Room, token resolution, expiry sweep (replaces TableRegistry)
    ├── TableHub.cs                   # CHANGED: room methods added; legacy CreateTable/TakeSeat become a shim over rooms
    ├── ParticipantPump.cs            # CHANGED from ClientUpdatePump: per-participant, current connection swappable
    └── ServerHost.cs                 # CHANGED: register registry, SignalR keep-alive tuning, SPA fallback for /room/*

tests/
├── Belote.Table.Tests/Rooms/         # NEW: RoomLobbyTests, PauseAndReplaceTests, ReclaimTests, LateJoinTests,
│                                     #      ExpiryTests, HandoverPropertyTests (FakeTimeProvider)
└── Belote.Server.Tests/EndToEnd/     # NEW: MultiClientRoomTests, ReloadReconnectTests, TakeoverTests,
                                      #      ConcurrentRoomsTests, HiddenInfoTests

web/src/
├── routing/                          # NEW: tiny History-API router, parseRoomLink()
├── session/                          # NEW: identity token + saved session (localStorage, try/catch)
├── connection/hubClient.ts           # CHANGED: room methods, auto-rejoin on (re)connect, superseded handling
├── state/                            # CHANGED: room slice (RoomState, countdown clock offset), seat perspective
├── screens/                          # NEW: MainMenu, JoinGame, Lobby, RoomUnavailable
├── components/                       # NEW: ShareRoom, NameField, PauseOverlay, ReconnectingOverlay, SeatPicker
│                                     # CHANGED: Table/Seat/TrickArea/Hand/HandResultDialog use seat-relative layout + names
├── theme/geometry.ts                 # CHANGED: positions relative to the viewer's seat
├── protocol/types.ts                 # CHANGED: protocol v2 types
└── i18n/{en,bg}.ts                   # CHANGED: new strings
```

**Structure Decision**: extend the existing two-part layout. The room domain goes in
`Belote.Table` (already the home of live-play concerns and already free of ASP.NET types), not in
`Belote.Core` (must stay pure) and not in a new project (no second consumer yet). The server stays
a thin transport; the client keeps one folder per concern.

## Key Design Decisions

(Details and alternatives in [research.md](research.md).)

1. **Reclaim is atomic, not scheduled.** A controller swap is one work item in the table actor, so
   it is already "at a safe point": an intent is either applied before it or rejected after it. A
   bot decision computed for a seat that has just been reassigned is rejected `NotOwner`; the
   driver treats that as "stale, re-evaluate" instead of a fault.
2. **Pause is room-owned.** The table exposes `SetPaused(bool)` (the creator-only check goes
   away). The room computes `paused = missing humans exist` and owns the per-player deadlines;
   the table remains unaware of why it is paused.
3. **Deadlines are absolute and server-timed.** The room sends `deadlineUtc` plus `serverNowUtc`;
   clients display `deadline - (clientNow + offset)`, so reloads and clock skew cannot reset or
   skew a countdown (spec edge case).
4. **Re-attach on every connect.** SignalR automatic reconnect yields a new connection id, so the
   client calls `Rejoin(roomId, token)` after every `start`/`reconnected`. Connection ids are never
   identity.
5. **Late join and "play again" respect hand boundaries** using the table's deal number.
6. **Legacy shim.** `CreateTable` = create room + seat creator South; `TakeSeat` = host start with
   bots filling the rest; `SubmitIntent`/`GetView` resolve through the connection's participant.
   Tests that asserted "a new table replaces the old one" are retired because rooms are
   independent.

## Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| Silent network loss is detected late by SignalR (default 30 s), delaying the pause | Tune `KeepAliveInterval` 5 s / `ClientTimeoutInterval` 15 s; clean closes (reload, tab close) are immediate. SC-003 clarified accordingly. |
| Client assumes the viewer is South in many components | Introduce one `perspective` helper and migrate components in one slice before the lobby UI; keep South as the default so existing client tests stay green. |
| Memory growth from abandoned rooms | 1 h idle expiry sweep; rooms hold no logs beyond the game's event list. |
| Token theft grants a seat | Token is 128-bit random, never put in URLs or logged, only sent over the hub; room link alone never grants an existing participant's seat. Documented limitation: no accounts. |
| Two tabs fighting over a seat | Explicit `Superseded` push; the older tab stops auto-rejoining until the user presses "Take back". |
| Server restart loses rooms | Accepted and surfaced as "room has ended"; the point at which a durable store becomes necessary is documented in the spec. |

## Constitution Check (post-design)

Re-evaluated after Phase 1: unchanged - all principles PASS. The data model adds no engine
dependency, no clock reads outside `TimeProvider`, and no per-seat information beyond the existing
projection.

## Complexity Tracking

No constitution violations to justify.
