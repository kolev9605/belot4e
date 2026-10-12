# Implementation Plan: Live Table vs Bots

**Branch**: `002-live-table-vs-bots` | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-live-table-vs-bots/spec.md`

## Summary

Put one in-memory, server-authoritative Belote table behind a persistent SignalR connection so
a single remote client (South) plays a full game against three `RandomLegalBot` seats using the
unchanged 001 rules engine.

The approach is two thin layers over 001:

1. **`Belote.Table`** (new, transport-agnostic library): a `LiveTable` actor that owns the
   `GameState`, serialises every intent through one single-reader queue, validates ownership /
   turn / legality via `GameEngine.Apply`, projects per-seat views, drives the bots through the
   same submit path as the human, and accumulates the 001 event log.
2. **`Belote.Server`** (new, ASP.NET Core + SignalR): a `TableHub` exposing `CreateTable`,
   `TakeSeat`, `SubmitIntent`, `GetView`, and pushing `Update` messages (the seat's view only,
   in version order).

No game rule, bot, or log format changes. Determinism is inherited by deriving the deal and
every bot seat's choices from the table seed exactly as 001's all-bot simulation does, so a
live game with South playing the baseline policy equals the 001 simulation of that seed move
for move.

## Technical Context

**Language/Version**: C# (`LangVersion latest`) on .NET 10 (`net10.0`), inherited from
`Directory.Build.props` (nullable on, warnings-as-errors for non-test projects).

**Primary Dependencies**: Existing `Belote.Core`, `Belote.Bots`, `Belote.Simulation` (for
`GameLog`, `GameAuditor`, JSON names). New: ASP.NET Core shared framework with SignalR (server);
`Microsoft.AspNetCore.SignalR.Client` (test client only); `Microsoft.Extensions.TimeProvider.Testing`
(tests only).

**Storage**: N/A. Table, state, and finished-game logs are held in memory only. An optional
directory sink for finished-game JSONL logs is a developer convenience, not persistence.

**Testing**: xUnit + FsCheck.Xunit (already used). In-process `LiveTable` tests plus
end-to-end tests that host the real server on loopback Kestrel (ephemeral port, WebSocket
transport) and drive it with a SignalR client.

**Target Platform**: Cross-platform .NET server process (developed on Windows 11; CI-agnostic).

**Project Type**: Web service (real-time hub) plus supporting class library; headless, no UI.

**Performance Goals**: 100 complete games over one connection, bot delay 0, finish within an
ordinary test pass (target under 60 s wall-clock on a developer machine). Per-intent
processing is in-memory and engine-bound.

**Constraints**: One intent processed at a time per table; updates in strict version order,
no gaps or duplicates; no hidden information in any message (FR-017); event log never sent to
the client (FR-025); game must not advance while the client is disconnected (FR-026).

**Scale/Scope**: One active table, one human, three bots. Multi-table, multi-human,
reconnect, timers, persistence are explicitly out of scope.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| Principle | Assessment | Status |
|-----------|-----------|--------|
| I. Pure Rules Engine | `Belote.Core` is untouched. All I/O, clocks, and the connection live in `Belote.Table` / `Belote.Server`. The only ambient randomness is choosing a seed when the client omits one, done in the server layer and then passed into the engine explicitly. | Pass |
| II. Server-Authoritative | Client sends intents only; every intent passes ownership, turn, and `GameEngine.Apply` validation. Per-seat `SeatView` projection is the only state shape sent; the log, seed, and other hands never leave the server. | Pass |
| III. Humans and Bots Interchangeable | Bots call the same `LiveTable.SubmitAsync` path as the hub, carrying a seat principal; the table only compares principals and never branches on bot vs. human. Bots receive only `SeatView`. | Pass |
| IV. Variants Are Configuration | The table takes a `RuleSet` (default `RuleSets.Bulgarian`) and records it in the log header; no rule values in the new code. | Pass |
| V. Test-First Game Logic | Rule logic is not changed. New behaviour (rejections, privacy, concurrency, pacing, replay) is specified test-first; the 100-game live run joins CI beside the existing simulations. | Pass |
| VI. Mobile-First and Accessible | Headless feature; no UI. Rejections carry stable machine `code`s so a later localized UI never depends on the English `detail` text. | Pass (N/A UI) |
| Technology Constraints | ASP.NET Core + SignalR; explicit, typed, versioned contract (`protocolVersion`). No technology outside the approved list. | Pass |

**Post-design re-check**: unchanged. The design adds two projects but no new technology and
no dependency from `Belote.Core` on anything. See Complexity Tracking (no violations).

## Project Structure

### Documentation (this feature)

```text
specs/002-live-table-vs-bots/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── hub-protocol.md  # SignalR methods, messages, rejection codes
│   └── table-api.md     # In-process LiveTable surface (used by hub, bots, tests)
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── Belote.Core/                 # unchanged
├── Belote.Bots/                 # unchanged (RandomLegalBot reused)
├── Belote.Simulation/           # one change: JsonNames becomes public so the wire
│                                #   vocabulary equals the log vocabulary
├── Belote.Table/                # NEW  (references Core, Bots, Simulation; no ASP.NET)
│   ├── LiveTable.cs             #   actor: queue, apply, publish, lifecycle, pause
│   ├── TableOptions.cs          #   seed, rule set, bot delay, TimeProvider
│   ├── Intent.cs                #   Intent, Principal, IntentOutcome, Rejection, codes
│   ├── BotDriver.cs             #   drives bot seats through LiveTable.SubmitAsync
│   ├── TableSnapshot.cs         #   immutable published state (version, state, last trick)
│   ├── Views/                   #   SeatViewDto, TableUpdateDto, mapper, legal-action DTOs
│   └── Wire/                    #   IntentDto parsing / malformed detection
└── Belote.Server/               # NEW  (ASP.NET Core web project)
    ├── TableHub.cs              #   CreateTable, TakeSeat, SubmitIntent, GetView
    ├── TableRegistry.cs         #   the single active table + finished-game log store
    ├── ClientUpdatePump.cs      #   per-connection ordered push of updates
    └── ServerHost.cs            #   Build(args, configure) used by Program and tests

tests/
├── Belote.Core.Tests/           # unchanged
├── Belote.Simulation.Tests/     # unchanged
├── Belote.Table.Tests/          # NEW
│   ├── Unit/                    #   rejections, state-unchanged, privacy, mapper, pacing, pause
│   ├── Integration/             #   100 games in-process, replay, equals 001, lifecycle
│   └── Concurrency/             #   bursts, duplicate card, client-vs-bot races
└── Belote.Server.Tests/         # NEW
    ├── Support/                 #   TableClient (SignalR), loopback server fixture
    └── EndToEnd/                #   100 games, wire privacy scan, scripted rejections, burst
```

**Structure Decision**: Keep table logic in a library with no ASP.NET dependency so
determinism, privacy, concurrency, and pacing are tested in-process and fast, and keep the
SignalR hub a thin adapter (Principle II/III: the transport is not where rules or seat logic
live). Two test projects mirror the two layers; the end-to-end project proves the real
persistent connection.

## Complexity Tracking

No constitution violations to justify. The two added projects are the minimum split that
keeps the actor testable without a web host.
