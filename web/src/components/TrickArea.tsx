import type { TableView } from "../protocol/types";
import { trickToShow } from "../state/selectors";
import { useLanguage } from "../i18n/LanguageProvider";
import { TableCard } from "./Card";
import { seatName } from "./labels";

export function TrickArea({ game }: { game: TableView }) {
  const { language, t } = useLanguage();
  const trick = trickToShow(game);
  return (
    <section aria-labelledby="trick-heading" className="mt-4 rounded border-2 border-slate-400 bg-white p-2">
      <h2 id="trick-heading" className="mb-2 text-sm font-semibold uppercase tracking-wide text-slate-700">
        {trick.completed ? t("label.lastTrick") : t("label.table")}
      </h2>
      {trick.plays.length === 0 ? (
        <p>{t("label.noCards")}</p>
      ) : (
        <ul className="flex flex-wrap gap-3">
          {trick.plays.map((play) => (
            <li key={`${play.seat}-${play.card}`} className="flex flex-col items-center gap-1 text-sm">
              <span>{seatName(language, play.seat)}</span>
              <TableCard card={play.card} />
            </li>
          ))}
        </ul>
      )}
      {trick.completed && trick.winner && (
        <p className="mt-2 font-semibold">
          {t("label.trickWon", { seat: seatName(language, trick.winner as never) })}
        </p>
      )}
    </section>
  );
}
