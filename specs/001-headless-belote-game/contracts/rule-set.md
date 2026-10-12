# Contract: Rule set

A `RuleSet` is the single place where variant rules live (constitution Principle IV). It is an
immutable record graph passed explicitly to every engine entry point and copied into every game log
header. Engine, bot, auditor and UI code MUST NOT contain rule values; a test
(`Rule_values_are_defined_only_in_the_rule_set_file`) scans Core for the Bulgarian totals.

## Shape

The JSON form (read and written by `Belote.Simulation.RuleSetJson`, used by `--rules path.json`) mirrors
the in-memory records exactly. Conventions:

- Point arrays are indexed by rank in natural order: `7, 8, 9, 10, J, Q, K, A`.
- Orders are listed strongest first.
- `deal.biddingAfterPacket` is the number of packets dealt before bidding (2 = after 5 cards).

This is the shipped Bulgarian rule set (`RuleSets.Bulgarian`), as written into every log header:

```json
{
  "name": "bulgarian",
  "version": 1,
  "deal": {
    "pattern": [
      3,
      2,
      3
    ],
    "biddingAfterPacket": 2
  },
  "bidding": {
    "ranking": [
      "clubs",
      "diamonds",
      "hearts",
      "spades",
      "noTrump",
      "allTrump"
    ],
    "contra": true,
    "recontra": true,
    "closeAfterConsecutivePasses": 3,
    "closeOnRecontra": true,
    "allPass": "VoidAndRedealNextDealer"
  },
  "contracts": {
    "suit": {
      "trump": {
        "order": [
          "J",
          "9",
          "A",
          "10",
          "K",
          "Q",
          "8",
          "7"
        ],
        "points": [
          0,
          0,
          14,
          10,
          20,
          3,
          4,
          11
        ]
      },
      "plain": {
        "order": [
          "A",
          "10",
          "K",
          "Q",
          "J",
          "9",
          "8",
          "7"
        ],
        "points": [
          0,
          0,
          0,
          10,
          2,
          3,
          4,
          11
        ]
      },
      "lastTrickBonus": 10
    },
    "noTrump": {
      "table": {
        "order": [
          "A",
          "10",
          "K",
          "Q",
          "J",
          "9",
          "8",
          "7"
        ],
        "points": [
          0,
          0,
          0,
          20,
          4,
          6,
          8,
          22
        ]
      },
      "lastTrickBonus": 20
    },
    "allTrump": {
      "table": {
        "order": [
          "J",
          "9",
          "A",
          "10",
          "K",
          "Q",
          "8",
          "7"
        ],
        "points": [
          0,
          0,
          14,
          10,
          20,
          3,
          4,
          11
        ]
      },
      "lastTrickBonus": 10
    }
  },
  "play": {
    "mustFollowSuit": true,
    "cutOnlyWhenOpponentWinning": true,
    "mustOvertrump": true,
    "mustOvertakeInAllTrump": true,
    "anyCardWhenCannotOvertrump": true
  },
  "announcements": {
    "allowedInSuit": true,
    "allowedInNoTrump": false,
    "allowedInAllTrump": true,
    "sequences": [
      {
        "minLength": 3,
        "points": 20
      },
      {
        "minLength": 4,
        "points": 50
      },
      {
        "minLength": 5,
        "points": 100
      }
    ],
    "carrePoints": [
      0,
      0,
      150,
      100,
      200,
      100,
      100,
      100
    ],
    "carreOrder": [
      "J",
      "9",
      "A",
      "10",
      "K",
      "Q"
    ],
    "belotePoints": 20,
    "beloteInSuit": true,
    "beloteInNoTrump": false,
    "beloteInAllTrump": true,
    "comparison": "PerCategory",
    "revealAfterTrick": 1
  },
  "scoring": {
    "contraMultiplier": 2,
    "recontraMultiplier": 4,
    "winnerTakesAllUnderMultiplier": true,
    "allTricksBonus": 90,
    "gamePointDivisor": 10,
    "rounding": "LargestRemainder",
    "roundingTieFavorsFewerPoints": true
  },
  "match": {
    "targetScore": 151,
    "allTricksCannotEndMatch": true
  }
}
```

## Semantics

- **Multipliers**: `contraMultiplier` / `recontraMultiplier` apply to the hand total. With
  `winnerTakesAllUnderMultiplier`, the team with more points scores the entire pool times the
  multiplier and the other team scores nothing.
- **Failure**: the declaring team scores strictly fewer raw points than the defenders; the
  defenders then receive the whole pool (times the multiplier). There is no separate threshold.
- **Tie**: equal raw points; the defenders keep their share, the declarer's share is held over and
  awarded to the team that wins a later hand (held points stack across consecutive ties).
- **All tricks**: `scoring.allTricksBonus` raw points are added to the team that wins all eight
  tricks; `match.allTricksCannotEndMatch` stops such a hand from ending the match.
- **Game points**: `gamePointDivisor` converts raw points. The pool is the sum of both teams'
  amounts divided by the divisor, rounded half up. Each team receives the floor of its own share and
  leftover points go to the largest remainders; ties go to the team with fewer points
  (`roundingTieFavorsFewerPoints`) or, in a tie hand, to the declarer whose points hang.
- **Announcements**: a seat may declare, with its first card, any detected combination whose cards do
  not overlap with its earlier declarations. `comparison: PerCategory` compares sequences with
  sequences (length, then top rank; identical cancels) and four-of-a-kinds by `carreOrder`.
  Declarations are compared once, after trick `revealAfterTrick`.
- **Play rules**: see `PlayRules`; `cutOnlyWhenOpponentWinning` exempts a seat from trumping when
  its partner is winning, `anyCardWhenCannotOvertrump` lets a seat that cannot beat a trump discard.

## Validation (`RuleSetValidator`)

- Each card table's order is a permutation of the eight ranks and has eight non-negative points.
- `bidding.ranking` lists each contract kind exactly once.
- `deal.pattern` entries are positive and sum to 8; `biddingAfterPacket` is within the packets.
- `closeAfterConsecutivePasses` is 1, 2 or 3; multipliers are positive with recontra >= contra.
- `announcements.carrePoints` has 8 values; sequence minimum lengths are distinct.
- `gamePointDivisor` and `match.targetScore` are positive.
- Points in play per contract kind are positive (`PointsInPlay`, used by the audit).

Invalid rule sets are rejected at engine start (`ArgumentException` listing every issue) and by the
CLI (exit code 2); they never reach play.

## Versioning and variants

`version` increments when any value changes. Logs record the full rule set, so old logs stay
interpretable. A variant is a new rule-set instance: the test suite builds target-score, no-contra,
no-announcement, bid-ranking, deal-pattern, overtrump, point-table and bonus variants with `with`
expressions only, and runs them through the independent audit.
