# Quickstart: validating the Smart Bot AI

Prerequisites: .NET 10 SDK. Commands run from the repository root. Nothing here needs Node or
a browser except step 5.

## 1. Build and run the whole test suite

```bash
dotnet build Belote.sln
dotnet test Belote.sln
```

Expect: all projects green, including the new `Belote.Bots.Tests`. This covers legality,
determinism, per-rule behaviour (bidding, feeding, cheapest win, discard, announcements) and the
blunder audit. (FR-001..FR-010, SC-002, SC-003, SC-005, SC-006)

## 2. Prove it is stronger than random

```bash
dotnet run --project src/Belote.Simulation.Cli -- bench --games 1000 --seed 1 --min-win-rate 0.80
```

Expect: `smart team wins` at or above 80 %, `failed games 0`, exit code 0. Re-run with
`--seed 2` and `--seed 3`; results differ but all clear the bar. Run the same command twice and
compare the JSON from `--out`: identical. (SC-001, SC-003, FR-014)

## 3. Smart bots in every seat finish legally

```bash
dotnet run --project src/Belote.Simulation.Cli -- run --games 500 --seed 7 --seats all-smart
```

Expect: 500 passed, 0 failed (every game audited). (SC-002)

## 4. Existing random-only behaviour is unchanged

```bash
dotnet run --project src/Belote.Simulation.Cli -- run --games 100 --seed 1 --seats all-bot
```

Expect: output and log hashes identical to those recorded before this feature (golden logs in
`tests/Belote.Simulation.Tests/Golden` still pass in step 1). (FR-013)

## 5. See it in the live table

```bash
dotnet run --project src/Belote.Server
cd web && npm run dev
```

Open the client, start a game, and watch the three bots. Check, informally:

- bots pass on weak hands and bid on strong ones;
- a bot feeds an ace or ten to a trick its partner is winning;
- a bot wins a trick with the cheapest card that works;
- play stays paced by the configured bot delay.

Then open the finished game's log (server `Table:LogDirectory`) and confirm the bot seats are
labelled `SmartBot`. (FR-011, FR-012, User Story 3)

## 6. Timing check

```bash
dotnet run --project src/Belote.Simulation.Cli -- run --games 200 --seed 1 --seats all-bot
dotnet run --project src/Belote.Simulation.Cli -- run --games 200 --seed 1 --seats all-smart
```

Expect: elapsed time **per scored hand** (`elapsed` divided by total hands in `report.json`) for
`all-smart` is no more than about twice that of `all-bot`. Smart games last more hands than random
games, so compare per hand, not per game. (SC-004)
