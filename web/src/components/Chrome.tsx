import type { ConnectionStatus, TableView } from "../protocol/types";
import type { LostReason, Notice as NoticeState } from "../state/reducer";
import { rejectionMessage, type MessageKey } from "../i18n";
import { useLanguage } from "../i18n/LanguageProvider";
import { contractName, seatName, teamName } from "./labels";

const buttonClass =
  "min-h-11 rounded border-2 border-slate-900 bg-white px-4 py-2 font-semibold hover:bg-yellow-100 " +
  "focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-blue-700 " +
  "disabled:border-dashed disabled:border-slate-400 disabled:bg-slate-100 disabled:text-slate-600";

export function ConnectionBanner({ status, showStale }: { status: ConnectionStatus; showStale: boolean }) {
  const { t } = useLanguage();
  const ok = status === "connected";
  return (
    <div
      role="status"
      aria-live="polite"
      data-testid="connection-banner"
      data-status={status}
      className={`rounded border-2 px-3 py-2 text-sm ${
        ok ? "border-slate-400 bg-white" : "border-amber-800 bg-amber-100 font-semibold"
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
    <section aria-label={t("label.score")} className="rounded border-2 border-slate-400 bg-white p-2 text-sm">
      <div className="flex flex-wrap justify-between gap-x-4">
        <span data-testid="score-ns">
          {teamName(language, "northSouth")}: <strong>{game.matchScore.northSouth}</strong>
        </span>
        <span data-testid="score-ew">
          {teamName(language, "eastWest")}: <strong>{game.matchScore.eastWest}</strong>
        </span>
      </div>
      <div className="mt-1 flex flex-wrap justify-between gap-x-4">
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
      className="mt-3 flex items-start justify-between gap-2 rounded border-2 border-red-900 bg-red-50 p-2"
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
    <section className="mt-4 rounded border-2 border-slate-400 bg-white p-4">
      {lost && (
        <p role="status" data-testid="game-lost" className="mb-3 font-semibold">
          {lost === "replaced" ? t("game.lostReplaced") : t("game.lostConnection")} {t("game.lost")}
        </p>
      )}
      {!lost && <p className="mb-3">{starting ? t("game.starting") : t("game.idle")}</p>}
      <button type="button" className={buttonClass} disabled={!canStart || starting} onClick={onStart}>
        {t("game.start")}
      </button>
    </section>
  );
}

export function ResultPanel({ game, onPlayAgain, canStart }: { game: TableView; onPlayAgain: () => void; canStart: boolean }) {
  const { language, t } = useLanguage();
  const winner = game.winner;
  return (
    <section
      aria-labelledby="result-heading"
      data-testid="result"
      className="mt-4 rounded border-2 border-slate-900 bg-white p-4"
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
      <button type="button" className={`${buttonClass} mt-3`} disabled={!canStart} onClick={onPlayAgain}>
        {t("game.playAgain")}
      </button>
    </section>
  );
}
