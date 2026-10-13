import type { ConnectionStatus, TableView } from "../protocol/types";
import type { LostReason, Notice as NoticeState } from "../state/reducer";
import { rejectionMessage, type MessageKey } from "../i18n";
import { useLanguage } from "../i18n/LanguageProvider";
import { contractName, seatName, teamName } from "./labels";

const buttonClass = "btn";
const primaryButtonClass = "btn btn-primary";

export function ConnectionBanner({ status, showStale }: { status: ConnectionStatus; showStale: boolean }) {
  const { t } = useLanguage();
  const ok = status === "connected";
  return (
    <div
      role="status"
      aria-live="polite"
      data-testid="connection-banner"
      data-status={status}
      className={`rounded-lg border px-2 py-0.5 text-xs ${
        ok ? "border-line bg-surface text-muted" : "border-warn bg-surface font-semibold text-warn"
      }`}
    >
      {t(`conn.${status}` as MessageKey)}
      {!ok && showStale && <span className="ml-1">{t("conn.stale")}</span>}
    </div>
  );
}

export function ScoreBoard({ game }: { game: TableView }) {
  const { language, t } = useLanguage();
  const contract = game.contract;
  return (
    <section aria-label={t("label.score")} className="panel flex flex-wrap items-center justify-between gap-x-4 gap-y-0 px-3 py-1 text-sm">
      <div className="flex flex-wrap gap-x-4">
        <span data-testid="score-ns">
          {teamName(language, "northSouth")}: <strong>{game.matchScore.northSouth}</strong>
        </span>
        <span data-testid="score-ew">
          {teamName(language, "eastWest")}: <strong>{game.matchScore.eastWest}</strong>
        </span>
      </div>
      <div className="flex flex-wrap gap-x-4 text-muted">
        <span>{t("label.deal", { number: game.dealNumber })}</span>
        {game.heldOverPoints > 0 && <span>{t("label.heldOver", { points: game.heldOverPoints })}</span>}
        <span>
          {t("label.contract")}:{" "}
          {contract
            ? `${contractName(language, contract.kind)} ${t("label.declarer", { seat: seatName(language, contract.declarer) })}` +
              (game.recontra ? ` (${t("label.redoubled")})` : game.contra ? ` (${t("label.doubled")})` : "")
            : game.standingBid && game.standingBidder
              ? `${contractName(language, game.standingBid)} ${t("label.declarer", { seat: seatName(language, game.standingBidder) })}`
              : t("label.noContract")}
        </span>
      </div>
    </section>
  );
}

export function NoticeBar({ notice, onDismiss }: { notice: NoticeState | null; onDismiss: () => void }) {
  const { language, t } = useLanguage();
  if (!notice) return null;
  let message: string;
  switch (notice.kind) {
    case "rejection":
    case "roomRejection":
      message = rejectionMessage(language, notice.code);
      break;
    case "versionMismatch":
      message = t("notice.version");
      break;
    case "startFailed":
      message = notice.code ? `${t("notice.startFailed")} ${rejectionMessage(language, notice.code)}` : t("notice.startFailed");
      break;
    default:
      message = t("notice.error");
  }
  return (
    <div
      role="alert"
      data-testid="notice"
      className="mt-3 flex items-start justify-between gap-2 rounded-lg border border-danger bg-surface p-3 text-danger"
    >
      <span>{message}</span>
      <button type="button" className={buttonClass} onClick={onDismiss}>
        {t("notice.dismiss")}
      </button>
    </div>
  );
}

interface StartPanelProps {
  starting: boolean;
  canStart: boolean;
  lost: LostReason | null;
  onStart: () => void;
}

export function StartPanel({ starting, canStart, lost, onStart }: StartPanelProps) {
  const { t } = useLanguage();
  return (
    <section className="panel mt-4 p-4">
      {lost && (
        <p role="status" data-testid="game-lost" className="mb-3 font-semibold">
          {lost === "replaced" ? t("game.lostReplaced") : t("game.lostConnection")} {t("game.lost")}
        </p>
      )}
      {!lost && <p className="mb-3">{starting ? t("game.starting") : t("game.idle")}</p>}
      <button type="button" className={primaryButtonClass} disabled={!canStart || starting} onClick={onStart}>
        {t("game.start")}
      </button>
    </section>
  );
}

export function ResultPanel({
  game,
  onPlayAgain,
  canStart,
  hostOnly = false,
}: {
  game: TableView;
  onPlayAgain: () => void;
  canStart: boolean;
  /** In a room only the host can start the next game; everyone else just waits. */
  hostOnly?: boolean;
}) {
  const { language, t } = useLanguage();
  const winner = game.winner;
  return (
    <section
      aria-labelledby="result-heading"
      data-testid="result"
      className="panel mt-4 border-accent p-4"
    >
      <h2 id="result-heading" className="text-lg font-bold">
        {t("result.title")}
      </h2>
      {winner && <p className="mt-1">{t("result.winner", { team: teamName(language, winner) })}</p>}
      {winner && (
        <p>{winner === "northSouth" ? t("result.youWon") : t("result.youLost")}</p>
      )}
      <p className="mt-1">
        {t("result.finalScore", { northSouth: game.matchScore.northSouth, eastWest: game.matchScore.eastWest })}
      </p>
      {!hostOnly && (
        <button type="button" className={`${primaryButtonClass} mt-3`} disabled={!canStart} onClick={onPlayAgain}>
          {t("game.playAgain")}
        </button>
      )}
    </section>
  );
}
