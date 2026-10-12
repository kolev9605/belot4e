# Contract: `belote-sim` command line

Thin console wrapper over `Belote.Simulation`'s `SimulationRunner`. It is the only
user-facing entry point of this feature and exists for developers and CI.

## Usage

```text
belote-sim run    --games <N> [--seed <U64>] [--rules <path|bulgarian>]
                  [--seats <preset>] [--parallelism <N>] [--keep-logs none|failures|all]
                  [--out <dir>] [--max-illegal <N>] [--decision-timeout-ms <N>]
belote-sim replay --seed <U64> [--rules <path|bulgarian>] [--seats <preset>] [--out <file>]
belote-sim audit  --log <file>
```

| Option | Default | Meaning |
|--------|---------|---------|
| `--games` | required (run) | Number of games to simulate |
| `--seed` | `1` | Base seed; game *i* uses `Seeds.ForGame(base, i)` |
| `--rules` | `bulgarian` | Built-in name or path to a rule-set JSON file |
| `--seats` | `all-bot` | `all-bot`, `mixed-1`..`mixed-4` (that many scripted "human" seats, rest bots), or `scripted-illegal` (scripted seats inject illegal moves first) |
| `--parallelism` | CPU count | Games in flight |
| `--keep-logs` | `failures` | Which game logs to write under `--out` |
| `--out` | `./sim-out` | Output directory for the report and logs |
| `--max-illegal` | `3` | Illegal attempts allowed per decision before the game fails |
| `--decision-timeout-ms` | `5000` | Runner watchdog per decision |

## Commands

- **run**: plays *N* games; writes `report.json` and any retained logs; prints a one-screen
  summary (games, passed, failed, by failure kind, elapsed).
- **replay**: re-plays a single game from its seed and prints/writes its log; used to
  reproduce a reported failure (SC-006).
- **audit**: loads a saved log, replays it through the engine, runs all correctness checks,
  and reports the first offending event if any.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | All games finished correctly |
| 1 | At least one game failed a correctness check |
| 2 | Invalid arguments or invalid rule set |

## Output contract

`report.json` and logs follow [game-log-and-report.md](game-log-and-report.md). Console text
is for humans and is not a stable interface; scripts must read `report.json` or the exit
code.
