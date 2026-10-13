import { cardName, parseCard } from "../i18n";
import { useLanguage } from "../i18n/LanguageProvider";

export const SUIT_GLYPHS: Record<string, string> = { S: "♠", H: "♥", D: "♦", C: "♣" };

/** Multiples of the hand card size (see --card-h / --card-w in theme/tokens.css). */
export type CardSize = "hand" | "table" | "small";
const SIZE_FACTOR: Record<CardSize, number> = { hand: 1, table: 0.85, small: 0.6 };

/** The printed face of a card: rank and suit shape, never colour alone. Purely visual. */
export function CardFace({ card, size = "hand" }: { card: string; size?: CardSize }) {
  const { rank, suit } = parseCard(card);
  const red = suit === "H" || suit === "D";
  const glyph = SUIT_GLYPHS[suit] ?? "";
  const k = SIZE_FACTOR[size];
  return (
    <span
      aria-hidden="true"
      data-testid="card-face"
      data-suit={suit}
      style={{
        height: `calc(var(--card-h) * ${k})`,
        width: `calc(var(--card-w) * ${k})`,
        fontSize: `calc(var(--card-h) * ${0.2 * k})`,
      }}
      className={`relative flex select-none flex-col items-start justify-between rounded-card border border-black/20 bg-face p-[0.2em] shadow-md shadow-black/40 ${
        red ? "text-face-red" : "text-face-ink"
      }`}
    >
      <span className="flex flex-col items-center leading-none">
        <span className="font-bold">{rank}</span>
        <span>{glyph}</span>
      </span>
      <span className="absolute inset-0 flex items-center justify-center text-[1.9em] opacity-90">{glyph}</span>
    </span>
  );
}

/** The back of a card, used for opponents' hands and the dealer's stack. Purely visual. */
export function CardBack({ className = "" }: { className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={`block rounded-[0.3rem] border border-back-line bg-back shadow shadow-black/40 ${className}`}
      style={{
        backgroundImage:
          "repeating-linear-gradient(45deg, rgb(255 255 255 / 0.07) 0 3px, transparent 3px 7px)",
      }}
    />
  );
}

/** A card lying on the table: not interactive. */
export function TableCard({ card, size = "table" }: { card: string; size?: CardSize }) {
  const { language } = useLanguage();
  return (
    <span role="img" aria-label={cardName(language, card)} className="inline-flex">
      <CardFace card={card} size={size} />
    </span>
  );
}

/**
 * How a hand card presents itself:
 * - inert: the person cannot play right now (bidding, or another seat's turn); shown normally.
 * - playable / unplayable: only while it is the person's turn to play.
 */
export type HandCardMode = "inert" | "playable" | "unplayable";

interface HandCardProps {
  card: string;
  mode: HandCardMode;
  enabled: boolean;
  onPlay: () => void;
}

/** A card in the player's hand: a button whose name carries rank, suit and, when relevant, playability. */
export function HandCard({ card, mode, enabled, onPlay }: HandCardProps) {
  const { language, t } = useLanguage();
  const name = cardName(language, card);
  const playable = mode === "playable";
  const usable = playable && enabled;
  const label = mode === "inert" ? name : playable ? t("card.playable", { name }) : t("card.notPlayable", { name });
  // Highlighting never moves a card: only ring and opacity change. Only the pointed-at card lifts.
  const style =
    mode === "inert"
      ? "ring-1 ring-black/10"
      : playable
        ? "ring-2 ring-accent " + (enabled ? "cursor-pointer hover:-translate-y-1 focus-visible:-translate-y-1" : "opacity-70")
        : "opacity-45 saturate-50 ring-1 ring-line";
  return (
    <button
      type="button"
      data-card={card}
      data-mode={mode}
      data-playable={playable ? "true" : "false"}
      disabled={!usable}
      aria-label={label}
      onClick={onPlay}
      className={`relative inline-flex rounded-card transition-transform duration-150 disabled:cursor-default ${style}`}
    >
      <CardFace card={card} />
      {mode === "unplayable" && (
        <span aria-hidden="true" className="absolute inset-x-1 bottom-1 h-0.5 rounded bg-face-ink/70" />
      )}
      {mode === "unplayable" && <span className="sr-only">{t("label.notPlayable")}</span>}
    </button>
  );
}
