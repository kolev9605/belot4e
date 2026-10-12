import { useEffect, useRef, useState } from "react";
import type { TableView } from "../protocol/types";
import { canAct } from "../state/selectors";
import { useStore } from "../state/StoreProvider";
import { useLanguage } from "../i18n/LanguageProvider";
import { AnnouncePanel, BidPanel } from "./ActionPanels";
import { NoticeBar, ResultPanel, ScoreBoard, StartPanel } from "./Chrome";
import { Hand } from "./Hand";
import { SeatPanel } from "./Seat";
import { TrickArea } from "./TrickArea";
import { seatName, teamName } from "./labels";

/** True when the table just moved from a bidding view with bids to a fresh, empty bidding view. */
function useVoidedHand(game: TableView | null): boolean {
  const previous = useRef<TableView | null>(null);
  const [voided, setVoided] = useState(false);
  useEffect(() => {
    const before = previous.current;
    if (game && before) {
      if (before.phase === "bidding" && game.phase === "bidding" && before.biddingHistory.length > 0 && game.biddingHistory.length === 0) {
        setVoided(true);
      } else if (game.phase !== "bidding" || game.biddingHistory.length > 0) {
        setVoided(false);
      }
    }
    if (!game) setVoided(false);
    previous.current = game;
  }, [game]);
  return voided;
}

function liveMessage(game: TableView | null, language: ReturnType<typeof useLanguage>["language"], t: ReturnType<typeof useLanguage>["t"]): string {
  if (!game) return "";
  if (game.phase === "finished") {
    return game.winner ? t("live.gameOver", { team: teamName(language, game.winner) }) : "";
  }
  const parts: string[] = [];
  const last = game.lastCompletedTrick;
  if (last && game.currentTrick.length === 0) parts.push(t("live.trickWon", { seat: seatName(language, last.winner) }));
  if (game.legalActions.length > 0) parts.push(t("live.yourTurn"));
  return parts.join(" ");
}

export function Table() {
  const { state, startGame, submit, dismissNotice } = useStore();
  const { language, t } = useLanguage();
  const game = state.game;
  const voided = useVoidedHand(game);
  const connected = state.connection === "connected";
  const enabled = canAct(state);

  const announcement = liveMessage(game, language, t);

  let body;
  if (!game || state.session === "idle" || state.session === "starting" || state.session === "lost") {
    body = (
      <StartPanel
        starting={state.session === "starting"}
        canStart={connected && !state.blocked}
        lost={state.session === "lost" ? state.lostReason ?? "unknown" : null}
        onStart={startGame}
      />
    );
  } else {
    const toAct = game.phase !== "finished" ? game.toAct : null;
    const status =
      state.pending !== null
        ? t("status.pending")
        : game.legalActions.length > 0
          ? t("status.yourTurn")
          : toAct
            ? t("status.waitingFor", { seat: seatName(language, toAct) })
            : t("status.finished");
    body = (
      <>
        <ScoreBoard game={game} />
        <div className="mt-3 grid grid-cols-3 gap-2">
          <SeatPanel seat="west" game={game} />
          <SeatPanel seat="north" game={game} />
          <SeatPanel seat="east" game={game} />
        </div>
        <TrickArea game={game} />
        <p data-testid="status" className="mt-3 text-lg font-semibold">
          {status}
        </p>
        {game.phase === "bidding" && <BidPanel game={game} enabled={enabled} onAct={submit} voided={voided} />}
        {game.phase !== "bidding" && <AnnouncePanel game={game} enabled={enabled} onAct={submit} />}
        <div className="mt-3">
          <SeatPanel seat="south" game={game} />
        </div>
        <Hand game={game} enabled={enabled} onAct={submit} />
        {state.session === "finished" && <ResultPanel game={game} canStart={connected} onPlayAgain={startGame} />}
      </>
    );
  }

  return (
    <div>
      <NoticeBar notice={state.notice} onDismiss={dismissNotice} />
      {body}
      <div className="sr-only" role="status" aria-live="polite" data-testid="live-region">
        {announcement}
      </div>
    </div>
  );
}
