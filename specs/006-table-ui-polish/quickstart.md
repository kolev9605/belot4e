# Quickstart: validating Table UI Polish

Prerequisites: .NET 10 SDK and Node as used by the repo; commands run from the repo root unless noted.

## 1. Automated checks

```text
dotnet test                        # includes the new hand-summary tests in Belote.Table.Tests
cd web
npm test                           # Vitest: presenter steps (bid, result), hand layout, seats, dialog
npx playwright test                # e2e at desktop, laptop (1366x768) and phone viewports
```

Expected: all green; the new e2e checks are listed in step 3.

## 2. Run the app

```text
dotnet run --project src/Belote.Server     # start the server (see .vscode tasks)
cd web && npm run dev                      # start the client, open the printed address
```

## 3. Manual / e2e scenarios (map to spec stories)

| # | Do | Expect |
|---|----|--------|
| 1 | Open at 1366x768 (devtools device toolbar or a 1366x768 window), start a game | No vertical scrollbar; whole hand, all seats and the bidding strip visible during bidding and during play (US1, SC-001) |
| 2 | Resize between ~600 and ~1100 px height | Hand stays fully visible; nothing overlaps (US1.3) |
| 3 | During bidding, inspect the hand | Every card looks normal; none dimmed, struck or labelled "not playable" (US3, SC-003) |
| 4 | Look at the hand with all four suits and a trump suit | All cards on one baseline, equal gaps; no card higher than its neighbours (US2, SC-002) |
| 5 | In the playing phase on your turn | Legal cards are highlighted, illegal ones are visibly unavailable; no card moves when highlighting changes (US3.2) |
| 6 | Watch a bidding round with bots | About a second between consecutive calls; each seat box lists that seat's calls in order, latest emphasised; person's own buttons enable only after the previous call is shown (US4, SC-004/005) |
| 7 | Play a hand through | After the last trick is shown a pop-up opens: two labelled team rows of won cards (32 total), per-team score breakdown, hand score and match total; your team is marked (US5, SC-006/007) |
| 8 | Dismiss with the mouse, then repeat with Escape and keyboard | Table continues to the next deal; no bids/deal happened behind the pop-up |
| 9 | Finish a whole game | Pop-up names the winner and offers "Play again" (US5.4) |
| 10 | Everyone passes (voided hand) | No pop-up with empty rows; the existing "redealt" notice shows (edge case) |
| 11 | Switch to Bulgarian during a game | All new labels (call chips, pop-up) are localized; layout still fits |
| 12 | Phone viewport (360x640) | Hand, seats and pop-up usable; no horizontal scroll |
| 13 | Tab through the page with the keyboard only | Focus trapped in the pop-up while open, restored on close; full game playable |

## 4. Contract spot-check

Open the browser network/WebSocket frames or the server log and confirm `lastHand` is `null`
before the first hand ends and non-null afterwards, containing only played cards
(see [contracts/hand-summary.md](contracts/hand-summary.md)).
