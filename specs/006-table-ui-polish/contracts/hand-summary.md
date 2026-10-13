# Contract: `lastHand` on the table view

Additive, nullable field on `SeatViewDto` (hub view/update payloads). Protocol version stays `1`:
clients that do not know the field ignore it; the web client treats a missing or null value as
"no finished hand to show".

## Shape (JSON, camelCase as the rest of the view)

```json
{
  "lastHand": {
    "dealNumber": 3,
    "contract": { "kind": "hearts", "declarer": "east", "multiplier": 1 },
    "declaringTeam": "eastWest",
    "outcome": "made",
    "teams": {
      "northSouth": {
        "wonCards": ["AS", "10S", "KD"],
        "cardPoints": 62, "lastTrickBonus": 0, "announcementPoints": 20,
        "belotePoints": 0, "allTricksBonus": 0, "rawTotal": 82, "gamePoints": 8
      },
      "eastWest": {
        "wonCards": ["JH", "9H"],
        "cardPoints": 100, "lastTrickBonus": 10, "announcementPoints": 0,
        "belotePoints": 20, "allTricksBonus": 0, "rawTotal": 130, "gamePoints": 13
      }
    },
    "matchScore": { "northSouth": 41, "eastWest": 57 },
    "heldOver": { "before": 0, "awarded": 0, "awardedTo": null, "newlyHeld": 0, "after": 0 },
    "winner": null
  }
}
```

(Card lists are abbreviated above.)

## Guarantees

- Present on every view after a hand has been scored, until the next hand is scored; `null`
  before that and in a brand-new game.
- A voided hand (all passed) does not change it.
- Contains only cards that were played face-up; never any card still in a hand or the deck.
- `wonCards` use the same card string format as the rest of the protocol (`"10H"`, `"AS"`).
- Sent to every seat identically (it is public information once the hand is over).

## Client use

The client compares `lastHand.dealNumber` with the previously presented view's value to detect a
newly finished hand; it never re-derives scoring from cards.
