# Contract: `belote-sim bench`

A new verb on the existing simulation CLI that measures the smart bot against the random
baseline.

## Usage

```text
belote-sim bench --games <N> [--seed <U64>] [--rules <path|bulgarian>]
                 [--parallelism <N>] [--out <file>] [--min-win-rate <0..1>]
```

| Option | Default | Meaning |
|--------|---------|---------|
| `--games` | required | Total games; split evenly between `smart-ns` and `smart-ew` (odd remainder goes to `smart-ns`). |
| `--seed` | `1` | Base seed; game `i` uses `Seeds.ForGame(seed, i)` (same derivation as `run`). |
| `--rules` | `bulgarian` | Rule set, as for `run`. |
| `--parallelism` | CPU count | As for `run`. |
| `--out` | none | Write the full JSON result to this file. |
| `--min-win-rate` | none | When given, exit code `1` if `smartWinRate` is below it. |

Every game is audited exactly as in `run`; a failed game counts in `failed` and forces exit
code `1` regardless of `--min-win-rate`.

## Output

Human summary on stdout, for example:

```text
games 1000  seed 1  rules bulgarian
smart team wins   9xx   (9x.x%)
random team wins    xx
mean score margin  +NNN
failed games          0
```

JSON (`--out`):

```json
{
  "baseSeed": 1,
  "gameCount": 1000,
  "smartWins": 0,
  "randomWins": 0,
  "smartWinRate": 0.0,
  "meanScoreMargin": 0.0,
  "failed": 0,
  "games": [
    { "index": 0, "seed": 0, "smartTeam": "northSouth", "winner": "northSouth",
      "northSouth": 0, "eastWest": 0 }
  ]
}
```

Numbers above are placeholders; real values are produced by the run.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Completed, no failed games, `--min-win-rate` (if given) met |
| 1 | A game failed audit, or the win rate was below `--min-win-rate` |
| 2 | Invalid arguments (same convention as other verbs) |

## Determinism

Same arguments yield byte-identical JSON on any machine, independent of `--parallelism`
(games are independent and reported by index).
