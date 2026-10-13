import type { IntentDto, PlayedCard, TableView } from "../protocol/types";
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

/** True when the page may send an intent right now. */
export function canAct(state: AppState): boolean {
  return (
    state.connection === "connected" &&
    state.session === "playing" &&
    !state.blocked &&
    state.pending === null &&
    isMyTurn(state.game)
  );
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
