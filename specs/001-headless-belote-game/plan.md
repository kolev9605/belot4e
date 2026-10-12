# Implementation Plan: Headless Belote Game

**Branch**: `001-headless-belote-game` | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-headless-belote-game/spec.md`

## Summary

Deliver a complete, deterministic, headless Bulgarian-Belote game: a dependency-free rules
engine (`Belote.Core`) that models a game as an immutable state machine driven by seat actions,
a per-seat projected view, a rule-set object holding every variant value, a random-legal-move
baseline bot, a seeded simulation runner with a correctness auditor, and an ordered event log
that can reconstruct any game. Quality is proven by unit tests per rule, property-based tests
for invariants, and large seeded bot-vs-bot and mixed (scripted "human") simulation batches.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (LTS). `net10.0` target for every project.

**Primary Dependencies**: `Belote.Core` and `Belote.Bots` have **none** beyond the .NET base
class library. Test projects: xUnit, FsCheck (property-based), FluentAssertions-free plain
asserts. CLI: `System.CommandLine` is avoided; plain argument parsing keeps it dependency-free.

**Storage**: N/A (in-memory). The simulation CLI may write game logs and reports to files as
JSON / JSON Lines; `Belote.Core` never touches I/O.

**Testing**: xUnit (unit + integration), FsCheck.Xunit (properties), golden-log snapshot test,
CI simulation batches (see Quickstart).

**Target Platform**: Cross-platform .NET (Windows, Linux, macOS); runs headless in CI.

**Project Type**: Library + console tool (no UI, no network).

**Performance Goals**: 1,000 all-bot games in under 5 minutes single machine (SC-007); design
target is ≥ 10x faster than that budget so 10,000-game batches stay practical (SC-001).

**Constraints**: Fully deterministic (no wall clock, no ambient randomness, no unordered
iteration over hash sets in the engine); immutable state; no hidden information in per-seat
views; the engine never knows whether a seat is a bot.

**Scale/Scope**: 4 seats, 32 cards, 8 tricks per hand, tens of hands per game, up to tens of
thousands of games per batch. One rule set shipped (Bulgarian); mechanism supports more.

**Prerequisite**: .NET SDK 10.0.x (verified: 10.0.401 installed on the development machine).
If `dotnet` is not found in a shell, restart it so the updated PATH is picked up.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Pre-research | Post-design |
|-----------|------|--------------|-------------|
| I. Pure Rules Engine | All rules live in dependency-free `Belote.Core`; no I/O, clock, or ambient randomness; seeded only | PASS | PASS: own PRNG in Core (`SeededRandom`), no `System.Random`, no `DateTime`, no file/console access; rule set is a plain object; I/O (JSON, files, console) lives in `Belote.Simulation` |
| II. Server-Authoritative | Hidden info only to entitled seats; per-player projection; engine validates every intent | PASS (foundation) | PASS: `SeatView` projection is the only thing controllers see; engine validates every `PlayerAction`. Networking itself is out of scope |
| III. Humans and Bots Interchangeable | One player interface; bot acts on projected view; engine blind to seat type | PASS | PASS: `IPlayer` consumes `SeatView` only; `GameEngine` has no reference to `IPlayer`; legal moves are computed from the view |
| IV. Variants Are Configuration | No hard-coded rule values; rule set is explicit input and recorded with game | PASS | PASS: `RuleSet` passed to every engine call and stored in the game log header; defaults live only in `RuleSets.Bulgarian` |
| V. Test-First Game Logic | Unit tests per rule; property tests for invariants; bot-vs-bot simulations in CI | PASS | PASS: rule-to-test traceability matrix in research.md; FsCheck properties; CI simulation batches |
| VI. Mobile-First and Accessible | UI, i18n, accessibility | N/A (no UI) | N/A. Log/reporting uses language-neutral codes so a later UI can localize |

Technology constraints: ASP.NET Core / SignalR / React are not used by this feature (no
networking or UI in scope); `Belote.Core` is reusable by the later ASP.NET back-end unchanged.

No violations; Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/001-headless-belote-game/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── core-api.md
│   ├── rule-set.md
│   ├── game-log-and-report.md
│   └── simulation-cli.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
Belote.sln
src/
├── Belote.Core/                  # Pure rules engine (no dependencies)
│   ├── Cards/                    # Suit, Rank, Card, Deck
│   ├── Rules/                    # RuleSet, RuleSets.Bulgarian, RuleSetValidator
│   ├── Random/                   # SeededRandom (deterministic PRNG), shuffle
│   ├── Bidding/                  # Bid, Contract, bidding state machine
│   ├── Play/                     # Trick, legal-move computation, trick resolution
│   ├── Announcements/            # Detection, comparison, belote
│   ├── Scoring/                  # Hand scoring, match scoring, hanging points
│   ├── State/                    # GameState, HandState, SeatView, projection
│   ├── Events/                   # GameEvent hierarchy (ordered log entries)
│   ├── Engine/                   # GameEngine.Start / Apply, Result types
│   └── Players/                  # IPlayer, PlayerAction (the seat interface)
├── Belote.Bots/                  # RandomLegalBot
├── Belote.Simulation/            # GameRunner, SimulationRunner, GameAuditor, report,
│                                 # ScriptedPlayer, fault-injecting wrapper, JSON I/O
└── Belote.Simulation.Cli/        # `belote-sim` console tool
tests/
├── Belote.Core.Tests/
│   ├── Unit/                     # per-rule tests (cards, bidding, legality, announcements, scoring)
│   ├── Properties/               # FsCheck invariants
│   └── Golden/                   # committed golden logs for fixed seeds
└── Belote.Simulation.Tests/
    ├── Integration/              # full games: all-bot, mixed seats, illegal-move injection
    └── Batches/                  # large seeded batches (size from environment variable)
```

**Structure Decision**: Single solution with four source projects and two test projects.
`Belote.Core` is isolated (constitution Principle I) and referenced by every other project;
nothing references anything that would invert that. Bots are in their own assembly to prove
they only need the public player interface and the projected view.

## Complexity Tracking

> No constitution violations; nothing to justify.
