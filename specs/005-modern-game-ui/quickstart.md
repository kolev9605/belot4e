# Quickstart: validating the Modern Game UI

## Prerequisites

- .NET 10 SDK and Node 22+
- `cd web && npm install` (adds `motion`)

## Run

```bash
dotnet run --project src/Belote.Server
```

```bash
cd web && npm run dev
```

Open the printed dev URL, start a game.

## Automated checks

```bash
dotnet test
```

```bash
cd web && npm run typecheck && npm test
```

```bash
cd web && npm run e2e
```

## Manual validation (maps to spec)

1. **Hand order (US1, SC-001)** – Over five deals, confirm cards are grouped ♠ ♥ ♣ ♦ and sorted
   strongest to weakest; play several cards and confirm no reshuffle; reach a trump contract and
   confirm the trump group is ordered J, 9, A, 10, K, Q, 8, 7.
2. **Pacing (US2, SC-002/004)** – Watch a deal (≈ 2 s, from the dealer's seat) and a bot-only
   stretch: each card visibly travels, the finished trick stays ≥ 1 s and then sweeps to the
   winner; on your turn, controls work immediately after things settle.
3. **Table (US3, SC-003)** – Without hints, point out dealer, whose turn, your team, each
   opponent's card count; finish a hand and confirm the dealer marker moves one seat.
4. **Theme (US4, SC-005/006)** – Walk through start, bidding, announcing, playing, game over,
   disconnected (stop the server) and notices: all dark, one accent, readable; at 360 px width no
   horizontal scroll; switch to Bulgarian and check labels fit.
5. **Reduced motion (SC-007)** – Enable "reduce motion" in the OS, play a hand: no travel, same
   pauses, nothing lost.
6. **Keyboard** – Tab through bid buttons and hand cards; Enter/Space plays; focus ring visible.
