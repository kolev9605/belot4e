# Implementation Plan: Smart Bot AI

**Branch**: `004-smart-bot-ai` | **Date**: 2026-10-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/004-smart-bot-ai/spec.md`

## Summary

Add one new seat controller, `SmartBot`, to the existing `Belote.Bots` library. It implements
the same `IPlayer` interface as `RandomLegalBot`, decides from its `SeatView` alone, and replaces
random choice with calculated heuristics: per-contract hand evaluation for bidding, card memory
derived from the public trick history, and a small prioritised rule set for card play and
announcements. It is stateless between calls (everything is recomputed from the view), so it is
deterministic and cannot carry stale state across a voided redeal.

Around the bot: (1) new seat presets and a `Benchmark` runner plus a `belote-sim bench` command
so the "at least 80 % against random" bar is repeatable; (2) the live table's default bot factory
switches to `SmartBot`; (3) existing parity tests that compared a live table to an all-random
simulation are repointed at a new `smart-table` preset (South random, others smart). No change
to `Belote.Core`, the hub protocol, the wire DTOs or the web client.

## Technical Context

**Language/Version**: C# / .NET 10 (as the rest of the solution)

**Primary Dependencies**: none new. `Belote.Bots` references only `Belote.Core` (enforced by an
existing assembly-reference test, extended to cover `SmartBot`)

**Storage**: N/A

**Testing**: xUnit + FsCheck (as `Belote.Simulation.Tests`); new `tests/Belote.Bots.Tests`
project for bot unit/property tests; simulation-level tests (benchmark, legality across variants)
use `Belote.Simulation`

**Target Platform**: any .NET 10 host; runs inside the server process and the simulation CLI

**Project Type**: library addition to an existing multi-project .NET solution (plus CLI command)

**Performance Goals**: one decision well under 1 ms typical (no search; bounded loops over at
most 8 cards, 32-card memory); a zero-delay full game at most 2x the random-bot time (SC-004)

**Constraints**: deterministic; seat-view-only inputs; legal-by-construction (choose only from
`LegalActions.For(view)`); no new dependencies, I/O, clock or randomness in the bot

**Scale/Scope**: 1 bot class + ~4 internal helpers (~600 LOC), 1 options record, 3-4 seat
presets, 1 benchmark runner + CLI verb, ~6 test files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle / Constraint | Status | Notes |
|------------------------|--------|-------|
| I. Pure Rules Engine | Pass | `Belote.Core` is untouched. The bot lives in `Belote.Bots`, which still references only `Belote.Core`. |
| II. Server-Authoritative | Pass | Bot moves still enter the table as intents and are validated like a human's. No wire or projection change, so no new hidden-information exposure. |
| III. Humans and Bots Interchangeable | Pass | `SmartBot : IPlayer`, input is `SeatView` only; table and engine do not branch on bot type (only the default factory changes). Card memory is rebuilt from public history in the view. |
| IV. Variants Are Configuration | Pass | Card strengths, points, trump identity and legality come from `RuleSet` via `CardStrength`, `LegalActions`, `RuleSet.CardPoints`; no rank values hard-coded. Heuristic weights live in one `SmartBotOptions` record (tuning, not rules). |
| V. Test-First Game Logic | Pass | Tests are written before each heuristic. Property test: legal across seeds and rule variants. Bot-vs-bot simulations (all-smart, smart vs random) added to the suite; failures reproducible from seed. |
| VI. Mobile-First and Accessible | N/A | No UI change. |
| Tech constraints | Pass | No technology outside the list is introduced. |

**Post-design re-check**: unchanged; the design adds no violations and needs no Complexity
Tracking entries.

## Project Structure

### Documentation (this feature)

```text
specs/004-smart-bot-ai/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── bot-api.md          # SmartBot / options / seat-preset surface
│   └── benchmark-cli.md    # `belote-sim bench` command and report shape
├── checklists/requirements.md
└── tasks.md                # /speckit-tasks (not created here)
```

### Source Code (repository root)

```text
src/
├── Belote.Bots/
│   ├── RandomLegalBot.cs            # unchanged
│   ├── SmartBot.cs                  # NEW: IPlayer; dispatch bidding / play / announce
│   ├── SmartBotOptions.cs           # NEW: thresholds and weights (the single difficulty)
│   ├── HandEvaluator.cs             # NEW: per-ContractKind strength estimate for bidding
│   ├── CardMemory.cs                # NEW: played/unseen cards, voids, master-card test
│   └── TrickSituation.cs            # NEW: who is winning, partner status, points, position
├── Belote.Simulation/
│   ├── SeatPresets.cs               # + all-smart, smart-ns, smart-ew, smart-table
│   └── Benchmark.cs                 # NEW: seeded smart-vs-random runner and result record
├── Belote.Simulation.Cli/
│   └── CliApp.cs                    # + `bench` verb, preset list in usage text
└── Belote.Table/
    └── LiveTable.cs                 # DefaultBot -> SmartBot; seat label -> nameof(SmartBot)

tests/
├── Belote.Bots.Tests/               # NEW project
│   ├── HandEvaluatorTests.cs
│   ├── CardMemoryTests.cs
│   ├── BiddingBehaviourTests.cs
│   ├── PlayBehaviourTests.cs
│   ├── DeterminismAndLegalityTests.cs
│   └── Support/ViewBuilder.cs
├── Belote.Simulation.Tests/         # + benchmark threshold test, blunder-audit test, preset tests
├── Belote.Table.Tests/              # parity tests repointed to smart-table; label assertions
└── Belote.Server.Tests/             # HundredSeededGames repointed to smart-table
```

**Structure Decision**: extend the existing single-solution layout. The bot stays in
`Belote.Bots` (the home Principle III implies for controllers), the measurement harness stays in
`Belote.Simulation`, and the table keeps depending on `Belote.Bots` only through its default bot
factory. A dedicated `Belote.Bots.Tests` project is added because bot behaviour tests need no
simulation or server references and run in milliseconds.

## Complexity Tracking

No Constitution Check violations; nothing to justify.
