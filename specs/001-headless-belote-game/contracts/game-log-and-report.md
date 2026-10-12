# Contract: Game log and simulation report

Both are language-neutral, deterministic data. JSON Lines is the on-disk form for logs and
a single JSON document for the batch report. In-memory forms are the `GameEvent` hierarchy
and `SimulationReport` types in [data-model.md](../data-model.md).

## Game log (JSON Lines, one event per line)

Line 1 is always a header; subsequent lines are events in strict `sequence` order.

```json
{"type":"header","logVersion":1,"seed":42,"ruleSet":{ "...full rule set..." },
 "seats":{"north":"RandomLegalBot","east":"RandomLegalBot","south":"RandomLegalBot","west":"RandomLegalBot"}}
{"seq":0,"hand":1,"type":"GameStarted"}
{"seq":1,"hand":1,"type":"HandDealt","dealer":"north","hands":{"north":["JH","9H", "..."],"east":[],"south":[],"west":[]}}
{"seq":2,"hand":1,"type":"BidMade","seat":"east","bid":"hearts"}
{"seq":3,"hand":1,"type":"PassMade","seat":"south"}
{"seq":4,"hand":1,"type":"ContraMade","seat":"west"}
{"seq":5,"hand":1,"type":"BiddingClosed","contract":"hearts","declarer":"east","multiplier":2}
{"seq":6,"hand":1,"type":"AnnouncementDeclared","seat":"east","kind":"tierce"}
{"seq":7,"hand":1,"type":"CardPlayed","seat":"south","card":"AS","trick":1}
{"seq":8,"hand":1,"type":"TrickWon","winner":"east","trick":1,"points":21}
{"seq":9,"hand":1,"type":"AnnouncementsRevealed","teams":{"eastWest":[{"kind":"tierce","top":"Q","suit":"hearts","points":20}],"northSouth":[]}}
{"seq":10,"hand":1,"type":"AnnouncementsCompared","countingTeam":"eastWest"}
{"seq":11,"hand":1,"type":"HandScored","outcome":"made","rawTotals":{"northSouth":71,"eastWest":91},"multiplier":2,"gamePoints":{"northSouth":14,"eastWest":18},"heldOver":0}
{"seq":12,"hand":1,"type":"MatchScoreUpdated","score":{"northSouth":14,"eastWest":18}}
{"seq":99,"hand":9,"type":"GameFinished","winner":"eastWest","score":{"northSouth":131,"eastWest":156}}
```

Card notation: rank (`7 8 9 10 J Q K A`) followed by suit letter (`C D H S`).

Rules:
- The log is **omniscient** (it contains all dealt hands) and is a developer/test artifact.
  It must never be sent to a seat controller or client.
- `ActionRejected` records (`seat`, `action`, `reason`) may appear in run logs between events
  for debugging; they carry no sequence number and do not affect engine state or the log
  hash.
- **Reconstructability**: the sequence of accepted `(seat, action)` pairs plus header
  `(seed, ruleSet)` suffices to replay the game exactly; the auditor does this.
- **Hash**: `LogHash` = SHA-256 over the canonical UTF-8 bytes of the header and event lines
  (excluding `ActionRejected` records and any timing metadata). Used for the
  same-seed-same-game assertion (SC-002).
- No localized text appears in the log; only codes.

## Simulation report (JSON document)

```json
{
  "reportVersion": 1,
  "options": { "baseSeed": 1, "games": 10000, "ruleSet": "bulgarian", "seats": "all-bot" },
  "summary": { "games": 10000, "finishedCorrectly": 10000, "failed": 0, "byFailureKind": {} },
  "games": [
    { "index": 0, "seed": 8123123, "finished": true, "winner": "eastWest",
      "finalScore": { "northSouth": 120, "eastWest": 158 }, "hands": 11,
      "logHash": "ab12...", "failureKind": null, "failureDetail": null }
  ],
  "runner": { "elapsedMs": 41230, "parallelism": 8 }
}
```

- `games[i].seed` is derived deterministically from `baseSeed` and `index`
  (`Seeds.ForGame`), so any single game can be re-run in isolation.
- `failureKind` values: `RuleViolationDetected`, `AuditMismatch`, `ControllerException`,
  `ControllerTimeout`, `TooManyIllegalAttempts`, `NoWinner`, `ExceededHandLimit`,
  `ReplayMismatch`.
- Logs of failed games are always retained (path or inline per options); logs of passing
  games are kept per `keepLogs`.
- `runner.*` is metadata and excluded from any determinism comparison.
- Exit semantics are in [simulation-cli.md](simulation-cli.md).
