# Quickstart: Live Table vs Bots

Validation guide for the headless live table. Protocol details are in
[contracts/hub-protocol.md](contracts/hub-protocol.md); entities in [data-model.md](data-model.md).

## Prerequisites

- .NET SDK 10 (`dotnet --version` reports 10.x).
- Repo root: `belot4e`.

## Build and run all tests

```bash
dotnet build Belote.sln
dotnet test Belote.sln
```

Expected: all existing 001 tests plus the new `Belote.Table.Tests` and `Belote.Server.Tests`
pass.

## Validation scenarios

### 1. One complete game end to end (User Story 1)

```bash
dotnet test tests/Belote.Server.Tests --filter "FullyQualifiedName~PlaysOneCompleteGame"
```

Expected: the test client connects, creates a table, takes South, plays every turn using the
server-reported `legalActions`, and receives a final update with `finished: true`, a winner,
and a score at or above the target. No intent is rejected.

### 2. 100 seeded games over one connection (SC-001, SC-002, SC-004, SC-006)

```bash
dotnet test tests/Belote.Server.Tests --filter "FullyQualifiedName~HundredSeededGames"
```

Expected, for every game:

- zero rejected intents and zero stuck games;
- the live event sequence equals the 001 all-bot simulation of the same seed (same events,
  same final score);
- no message contains a card outside the seat's own hand or already public;
- replaying the recorded log with `GameAuditor` reproduces the same final state.

### 3. Scripted rejections leave state identical (SC-003)

```bash
dotnet test tests/Belote.Server.Tests --filter "FullyQualifiedName~RejectionCatalogue"
```

Expected: each of `malformed`, `notSeated`, `gameNotInProgress`, `notOwner`, `outOfTurn`,
`illegal` and `tableExists` is produced at least once with a code and detail, and the table
snapshot (the same object: version, state, events) is unchanged afterwards.

### 4. Concurrency (SC-005)

```bash
dotnet test tests/Belote.Table.Tests --filter "FullyQualifiedName~BurstTests"
dotnet test tests/Belote.Server.Tests --filter "FullyQualifiedName~ConcurrencyDisconnectAndWire"
```

Expected: bursts of simultaneous intents, including identical card plays, each get one
definite outcome; exactly one duplicate is accepted; the table log replays to the same state.

### 5. Pacing (SC-007)

```bash
dotnet test tests/Belote.Table.Tests --filter "FullyQualifiedName~PacingAndPauseTests"
```

Expected: with a fake clock and a non-zero delay, consecutive bot moves are separated by at
least the delay; with zero delay no waiting occurs.

### 6. Pause on disconnect (FR-026)

```bash
dotnet test tests/Belote.Table.Tests --filter "FullyQualifiedName~PacingAndPauseTests"
```

Expected: after `Detach` no bot moves or version changes occur until `Attach`.

## Manual run

```bash
dotnet run --project src/Belote.Server -- --urls http://127.0.0.1:5080
```

Connect any SignalR client to `http://127.0.0.1:5080/table`, call `CreateTable` with
`{"seed":42,"botDelayMs":0}`, `TakeSeat` with `{"seat":"south"}`, then loop on `Update`
messages sending one of the entries in `view.legalActions` to `SubmitIntent`. Seed 42 with a
South that picks uniformly at random using seed-derived choices reproduces the 001 run for the
same seed.
