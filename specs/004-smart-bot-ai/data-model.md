# Data Model: Smart Bot AI

All types are in-memory, immutable where practical, and created per decision. Nothing is
persisted or sent over the wire; the existing `SeatView`, `PlayerAction`, event log and hub DTOs
are unchanged.

## SmartBot

A seat controller (`IPlayer`).

| Member | Description |
|--------|-------------|
| `SmartBot(SmartBotOptions? options = null)` | Uses `SmartBotOptions.Standard` when `null`. Holds no per-game state. |
| `DecideAsync(SeatView, CancellationToken)` | Returns one action from `LegalActions.For(view)`; throws `InvalidOperationException` if the list is empty (same contract as `RandomLegalBot`). Completes synchronously. |

**Validation**: the returned action MUST satisfy `LegalActions.Check(view, action) is null`.

## SmartBotOptions

The single difficulty's tunable weights. All values are plain numbers with documented units
(estimated card points). Defaults are chosen by benchmarking; names below are indicative.

| Field | Meaning |
|-------|---------|
| `BidThreshold` | Minimum best-contract score to bid instead of pass. |
| `OvercallPartnerMargin` | Extra score required to outbid the partner's standing bid. |
| `ContraThreshold` | Minimum defensive score to double an opposing suit contract. |
| `RecontraThreshold` | Minimum score to redouble the team's own doubled contract. |
| `TrumpLengthWeight`, `BeloteWeight`, `SideAceWeight` | Components of the suit-contract score. |
| `MasterCardLeadBonus` | Preference for cashing a card that is the highest unseen of its suit. |

**Validation**: thresholds are finite and non-negative; `Standard` is the only shipped instance.

## HandEvaluation

Result of evaluating one hand during bidding.

| Field | Description |
|-------|-------------|
| `Scores` | Map `ContractKind -> double` for every kind in `RuleSet.Bidding.Ranking`. |
| `Best` | The highest-scoring kind (ties by contract rank, then canonical order). |

## CardMemory

Public knowledge of one hand in progress, rebuilt from the `SeatView` every decision.

| Field | Description |
|-------|-------------|
| `Played` | Set of cards seen in completed tricks and the current trick. |
| `Unseen` | The 32-card deck minus `OwnHand` minus `Played`. |
| `RemainingBySuit` | Count of `Unseen` per suit. |
| `TrumpsRemaining` | Unseen trump cards (empty for no-trump / all-trump handled per family). |
| `KnownVoids` | Per seat, the suits that seat has shown it cannot follow. |
| `IsMaster(card)` | True if no `Unseen` card beats `card` under `CardStrength.Of` for its suit. |

**Relationships**: `Unseen` and `Played` partition the deck together with `OwnHand`; the sum of
sizes is always 32 (an invariant checked by a unit test).

## TrickSituation

Snapshot of the trick being played, derived from `view.CurrentTrick` and the contract.

| Field | Description |
|-------|-------------|
| `LedSuit` | Suit of the first card, or none when the bot leads. |
| `CurrentWinner` | Seat currently winning (via `TrickResolver.Best`), or none. |
| `PartnerWinning` | Whether the current winner is the bot's partner. |
| `PositionFromLast` | Seats still to play after the bot (0 = last). |
| `PointsOnTable` | Card points already in the trick under `RuleSet.CardPoints`. |
| `PartnerSecure` | Partner's winning card is master, or the bot is last. |

## BenchmarkResult

Produced by `Benchmark.RunAsync`; serialisable to JSON for CI output.

| Field | Description |
|-------|-------------|
| `BaseSeed`, `GameCount` | Inputs; seeds are `Seeds.ForGame(BaseSeed, i)`. |
| `SmartWins`, `RandomWins` | Match wins per controller type, summed across both seat assignments. |
| `SmartWinRate` | `SmartWins / GameCount`. |
| `MeanScoreMargin` | Mean of (smart team final score - random team final score). |
| `Failed` | Count of games that did not finish correctly (must be 0). |
| `Games` | Per-game `{ index, seed, smartTeam, winner, northSouth, eastWest }`. |

**State transitions**: none. The bot is a pure function of the view; a benchmark is a batch of
independent games.
