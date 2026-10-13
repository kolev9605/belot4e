# Contract: Smart bot library surface

Public surface added by this feature. There is **no hub, wire or web-client change**: bot moves
travel through the existing intent path and the existing views.

## `Belote.Bots.SmartBot`

```csharp
public sealed class SmartBot : IPlayer
{
    public SmartBot(SmartBotOptions? options = null);
    public ValueTask<PlayerAction> DecideAsync(SeatView view, CancellationToken cancellationToken);
}
```

Behavioural guarantees (each is a test):

| # | Guarantee | Spec |
|---|-----------|------|
| 1 | Result is always an element of `LegalActions.For(view)`. | FR-002, SC-002 |
| 2 | Result depends only on `view` (and options): equal views give equal actions, across calls and across instances. | FR-003, FR-010, SC-003 |
| 3 | No mutable state survives between calls. | FR-010 |
| 4 | Throws `InvalidOperationException` when no legal action exists (not the bot's turn). | parity with `RandomLegalBot` |
| 5 | Completes synchronously (returned `ValueTask` is already completed). | FR-015 |
| 6 | The `Belote.Bots` assembly references only `Belote.Core`. | Principle I, III |

## `Belote.Bots.SmartBotOptions`

```csharp
public sealed record SmartBotOptions
{
    public static SmartBotOptions Standard { get; }
    // tunable weights and thresholds; see data-model.md
}
```

## Seat presets (`Belote.Simulation.SeatPresets`)

Existing presets (`all-bot`, `mixed-1..4`, `scripted-illegal`) are unchanged and keep using
`RandomLegalBot`. Added:

| Preset | North | East | South | West | Use |
|--------|-------|------|-------|------|-----|
| `all-smart` | Smart | Smart | Smart | Smart | Legality/determinism of the smart bot |
| `smart-ns` | Smart | Random | Smart | Random | Benchmark, smart team = North/South |
| `smart-ew` | Random | Smart | Random | Smart | Benchmark, seats swapped |
| `smart-table` | Smart | Smart | Random | Smart | Equivalence with a live table whose client plays South with the baseline policy |

Controller labels in logs: `SmartBot` and `RandomLegalBot` (type names). Each seat's
`RandomLegalBot` still takes `Seeds.ForSeat(gameSeed, seat)`.

## Live table (`Belote.Table.LiveTable`)

`LiveTable.DefaultBot(seed)` returns a `SmartBot` for each of North, East and West. A caller may
still pass its own `botFactory`. The finished-game log labels bot seats `SmartBot`.

## Compatibility

- `RandomLegalBot`: unchanged behaviour and seeds; existing golden logs remain valid.
- Hub protocol version, `TableUpdateDto`, `SeatViewMapper`: unchanged.
- Event-log format: unchanged (only the controller label string for bot seats of live tables).
