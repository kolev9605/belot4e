# Contract: Client connection and game state machine

## Connection

```text
connecting --ok--> connected --drop--> reconnecting --ok--> connected
    |                                      |
    +--fail/backoff--> unavailable <-------+   (retries continue indefinitely)
```

Backoff schedule: 0 s, 1 s, 2 s, 5 s, then every 10 s. Input is enabled only in `connected`.
Entering `connected` always triggers `GetView` before any input is allowed.

## Session

```text
idle --startGame--> starting --first update--> playing --finished:true--> finished
 ^                                               |                           |
 |                         connection lost / TableClosed / notSeated         |
 +------------------------ lost <-------------------+                        |
 +--------------------------- startGame (play again) <-----------------------+
```

| State | Allowed input |
|-------|---------------|
| `idle` | Start game (when connected) |
| `starting` | none |
| `playing` | legal actions only, when connected, `toAct == south`, no `pending` |
| `finished` | Play again |
| `lost` | Start game (when connected) |

## Update handling

| Incoming | Condition | Effect |
|----------|-----------|--------|
| `Update` | `protocolVersion != 1` | `notice: versionMismatch`, input disabled |
| `Update` | `version == last + 1` (or first after sync) | apply |
| `Update` | `version <= last` | drop |
| `Update` | `version > last + 1` | `GetView`, apply if newer |
| `Update` | `finished: true` | apply, `session = finished` |
| `TableClosed` | any | `session = lost` |
| `IntentResult` rejected | any | clear `pending`, `notice: rejection(code)`, view unchanged |
| `IntentResult` rejected | `notSeated` | `session = lost` |
| transport error on call | any | clear `pending`, `notice: error`, resync on reconnect |

## Guarantees

1. The displayed view is always a view the server sent, unmodified.
2. The displayed version never decreases.
3. From any state, once `connected`, a path to `startGame` exists (FR-016).
4. At most one intent is in flight (FR-010).
