import { createContext, useContext, useMemo, type ReactNode } from "react";
import type { TableView } from "../protocol/types";
import { useStore } from "../state/StoreProvider";
import { usePacing } from "./PacingProvider";
import type { Frame } from "./steps";
import { usePresenter } from "./usePresenter";

export interface PresentedTable {
  /** What to draw: the presented frame's view, or the latest view when nothing is being staged. */
  view: TableView | null;
  frame: Frame | null;
  /** Presented view is the latest and no step is running; input may be enabled. */
  settled: boolean;
}

const PresenterContext = createContext<PresentedTable | null>(null);

export function PresenterProvider({ children }: { children: ReactNode }) {
  const { state } = useStore();
  const pacing = usePacing();
  const active = state.session === "playing" || state.session === "finished";
  const { frame, settled } = usePresenter(state.game, state.connection, active, pacing);
  const value = useMemo<PresentedTable>(
    () => ({ view: frame ? frame.view : state.game, frame, settled }),
    [frame, settled, state.game],
  );
  return <PresenterContext.Provider value={value}>{children}</PresenterContext.Provider>;
}

export function usePresented(): PresentedTable {
  const value = useContext(PresenterContext);
  if (!value) throw new Error("usePresented must be used inside PresenterProvider");
  return value;
}
