import { describe, expect, it } from "vitest";
import { bg } from "../src/i18n/bg";
import { en } from "../src/i18n/en";
import { cardName, rejectionMessage, translate } from "../src/i18n";

describe("i18n", () => {
  it("defines identical keys in English and Bulgarian", () => {
    expect(Object.keys(bg).sort()).toEqual(Object.keys(en).sort());
  });

  it("has no empty messages", () => {
    for (const [key, value] of [...Object.entries(en), ...Object.entries(bg)]) {
      expect(value.trim(), key).not.toBe("");
    }
  });

  it("translates every rejection code in both languages", () => {
    const codes = [
      "NotYourTurn",
      "WrongPhase",
      "BidNotHigher",
      "BidNotAllowed",
      "ContraNotAllowed",
      "RecontraNotAllowed",
      "CardNotInHand",
      "MustFollowSuit",
      "MustTrump",
      "MustOvertrump",
      "AnnouncementNotInHand",
      "AnnouncementNotAllowed",
      "AnnouncementWindowClosed",
      "GameFinished",
      "GameNotStarted",
      "TableFaulted",
      "NotSeated",
      "NotOwner",
      "TableExists",
      "SeatTaken",
      "TablePaused",
      "Malformed",
    ];
    for (const code of codes) {
      expect(`reject.${code}` in en, code).toBe(true);
      expect(rejectionMessage("bg", code)).not.toBe(rejectionMessage("bg", "NoSuchCode"));
    }
    expect(rejectionMessage("en", "NoSuchCode")).toBe(en["reject.unknown"]);
  });

  it("names cards by rank and suit", () => {
    expect(cardName("en", "10H")).toBe("Ten of Hearts");
    expect(cardName("bg", "AS")).toBe("Асо Пика");
  });

  it("fills placeholders", () => {
    expect(translate("en", "label.cards", { count: 5 })).toBe("5 cards");
  });
});
