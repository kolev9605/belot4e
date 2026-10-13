import type { IntentDto, TableView } from "../protocol/types";
import { announceActions, bidActions } from "../state/selectors";
import { useLanguage } from "../i18n/LanguageProvider";
import { actionLabel, historyLabel, publicAnnouncementLabel, announcementSpecLabel } from "./labels";

const buttonClass = "btn text-left";

interface PanelProps {
  game: TableView;
  enabled: boolean;
  onAct: (intent: IntentDto) => void;
  voided: boolean;
}

/** Bidding history plus the bids the server currently allows. */
export function BidPanel({ game, enabled, onAct, voided }: PanelProps) {
  const { language, t } = useLanguage();
  const actions = bidActions(game);
  return (
    <section aria-labelledby="bid-heading" className="panel mt-4 p-3">
      <h2 id="bid-heading" className="heading-label mb-2">
        {t("label.bidding")}
      </h2>
      {voided && (
        <p role="status" className="mb-2 font-semibold text-warn">
          {t("label.voided")}
        </p>
      )}
      {game.biddingHistory.length === 0 ? (
        <p>{t("label.noBids")}</p>
      ) : (
        <ul className="text-sm">
          {game.biddingHistory.map((entry, i) => (
            <li key={i}>{historyLabel(language, entry)}</li>
          ))}
        </ul>
      )}
      {actions.length > 0 && (
        <div className="mt-3">
          <p className="mb-1 font-semibold">{t("label.legalBids")}</p>
          <ul className="flex flex-wrap gap-2">
            {actions.map((action, i) => (
              <li key={i}>
                <button type="button" disabled={!enabled} className={buttonClass} onClick={() => onAct(action)}>
                  {actionLabel(language, action)}
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

/** Announcements already public, the player's own, and any the server allows declaring now. */
export function AnnouncePanel({ game, enabled, onAct }: Omit<PanelProps, "voided">) {
  const { language, t } = useLanguage();
  const actions = announceActions(game);
  if (actions.length === 0 && game.announcements.length === 0 && game.ownDeclared.length === 0) return null;
  return (
    <section aria-labelledby="announce-heading" className="panel mt-4 p-3">
      <h2 id="announce-heading" className="heading-label mb-2">
        {t("label.announcements")}
      </h2>
      {game.announcements.length > 0 && (
        <ul className="text-sm">
          {game.announcements.map((a, i) => (
            <li key={i}>{publicAnnouncementLabel(language, a)}</li>
          ))}
        </ul>
      )}
      {game.ownDeclared.length > 0 && (
        <div className="mt-2 text-sm">
          <p className="font-semibold">{t("label.yourAnnouncements")}</p>
          <ul>
            {game.ownDeclared.map((spec, i) => (
              <li key={i}>{announcementSpecLabel(language, spec)}</li>
            ))}
          </ul>
        </div>
      )}
      {actions.length > 0 && (
        <div className="mt-3">
          <p className="mb-1 font-semibold">{t("label.declare")}</p>
          <ul className="flex flex-wrap gap-2">
            {actions.map((action, i) => (
              <li key={i}>
                <button type="button" disabled={!enabled} className={buttonClass} onClick={() => onAct(action)}>
                  {actionLabel(language, action)}
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
