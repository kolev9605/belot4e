import type { IntentDto, TableView } from "../protocol/types";
import { announceActions, bidActions } from "../state/selectors";
import { useLanguage } from "../i18n/LanguageProvider";
import { actionLabel, publicAnnouncementLabel, announcementSpecLabel } from "./labels";

const buttonClass = "btn text-left";

interface PanelProps {
  game: TableView;
  enabled: boolean;
  onAct: (intent: IntentDto) => void;
  voided: boolean;
}

/**
 * The bids the server currently allows, shown in the middle of the table while bidding. The calls
 * already made appear in each seat's box, so this block stays small.
 */
export function BidPanel({ game, enabled, onAct, voided }: PanelProps) {
  const { language, t } = useLanguage();
  const actions = bidActions(game);
  return (
    <section aria-labelledby="bid-heading" className="flex flex-col items-center gap-2 text-center">
      <h2 id="bid-heading" className="heading-label !text-white/70">
        {t("label.bidding")}
      </h2>
      {voided && (
        <p role="status" className="font-semibold text-warn">
          {t("label.voided")}
        </p>
      )}
      {game.biddingHistory.length === 0 && !voided && <p className="text-sm text-white/80">{t("label.noBids")}</p>}
      {actions.length > 0 && (
        <div>
          <p className="mb-1 text-sm font-semibold">{t("label.legalBids")}</p>
          <ul className="flex flex-wrap justify-center gap-2">
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

/** Announcements already public, the person's own, and any the server allows declaring now: one slim strip. */
export function AnnouncePanel({ game, enabled, onAct }: Omit<PanelProps, "voided">) {
  const { language, t } = useLanguage();
  const actions = announceActions(game);
  if (actions.length === 0 && game.announcements.length === 0 && game.ownDeclared.length === 0) return null;
  return (
    <section
      aria-labelledby="announce-heading"
      className="panel flex flex-wrap items-center justify-center gap-x-3 gap-y-1 px-3 py-1 text-sm"
    >
      <h2 id="announce-heading" className="heading-label">
        {t("label.announcements")}
      </h2>
      {game.announcements.length > 0 && (
        <ul className="flex flex-wrap gap-x-3">
          {game.announcements.map((a, i) => (
            <li key={i}>{publicAnnouncementLabel(language, a)}</li>
          ))}
        </ul>
      )}
      {game.ownDeclared.length > 0 && (
        <div className="flex flex-wrap items-center gap-x-2">
          <span className="font-semibold">{t("label.yourAnnouncements")}:</span>
          <ul className="flex flex-wrap gap-x-3">
            {game.ownDeclared.map((spec, i) => (
              <li key={i}>{announcementSpecLabel(language, spec)}</li>
            ))}
          </ul>
        </div>
      )}
      {actions.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-semibold">{t("label.declare")}</span>
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
