import type { ContractKind } from "../protocol/types";
import { parseCard } from "../i18n";

/** Presentation-only suit group order: alternating black/red so neighbouring groups read apart. */
export const SUIT_ORDER = ["S", "H", "C", "D"] as const;

const TRUMP_SUIT: Partial<Record<ContractKind, string>> = {
  spades: "S",
  hearts: "H",
  clubs: "C",
  diamonds: "D",
};

export interface SuitGroup {
  suit: string;
  cards: string[];
  trump: boolean;
}

/**
 * Groups the hand by suit in the fixed suit order. The order inside a group is exactly the order the
 * server sent (strongest first for the contract in force); card power is never computed here.
 */
export function groupBySuit(cards: readonly string[], contract: ContractKind | null): SuitGroup[] {
  const bySuit = new Map<string, string[]>();
  for (const card of cards) {
    const { suit } = parseCard(card);
    const list = bySuit.get(suit);
    if (list) list.push(card);
    else bySuit.set(suit, [card]);
  }
  const trumpSuit = contract ? TRUMP_SUIT[contract] : undefined;
  const known = SUIT_ORDER.filter((s) => bySuit.has(s));
  const unknown = [...bySuit.keys()].filter((s) => !(SUIT_ORDER as readonly string[]).includes(s));
  return [...known, ...unknown].map((suit) => ({
    suit,
    cards: bySuit.get(suit) ?? [],
    trump: contract === "allTrump" || suit === trumpSuit,
  }));
}
