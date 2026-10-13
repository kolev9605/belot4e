import { cardName, parseCard } from "../i18n";
import { useLanguage } from "../i18n/LanguageProvider";

const GLYPHS: Record<string, string> = { S: "♠", H: "♥", D: "♦", C: "♣" };

function CardText({ card }: { card: string }) {
  const { language, t } = useLanguage();
  const { rank, suit } = parseCard(card);
  const red = suit === "H" || suit === "D";
  void language;
  return (
    <span className="flex flex-col items-center leading-tight">
      <span className={`text-lg font-bold ${red ? "text-red-800" : "text-slate-900"}`} aria-hidden="true">
        {rank}
        {GLYPHS[suit] ?? ""}
      </span>
      <span className="text-xs" aria-hidden="true">
        {t(`suit.${suit}` as never)}
      </span>
    </span>
  );
}

/** A card lying on the table: not interactive. */
export function TableCard({ card }: { card: string }) {
  const { language } = useLanguage();
  return (
    <span
      role="img"
      aria-label={cardName(language, card)}
      className="inline-flex min-h-14 min-w-14 items-center justify-center rounded border-2 border-slate-700 bg-white px-2 py-1"
    >
      <CardText card={card} />
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
      disabled={!usable}
      aria-label={playable ? t("card.playable", { name }) : t("card.notPlayable", { name })}
      onClick={onPlay}
      className={
        "inline-flex min-h-16 min-w-14 items-center justify-center rounded border-2 px-2 py-1 " +
        "focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-blue-700 " +
        (playable
          ? "border-slate-900 bg-white hover:bg-yellow-100 " + (enabled ? "" : "opacity-60")
          : "border-dashed border-slate-400 bg-slate-100 text-slate-600 line-through")
      }
    >
      <CardText card={card} />
      {!playable && <span className="sr-only">{t("label.notPlayable")}</span>}
    </button>
  );
}
