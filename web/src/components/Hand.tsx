import type { IntentDto, TableView } from "../protocol/types";
import { useLanguage } from "../i18n/LanguageProvider";
import { HandCard } from "./Card";

interface HandProps {
  game: TableView;
  enabled: boolean;
  onAct: (intent: IntentDto) => void;
}

export function Hand({ game, enabled, onAct }: HandProps) {
  const { t } = useLanguage();
  // Legality comes only from the server's legal actions; the card itself is echoed back unchanged.
  const playAction = new Map<string, IntentDto>();
  for (const action of game.legalActions) {
    if (action.kind === "play" && action.card) playAction.set(action.card, action);
  }

  return (
    <section aria-labelledby="hand-heading" className="mt-4">
      <h2 id="hand-heading" className="mb-2 text-sm font-semibold uppercase tracking-wide text-slate-700">
        {t("label.hand")}
      </h2>
      {game.ownHand.length === 0 ? (
        <p>{t("label.emptyHand")}</p>
      ) : (
        <ul className="flex flex-wrap gap-2">
          {game.ownHand.map((card) => {
            const action = playAction.get(card);
            return (
              <li key={card}>
                <HandCard
                  card={card}
                  playable={action !== undefined}
                  enabled={enabled}
                  onPlay={() => action && onAct(action)}
                />
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
