# Contract: `Belote.Table` in-process API

The surface shared by the SignalR hub, the bot driver, and in-process tests. Signatures show
shape and semantics; exact C# syntax is settled during implementation. No ASP.NET types appear
here.

```text
LiveTable.Create(TableOptions options, Principal creator,
                 Func<Seat, IPlayer>? botFactory = null,
                 Action<FinishedGame>? onFinished = null) -> LiveTable   // lifecycle AwaitingSeat

LiveTable.TakeSeatAsync(Principal client) -> TakeSeatOutcome   // creator only; seats South, starts game, version 0
LiveTable.SubmitAsync(Intent intent)       -> ValueTask<IntentOutcome>
LiveTable.SubmitAsync(Principal sender, IntentDto dto) -> ValueTask<IntentOutcome>   // parses; malformed never queues
LiveTable.GetView(Principal client)        -> ViewOutcome               // lock-free read
LiveTable.AttachAsync / DetachAsync(Principal client)                  // resume / pause
LiveTable.Subscribe(ITableObserver observer) -> IDisposable
LiveTable.Snapshot                          -> TableSnapshot            // for assertions
LiveTable.Completion                        -> Task<FinishedGame?>      // completes on finish
```

```text
interface ITableObserver { ValueTask OnUpdateAsync(TableUpdate update) }
```

## Guarantees

- **Serialised**: `SubmitAsync`, `TakeSeatAsync`, `Attach`, `Detach` are enqueued and handled
  one at a time in arrival order; each caller gets exactly one definite outcome.
- **Atomic**: `Rejected` leaves `Snapshot` (version, state reference, last trick) and the
  event list unchanged and notifies no observer.
- **Ordered, gapless updates**: observers see versions `0, 1, 2, ...` with no gaps or
  duplicates, in the order state changed.
- **Seat-blind**: ownership is a `Seat -> Principal` equality check; bots and humans differ
  only in which principal they hold.
- **Paused**: while detached, every intent is refused (`paused`) and the bot driver blocks;
  `Snapshot.Version` and `State` are stable. A decision a bot already made is kept and
  resubmitted after `Attach`, so a pause never consumes the bot random draw.
- **Record before notification**: `onFinished` runs inside the actor before the final update is
  pushed, so a client that has seen `finished: true` can rely on the log being stored.
- **Deterministic**: for fixed `(RuleSet, Seed)` and fixed per-seat choice policies the event
  sequence equals the 001 simulation of that seed.

## Bot driver

```text
BotDriver.Run(LiveTable table, IReadOnlyDictionary<Seat, IPlayer> bots, TimeProvider time, TimeSpan delay)
```

Per iteration: wait until `ToAct` is a bot seat and the table is attached; `await
time.Delay(delay)`; re-check attached; `view = Projection.For(state, seat)`; `action = await
bot.DecideAsync(view)`; `table.SubmitAsync(new Intent(seat, action, BotPrincipal(seat)))`.
A rejection of a bot's own choice moves the table to `Faulted` and is logged.

## Finished-game store

```text
FinishedGames(string? directory = null)
FinishedGames.Add(FinishedGame game)                  // also writes <seed>-<tableId>.jsonl when a directory is set
FinishedGames.All -> IReadOnlyList<FinishedGame>     // server-side only; never exposed on the hub
```
