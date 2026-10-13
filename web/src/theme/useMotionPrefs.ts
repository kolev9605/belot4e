import { useReducedMotion } from "motion/react";
import { usePacing } from "../presentation/PacingProvider";
import { isInstant, type PacingConfig } from "./motion";

export interface MotionPrefs {
  pacing: PacingConfig;
  /** Pacing is all zero (tests, e2e): render static, with no presence or layout animation. */
  instant: boolean;
  /** The person asked the system for less motion: fade instead of travel. */
  reduced: boolean;
  /** Seconds, for motion transitions. */
  seconds: (ms: number) => number;
}

export function useMotionPrefs(): MotionPrefs {
  const pacing = usePacing();
  const reduced = useReducedMotion() ?? false;
  return { pacing, instant: isInstant(pacing), reduced, seconds: (ms) => ms / 1000 };
}
