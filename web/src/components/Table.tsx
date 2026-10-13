import { useEffect, useRef, useState } from "react";
import type { TableView } from "../protocol/types";
import { usePresented } from "../presentation/PresenterProvider";
import { canAct } from "../state/selectors";
import { useStore } from "../state/StoreProvider";
import { useLanguage } from "../i18n/LanguageProvider";
import { AnnouncePanel, BidPanel } from "./ActionPanels";
import { NoticeBar, ResultPanel, ScoreBoard, StartPanel } from "./Chrome";
import { Hand } from "./Hand";
import { HandResultDialog } from "./HandResultDialog";
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
  const presented = usePresented();
  const latest = state.game;
  const game = presented.view;
  const frame = presented.frame;
  const voided = useVoidedHand(latest);
  const connected = state.connection === "connected";
  // Input needs both the store's go-ahead and a table that has finished showing what happened.
  const enabled = canAct(state) && presented.settled;

  // The live region follows the latest truth, not the animation, so assistive technology is never behind.
  const announcement = liveMessage(latest, language, t);

  let body;
  if (state.room && (!game || !latest)) {
    // In a room the game starts from the lobby; until the first view arrives the cards are being dealt.
    body = (
      <section className="panel mt-4 p-4" data-testid="room-dealing">
        <p>{t("room.dealing")}</p>
      </section>
    );
  } else if (!game || !latest || state.session === "idle" || state.session === "starting" || state.session === "lost") {
    body = (
      <StartPanel
        starting={state.session === "starting"}
        canStart={connected && !state.blocked}
        lost={state.session === "lost" ? state.lostReason ?? "unknown" : null}
        onStart={startGame}
      />
    );
  } else {
    const dealing = frame?.dealing ?? false;
    const perSeat = frame?.perSeat ?? 0;
    const trick = frame?.trick ?? { plays: game.currentTrick, winner: null, mode: "live" as const };
    const toAct = game.phase !== "finished" ? game.toAct : null;
    const myTurn = presented.settled && game.legalActions.length > 0;
    // Cards read as playable / not playable only while the person is the one to play a card.
    const choosingCard = game.phase === "playing" && game.legalActions.some((a) => a.kind === "play");
    const status =
      state.pending !== null
        ? t("status.pending")
        : dealing
          ? t("label.dealing")
          : game.phase === "finished"
            ? t("status.finished")
            : myTurn
              ? t("status.yourTurn")
              : toAct === "south"
                ? t("status.playing")
                : toAct
                  ? t("status.waitingFor", { seat: seatName(language, toAct) })
                  : t("status.finished");
    body = (
      <>
        <ScoreBoard game={game} />
        <div className="grid min-h-0 flex-1 grid-cols-[auto_minmax(0,1fr)_auto] grid-rows-[auto_minmax(0,1fr)] items-stretch gap-2">
          <div className="col-span-3 flex justify-center">
            <SeatPanel seat="north" game={game} dealing={dealing} perSeat={perSeat} />
          </div>
          <div className="flex min-h-0">
            <SeatPanel seat="west" game={game} dealing={dealing} perSeat={perSeat} />
          </div>
          {game.phase === "bidding" ? (
            <div className="felt flex min-h-0 items-center justify-center rounded-2xl p-2">
              <BidPanel game={game} enabled={enabled} onAct={submit} voided={voided} />
            </div>
          ) : (
            <TrickArea trick={trick} />
          )}
          <div className="flex min-h-0">
            <SeatPanel seat="east" game={game} dealing={dealing} perSeat={perSeat} />
          </div>
        </div>
        {/* Reserved in every phase so the felt keeps one size whether or not there is anything to declare. */}
        <div className="min-h-8">
          {game.phase !== "bidding" && <AnnouncePanel game={game} enabled={enabled} onAct={submit} />}
        </div>
        <div className="flex flex-col items-center gap-1 sm:flex-row sm:justify-center sm:gap-x-4">
          <SeatPanel seat="south" game={game} dealing={dealing} perSeat={perSeat} />
          <p data-testid="status" className="flex min-h-6 items-center justify-center text-center text-base font-semibold sm:min-h-12 sm:w-44">
            {status}
          </p>
        </div>
        <Hand
          game={game}
          enabled={enabled}
          onAct={submit}
          dealing={dealing}
          dealer={game.dealer}
          playing={choosingCard}
        />
        {state.session === "finished" && presented.settled && (
          <ResultPanel
            game={latest}
            canStart={connected}
            onPlayAgain={startGame}
            hostOnly={state.room !== null && !state.room.you.isHost}
          />
        )}
        {frame?.result && (
          <HandResultDialog
            key={frame.result.dealNumber}
            summary={frame.result}
            onDismiss={presented.release}
            onPlayAgain={frame.result.winner && !(state.room && !state.room.you.isHost) ? startGame : undefined}
            canStart={connected}
          />
        )}
      </>
    );
  }

  return (
    <div className="relative flex min-h-0 flex-1 flex-col gap-2">
      {/* Floats over the table, so a notice never pushes the seats and felt around. */}
      <div className="absolute inset-x-0 top-0 z-20">
        <NoticeBar notice={state.notice} onDismiss={dismissNotice} />
      </div>
      {body}
      <div className="sr-only" role="status" aria-live="polite" data-testid="live-region">
        {announcement}
      </div>
    </div>
  );
}
