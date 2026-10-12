# Belote (Bulgarian) - headless rules engine and simulator

A deterministic, dependency-free Belote engine with a baseline bot and a seeded simulation runner.
It plays complete Bulgarian-belote games (bidding with contra/recontra, all three contract types,
announcements, belote, hanging points, all-tricks bonus, match to 151) with no UI or networking.

## Layout

| Project | Purpose |
|---------|---------|
| `src/Belote.Core` | Pure rules engine: no I/O, no clock, no ambient randomness; rule values live only in the `RuleSet` object |
| `src/Belote.Bots` | `RandomLegalBot`: picks uniformly among the legal actions of its seat's view |
| `src/Belote.Simulation` | Game runner, independent auditor, game log (JSON Lines), seeded batch runner, rule-set JSON |
| `src/Belote.Simulation.Cli` | `belote-sim` command line (`run`, `replay`, `audit`) |
| `src/Belote.Table` | `LiveTable`: one in-memory table (single-writer actor), per-seat views, bot driver, wire DTOs |
| `src/Belote.Server` | ASP.NET Core + SignalR hub (`/table`) over `Belote.Table`: one player (South) vs three bots |
| `tests/Belote.Core.Tests` | Unit tests per rule (`Rxx_*`), FsCheck properties |
| `tests/Belote.Simulation.Tests` | Integration, determinism, golden logs, mixed seats, large batches |
| `tests/Belote.Table.Tests` | Table rejections, privacy oracle, pacing/pause, concurrency, replay, equals-001 |
| `tests/Belote.Server.Tests` | End-to-end over a real SignalR connection, incl. 100 seeded games |

Design documents live in `specs/001-headless-belote-game/` (spec, plan, research, data model,
contracts, quickstart, tasks).

## Build and test

Requires the .NET 10 SDK.

```bash
dotnet build Belote.sln -c Release
dotnet test Belote.sln -c Release
```

`BELOTE_BATCH_GAMES` (default 1000) sizes the large-batch tests; CI's nightly job uses 10000.

## Run simulations

```bash
# 10,000 seeded all-bot games, audited, with a JSON report in ./sim-out
dotnet run --project src/Belote.Simulation.Cli -c Release -- run --games 10000 --seed 1

# Scripted "human" seats among bots (mixed-1..mixed-4) and illegal-move injection
dotnet run --project src/Belote.Simulation.Cli -c Release -- run --games 1000 --seats mixed-2
dotnet run --project src/Belote.Simulation.Cli -c Release -- run --games 1000 --seats scripted-illegal

# Reproduce one game from its seed, then audit the log
dotnet run --project src/Belote.Simulation.Cli -c Release -- replay --seed 1234 --out game.jsonl
dotnet run --project src/Belote.Simulation.Cli -c Release -- audit --log game.jsonl
```

Exit codes: `0` all games finished correctly, `1` at least one game failed a check, `2` invalid arguments.

## Live table (one player against three bots)

A headless server lets one connected client (South) play a full game against three
`RandomLegalBot` seats in real time. The client sends intents only; the server validates every
one with the rules engine and pushes that seat's view after each state change. Design and
protocol: `specs/002-live-table-vs-bots/` (see `contracts/hub-protocol.md`).

```bash
dotnet run --project src/Belote.Server -c Release -- --urls http://127.0.0.1:5080
```

| Setting | Meaning |
|---------|---------|
| `Table:BotDelayMs` | Default pause before each bot move in ms (default 600; clamped 0..5000; a client may override per table; tests use 0) |
| `Table:LogDirectory` | Optional directory that receives one JSON Lines log per finished game |

The hub is at `/table`. Methods: `CreateTable`, `TakeSeat`, `SubmitIntent`, `GetView`; the server
pushes `Update` messages. The end-to-end tests (`tests/Belote.Server.Tests`) are the reference
client and run in CI with the rest of the solution.

## Rule variants are configuration

All variant behaviour is in the immutable `RuleSet` record (`RuleSets.Bulgarian` is the default).
To try a variant, copy a rule set with `with { ... }` in code, or write it as JSON and pass
`--rules path.json`. The engine, bots and auditor read every value from the rule set; no rule value
is hard-coded elsewhere (a test enforces this).

```csharp
var shortGame = RuleSets.Bulgarian with
{
    Match = RuleSets.Bulgarian.Match with { TargetScore = 51 },
};
```

## Determinism

A game is a pure function of `(rule set, seed, seat decisions)`. Randomness comes only from the
in-library xoshiro256** generator (`SeededRandom`), so the same seed reproduces the same log
byte-for-byte on any machine or .NET version. Changing the generator or any rule value changes the
golden logs under `tests/Belote.Simulation.Tests/Golden` on purpose; regenerate with
`BELOTE_UPDATE_GOLDEN=1` and review the diff.
