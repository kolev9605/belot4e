# Data Model: Headless Belote Game

All types live in `Belote.Core` unless noted and are immutable. Names are indicative of
shape and meaning, not final signatures.

## Cards

| Entity | Fields | Notes |
|--------|--------|-------|
| `Suit` | Clubs, Diamonds, Hearts, Spades | Bid ranking is **not** the enum order; it comes from the rule set |
| `Rank` | Seven, Eight, Nine, Ten, Jack, Queen, King, Ace | 32-card deck, 7 through Ace |
| `Card` | `Suit`, `Rank` | Value type; equality by both fields; unique within a deck |
| `Seat` | North, East, South, West | Clockwise order; `Partner(seat)` = opposite; `Team(seat)` = NS or EW |
| `Team` | NorthSouth, EastWest | |

## RuleSet

`RuleSet` (see [contracts/rule-set.md](contracts/rule-set.md)). Key members:

- `Name`, `Version`
- `Deal.Pattern` (3-2-3) and `Deal.BiddingAfterPacket` (2: bidding on 5 cards)
- `BidRanking` (ordered list of `ContractKind`: four suits, NoTrump, AllTrump)
- `ContraAllowed`, `RecontraAllowed`, `BiddingCloseRule`, `AllPassPolicy`
- `CardOrder[ContractKind]`, `CardPoints[ContractKind]`, `LastTrickBonus[ContractKind]`
- `MustFollowSuit`, `MustTrump`, `MustOvertrump`, `PartnerWinningExemption` flags per
  contract kind
- `Announcements`: definitions (kind, length/rank, points), `AllowedIn[ContractKind]`,
  `Timing`, `ComparisonOrder`
- `Belote`: points, `AppliesIn[ContractKind]`
- `Scoring`: `FailureRule`, `TiePolicy`, `AllTricksBonus`, `MultiplierRules`, `GamePointDivisor`,
  `RoundingMode`
- `Match`: `TargetScore`, `BothReachTargetRule`, `EqualTotalsRule`

Validation: card orders are permutations of the 8 ranks; card points + last-trick bonus sum
to a positive per-contract total; bid ranking has no duplicates; all referenced contract kinds
exist.

## Game

| Entity | Fields |
|--------|--------|
| `GameState` | `RuleSet`, `Seed`, `Phase` (`Bidding`, `Playing`, `Finished`), `Hand` (current `HandState`, includes `DealNumber` and `Dealer`), `MatchScore[Team]` (game points), `HeldOverPoints` (game points waiting for the next hand's winner), `Winner` (nullable team), `HandsScored`, `NextSequence` |
| `HandState` | `HandSeed`, `Hands[Seat]` (remaining cards), `Dealer`, `Bidding` (`BiddingState`), `Contract` (nullable), `Tricks` (completed), `CurrentTrick`, `ToAct` (seat), `Declared` (announcements), `RevealedAnnouncements`, `Won[Team]` (cards won), `AllCardsAccountedFor` helper |
| `BiddingState` | ordered `BidAction` history, `StandingBid` (nullable), `Declarer`, `ConsecutivePasses`, `Contra`/`Recontra` flags, `Closed` |
| `Contract` | `Kind` (`ContractKind`), `DeclaringTeam`, `Declarer` seat, `Multiplier` (1, 2, 4) |
| `ContractKind` | `Clubs`, `Diamonds`, `Hearts`, `Spades`, `NoTrump`, `AllTrump` |
| `Trick` | `Leader`, ordered `(Seat, Card)` plays (≤ 4), `LedSuit`, `Winner` (when complete) |
| `Announcement` | `Seat`, `Kind` (`Tierce`, `Quarte`, `Quinte`, `Carre`), `TopRank`, `Suit` (for sequences), `Cards`, `Points` |
| `BeloteBonus` | `Seat`, `Suit`, `Awarded` (both cards played) |
| `HandResult` | `Contract`, `CardPoints[Team]`, `LastTrickTeam`, `CountedAnnouncements[Team]`, `Belote[Team]`, `AllTricksTeam?`, `RawTotals[Team]`, `Outcome` (`Made`, `Failed`, `Tied`, `Voided`), `Multiplier`, `GamePointsAwarded[Team]`, `HeldOverBefore`, `HeldOverAfter` |

### State transitions

```text
Start ──deal──▶ Bidding ──contract fixed──▶ Playing ──8th trick──▶ (scored, in the same step)
                  │                                                  │
                  └─all pass─▶ (void, next dealer) ─▶ Bidding        ├─target reached─▶ Finished
                                                                     └─else──────────▶ Bidding (next hand)
```

Invariants (checked by tests and by the auditor):
1. `|hands| + |won cards| + |current trick| = 32` at every step of a hand.
2. Exactly one seat is `ToAct` in `Bidding` and `Playing`; `Finished` accepts no actions.
3. A `Contract` exists iff bidding closed with a bid; it is immutable once play starts.
4. Points awarded + change in held-over points = points in play for the hand.
5. `Winner` is set iff `Phase = Finished`; its match score ≥ `TargetScore`.

## Seat interface and views

| Entity | Fields / Meaning |
|--------|------------------|
| `PlayerAction` | One of: `Pass`, `Bid(ContractKind)`, `Contra`, `Recontra`, `Announce(AnnouncementKind, cards)`, `PlayCard(Card)` |
| `SeatView` | `Seat`, `OwnHand`, `Phase`, `Dealer`, `ToAct`, public bidding history, `Contract`, `CurrentTrick`, completed tricks (public cards), declared/revealed announcements, `MatchScore`, `HeldOverPoints`, `HandNumber`, `RuleSet` reference, and `LegalActions` (derived from the view itself) |
| `IPlayer` | `DecideAsync(SeatView, CancellationToken) → ValueTask<PlayerAction>` |

`SeatView` contains **no** other seat's unplayed cards and no undealt-card order or seed.

## Events (ordered log)

Each `GameEvent` has `Sequence` (monotonic from 0), `Type`, `HandNumber`, and type-specific
data. Types: `GameStarted`, `HandDealt`, `BidMade`, `PassMade`, `ContraMade`,
`RecontraMade`, `BiddingClosed`, `HandVoided`, `AnnouncementDeclared`,
`AnnouncementsRevealed`, `AnnouncementsCompared`, `CardPlayed`, `BeloteAwarded`,
`TrickWon`, `HandScored`, `PointsHeldOver`, `HeldOverAwarded`, `MatchScoreUpdated`,
`GameFinished`, plus rejection records `ActionRejected` (seat, action, reason) kept in the
run log (not part of engine state). Details in
[contracts/game-log-and-report.md](contracts/game-log-and-report.md).

## Simulation (in `Belote.Simulation`)

| Entity | Fields |
|--------|--------|
| `SeatAssignment` | `Seat` → controller factory (bot, scripted, fault-injecting) |
| `RunOptions` | `BaseSeed`, `GameCount`, `RuleSet`, `SeatAssignments` (or preset: all-bot / mixed *k*), `Parallelism`, `KeepLogs` (`None`/`FailuresOnly`/`All`), `MaxIllegalAttempts`, `DecisionTimeout` |
| `GameOutcome` | `Index`, `Seed`, `Finished` (bool), `Winner`, `FinalScore`, `HandsPlayed`, `FailureKind`, `FailureDetail`, `LogHash`, optional `Log` |
| `SimulationReport` | `Options` summary, `Games[GameOutcome]`, totals (passed, failed, by failure kind), `Elapsed` (runner metadata only; not part of any result hash) |
