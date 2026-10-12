# Quickstart: Validating the Headless Belote Game

A run/validation guide. It does not contain implementation; see [plan.md](plan.md),
[data-model.md](data-model.md), and [contracts/](contracts/).

## Prerequisites

- .NET SDK 10.0.x (`dotnet --version` prints `10.0.*`).
- Repository root contains `Belote.sln` (created during implementation).

## 1. Build and run the test suite

```bash
dotnet build Belote.sln -c Release
dotnet test Belote.sln -c Release
```

Expected: build succeeds with zero warnings treated as errors; all unit, property, golden,
and integration tests pass.

## 2. Play one complete game (US1, SC-001)

```bash
dotnet run --project src/Belote.Simulation.Cli -c Release -- run --games 1 --seed 42 --keep-logs all --out ./sim-out
```

Expected: exit code 0; `sim-out/report.json` shows 1 game, `finished: true`, one winner whose
score is at or above 151; the log lists deals, bids, cards, tricks, hand scores, and the
final result in order.

## 3. Large all-bot batch (SC-001, SC-003, SC-007)

```bash
dotnet run --project src/Belote.Simulation.Cli -c Release -- run --games 10000 --seed 1
```

Expected: exit code 0; summary `finishedCorrectly = 10000`, `failed = 0`. Note the elapsed
time; 1,000 games must complete in under 5 minutes (SC-007).

## 4. Reproducibility (US6, SC-002)

```bash
dotnet run --project src/Belote.Simulation.Cli -c Release -- replay --seed 1234 --out ./a.jsonl
dotnet run --project src/Belote.Simulation.Cli -c Release -- replay --seed 1234 --out ./b.jsonl
```

Expected: `a.jsonl` and `b.jsonl` are byte-for-byte identical.

## 5. Mixed bot and scripted "human" seats (US7, SC-004)

```bash
dotnet run --project src/Belote.Simulation.Cli -c Release -- run --games 1000 --seed 7 --seats mixed-1
dotnet run --project src/Belote.Simulation.Cli -c Release -- run --games 1000 --seed 7 --seats mixed-2
dotnet run --project src/Belote.Simulation.Cli -c Release -- run --games 1000 --seed 7 --seats mixed-3
dotnet run --project src/Belote.Simulation.Cli -c Release -- run --games 1000 --seed 7 --seats mixed-4
dotnet run --project src/Belote.Simulation.Cli -c Release -- run --games 1000 --seed 7 --seats scripted-illegal
```

Expected: exit code 0 for each; the `scripted-illegal` run shows rejected moves recorded in
the run logs yet every game finishes correctly (illegal moves never change state).

## 6. Failure diagnosis (SC-006)

If any run exits with code 1, take the failing game's `seed` from `report.json`:

```bash
dotnet run --project src/Belote.Simulation.Cli -c Release -- replay --seed <seed> --out ./failing.jsonl
dotnet run --project src/Belote.Simulation.Cli -c Release -- audit --log ./failing.jsonl
```

Expected: `audit` names the first offending event and the violated check.

## 7. Spot checks that map to the clarified rules

Covered by named unit tests (see traceability in [research.md](research.md)); each is
also observable in a log:

| Rule | Where to see it |
|------|-----------------|
| Failed contract: declarer 0, defenders get all points ×multiplier | `HandScored` with `outcome: "failed"` |
| Tied hand: declarer's points held over to next hand's winner | `PointsHeldOver` then `HeldOverAwarded` |
| All eight tricks: +90 bonus | `HandScored` with all-tricks flag |
| All-pass: voided, next dealer redeals | `HandVoided` followed by `HandDealt` with next dealer |
| Both reach target: higher total wins | `GameFinished` after a hand where both ≥ target |

## Done criteria for this feature

- Steps 1–5 pass; step 6 is not needed (no failures).
- Every row R1–R25 in research.md has at least one passing unit test; rows marked *verify*
  have their source recorded.
