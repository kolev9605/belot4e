import type { MessageKey } from "../i18n";
import { rotateSeat } from "../protocol/perspective";
import type { IntentDto, PlayedCard, RoomState, Seat, Team, TableView } from "../protocol/types";
import type { AppState } from "./reducer";

/** Display helpers only. Legality always comes from the server's legalActions. */

export function playableCards(game: TableView | null): Set<string> {
  const cards = new Set<string>();
  for (const action of game?.legalActions ?? []) {
    if (action.kind === "play" && action.card) cards.add(action.card);
  }
  return cards;
}

export function actionsOfKind(game: TableView | null, ...kinds: IntentDto["kind"][]): IntentDto[] {
  return (game?.legalActions ?? []).filter((a) => kinds.includes(a.kind));
}

export function bidActions(game: TableView | null): IntentDto[] {
  return actionsOfKind(game, "bid", "pass", "contra", "recontra");
}

export function announceActions(game: TableView | null): IntentDto[] {
  return actionsOfKind(game, "announce");
}

export function isMyTurn(game: TableView | null): boolean {
  return !!game && game.phase !== "finished" && game.legalActions.length > 0;
}

/** True while the room is waiting for a missing player (nobody can act). */
export function isPaused(room: RoomState | null): boolean {
  return !!room && room.lifecycle === "ingame" && room.missing.length > 0;
}

/** True when the page may send an intent right now. */
export function canAct(state: AppState): boolean {
  return (
    state.connection === "connected" &&
    state.session === "playing" &&
    !state.blocked &&
    !state.superseded &&
    !isPaused(state.room) &&
    state.pending === null &&
    isMyTurn(state.game)
  );
}

type Translate = (key: MessageKey, vars?: Record<string, string | number>) => string;

/**
 * Names to show for seats and teams, keyed by the viewer-relative seat the table is drawn with.
 * Empty without a room, so the single-player table keeps the compass names.
 */
export function seatLabels(
  room: RoomState | null,
  t: Translate,
): { seats: Partial<Record<Seat, string>>; teams: Partial<Record<Team, string>> } {
  if (!room) return { seats: {}, teams: {} };
  const viewer = room.you.seat;
  const seats: Partial<Record<Seat, string>> = {};
  for (const s of room.seats) {
    const shown = viewer ? rotateSeat(s.seat, viewer) : s.seat;
    if (s.kind === "human" && s.name) seats[shown] = s.name;
    else if (s.kind === "bot") seats[shown] = s.botFor ? t("seat.botFor", { name: s.botFor }) : t("seat.bot");
  }
  const of = (seat: Seat): string => seats[seat] ?? t(`seat.${seat}` as MessageKey);
  return {
    seats,
    teams: {
      northSouth: `${of("south")} & ${of("north")}`,
      eastWest: `${of("west")} & ${of("east")}`,
    },
  };
}

export interface TrickToShow {
  plays: PlayedCard[];
  winner: string | null;
  completed: boolean;
}

export function trickToShow(game: TableView | null): TrickToShow {
  if (!game) return { plays: [], winner: null, completed: false };
  if (game.currentTrick.length > 0) return { plays: game.currentTrick, winner: null, completed: false };
  if (game.lastCompletedTrick) {
    return { plays: game.lastCompletedTrick.plays, winner: game.lastCompletedTrick.winner, completed: true };
  }
  return { plays: [], winner: null, completed: false };
}

export function sameIntent(a: IntentDto, b: IntentDto): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}
