import { cardName, parseCard } from "../i18n";
import { useLanguage } from "../i18n/LanguageProvider";

export const SUIT_GLYPHS: Record<string, string> = { S: "♠", H: "♥", D: "♦", C: "♣" };

const FACE_SIZE = "h-[4.25rem] w-12 sm:h-[5.25rem] sm:w-[3.75rem]";

/** The printed face of a card: rank and suit shape, never colour alone. Purely visual. */
export function CardFace({ card }: { card: string }) {
  const { rank, suit } = parseCard(card);
  const red = suit === "H" || suit === "D";
  const glyph = SUIT_GLYPHS[suit] ?? "";
  return (
    <span
      aria-hidden="true"
      data-testid="card-face"
      data-suit={suit}
      className={`relative flex ${FACE_SIZE} select-none flex-col items-start justify-between rounded-card border border-black/20 bg-face p-1 shadow-md shadow-black/40 ${
        red ? "text-face-red" : "text-face-ink"
      }`}
    >
      <span className="flex flex-col items-center leading-none">
        <span className="text-sm font-bold sm:text-base">{rank}</span>
        <span className="text-sm sm:text-base">{glyph}</span>
      </span>
      <span className="absolute inset-0 flex items-center justify-center text-2xl opacity-90 sm:text-3xl">{glyph}</span>
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
export function TableCard({ card }: { card: string }) {
  const { language } = useLanguage();
  return (
    <span role="img" aria-label={cardName(language, card)} className="inline-flex">
      <CardFace card={card} />
    </span>
  );
}

interface HandCardProps {
  card: string;
  playable: boolean;
  enabled: boolean;
  onPlay: () => void;
}

/** A card in the player's hand: a button whose name carries rank, suit and playability. */
export function HandCard({ card, playable, enabled, onPlay }: HandCardProps) {
  const { language, t } = useLanguage();
  const name = cardName(language, card);
  const usable = playable && enabled;
  return (
    <button
      type="button"
      data-card={card}
      data-playable={playable ? "true" : "false"}
      disabled={!usable}
      aria-label={playable ? t("card.playable", { name }) : t("card.notPlayable", { name })}
      onClick={onPlay}
      className={
        "relative inline-flex rounded-card transition-transform duration-150 " +
        (playable
          ? "cursor-pointer ring-2 ring-accent/80 hover:-translate-y-1 disabled:cursor-default disabled:hover:translate-y-0 " +
            (enabled ? "-translate-y-0.5" : "opacity-70")
          : "opacity-45 saturate-50 ring-1 ring-line")
      }
    >
      <CardFace card={card} />
      {!playable && (
        <span aria-hidden="true" className="absolute inset-x-1 bottom-1 h-0.5 rounded bg-face-ink/70" />
      )}
      {!playable && <span className="sr-only">{t("label.notPlayable")}</span>}
    </button>
  );
}
