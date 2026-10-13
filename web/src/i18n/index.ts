import { bg } from "./bg";
import { en, type MessageKey } from "./en";

export type Language = "en" | "bg";
export type { MessageKey };

const dictionaries: Record<Language, Record<MessageKey, string>> = { en, bg };
const STORAGE_KEY = "belote.language";

export type Vars = Record<string, string | number>;

export function format(template: string, vars?: Vars): string {
  if (!vars) return template;
  return template.replace(/\{(\w+)\}/g, (_, name: string) => String(vars[name] ?? `{${name}}`));
}

export function translate(language: Language, key: MessageKey, vars?: Vars): string {
  return format(dictionaries[language][key], vars);
}

export function hasKey(key: string): key is MessageKey {
  return key in en;
}

export function detectLanguage(): Language {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    if (stored === "en" || stored === "bg") return stored;
  } catch {
    // storage unavailable; fall through to the browser language
  }
  const browser = typeof navigator !== "undefined" ? navigator.language : "en";
  return browser.toLowerCase().startsWith("bg") ? "bg" : "en";
}

export function storeLanguage(language: Language): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, language);
  } catch {
    // ignore: the preference is a convenience only
  }
}

/** Message for a server rejection `code`, falling back to a generic refusal. */
export function rejectionMessage(language: Language, code: string): string {
  const key = `reject.${code}`;
  return translate(language, hasKey(key) ? key : "reject.unknown");
}

const RANKS = ["7", "8", "9", "10", "J", "Q", "K", "A"];

/** Splits a wire card ("10H") into rank text and suit letter. */
export function parseCard(card: string): { rank: string; suit: string } {
  return { rank: card.slice(0, -1), suit: card.slice(-1) };
}

export function cardName(language: Language, card: string): string {
  const { rank, suit } = parseCard(card);
  const rankKey = `rank.${rank}`;
  const suitKey = `suit.${suit}`;
  return translate(language, "card.name", {
    rank: RANKS.includes(rank) && hasKey(rankKey) ? translate(language, rankKey) : rank,
    suit: hasKey(suitKey) ? translate(language, suitKey) : suit,
  });
}

export function rankName(language: Language, rank: string): string {
  const key = `rank.${rank}`;
  return hasKey(key) ? translate(language, key) : rank;
}
