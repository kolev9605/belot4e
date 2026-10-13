import { useCallback, useEffect, useRef, useState } from "react";
import type { ConnectionStatus, TableView } from "../protocol/types";
import type { PacingConfig } from "../theme/motion";
import { diffViews, type Frame, type PresentationStep } from "./steps";

export interface PresenterOutput {
  /** The frame being drawn, or null when the table should simply draw the latest view. */
  frame: Frame | null;
  /** True when nothing is queued or running and the drawn frame is the latest view. */
  settled: boolean;
  /** Lets the presenter move on from a blocking step (the hand-result pop-up). */
  release: () => void;
}

/** How long a step stays on screen before the next one may replace it. */
export function stepDuration(step: PresentationStep, pacing: PacingConfig, scale = 1): number {
  let ms = 0;
  switch (step.kind) {
    case "deal":
      ms = step.frame.perSeat * 4 * pacing.dealStaggerMs + pacing.dealTravelMs;
      break;
    case "play":
      ms = pacing.playTravelMs;
      break;
    case "hold":
      ms = pacing.trickHoldMs;
      break;
    case "collect":
      ms = pacing.collectMs;
      break;
    case "snap":
      ms = pacing.snapMs;
      break;
    case "bid":
      // The person's own call is never delayed: only the bots' calls are held for reading.
      ms = step.frame.view.biddingHistory.at(-1)?.seat === "south" ? 0 : pacing.bidHoldMs;
      break;
    default:
      ms = 0;
  }
  return Math.round(ms * scale);
}

/** With more than three steps waiting, shorten holds so the display never falls far behind. */
export function compressionScale(queued: number): number {
  return queued > 3 ? Math.max(0.4, 3 / queued) : 1;
}

/**
 * Presentation layer between the store's latest view and what the table draws. It only delays
 * drawing; it never changes which actions are legal (the store owns that).
 */
export function usePresenter(
  game: TableView | null,
  connection: ConnectionStatus,
  active: boolean,
  pacing: PacingConfig,
): PresenterOutput {
  const [frame, setFrame] = useState<Frame | null>(null);
  const [busy, setBusy] = useState(false);
  const queue = useRef<PresentationStep[]>([]);
  const running = useRef(false);
  /** A blocking step (the result pop-up) is showing; the queue waits for `release`. */
  const blocking = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const basis = useRef<TableView | null>(null);
  const pacingRef = useRef(pacing);
  pacingRef.current = pacing;

  const clear = useCallback(() => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    queue.current = [];
    running.current = false;
    blocking.current = false;
    basis.current = null;
    setFrame(null);
    setBusy(false);
  }, []);

  const pump = useCallback(() => {
    if (running.current) return;
    let last: PresentationStep | null = null;
    while (queue.current.length > 0) {
      const step = queue.current.shift()!;
      if (step.kind === "result") {
        setFrame(step.frame);
        running.current = true;
        blocking.current = true;
        setBusy(true);
        return;
      }
      const ms = stepDuration(step, pacingRef.current, compressionScale(queue.current.length + 1));
      last = step;
      if (ms <= 0) continue;
      setFrame(step.frame);
      running.current = true;
      setBusy(true);
      timer.current = setTimeout(() => {
        timer.current = null;
        running.current = false;
        pump();
      }, ms);
      return;
    }
    if (last) setFrame(last.frame);
    setBusy(false);
  }, []);

  const release = useCallback(() => {
    if (!blocking.current) return;
    blocking.current = false;
    running.current = false;
    if (queue.current.length === 0) {
      setFrame(null);
      setBusy(false);
      return;
    }
    pump();
  }, [pump]);

  useEffect(() => {
    if (!active || !game) {
      if (basis.current !== null || queue.current.length > 0 || running.current) clear();
      return;
    }
    if (basis.current === game) return;
    const steps = diffViews(basis.current, game);
    basis.current = game;
    if (steps.length === 0) {
      if (!running.current && queue.current.length === 0) setFrame(null);
      return;
    }
    queue.current.push(...steps);
    pump();
  }, [game, active, clear, pump]);

  useEffect(() => {
    if (connection !== "connected") clear();
  }, [connection, clear]);

  useEffect(
    () => () => {
      if (timer.current) clearTimeout(timer.current);
    },
    [],
  );

  const settled = !busy && queue.current.length === 0 && (frame === null || frame.view === game);
  return { frame, settled, release };
}
