/** Single source of presentation timings (see specs/005-modern-game-ui/contracts/presentation.md). */
export interface PacingConfig {
  dealStaggerMs: number;
  dealTravelMs: number;
  playTravelMs: number;
  trickHoldMs: number;
  collectMs: number;
  snapMs: number;
}

export const DEFAULT_PACING: PacingConfig = {
  dealStaggerMs: 60,
  dealTravelMs: 350,
  playTravelMs: 350,
  trickHoldMs: 1400,
  collectMs: 450,
  snapMs: 150,
};

export const INSTANT_PACING: PacingConfig = {
  dealStaggerMs: 0,
  dealTravelMs: 0,
  playTravelMs: 0,
  trickHoldMs: 0,
  collectMs: 0,
  snapMs: 0,
};

export function isInstant(config: PacingConfig): boolean {
  return Object.values(config).every((v) => v === 0);
}

/** `VITE_PACING=instant` (unit tests) or `?pace=instant` in the page address (e2e) turns all pauses off. */
export function pacingFromEnv(): PacingConfig {
  if (import.meta.env.VITE_PACING === "instant") return INSTANT_PACING;
  try {
    if (new URLSearchParams(window.location.search).get("pace") === "instant") return INSTANT_PACING;
  } catch {
    // no window (should not happen in the browser); fall through to the calm default
  }
  return DEFAULT_PACING;
}

export const EASE_OUT = [0.22, 1, 0.36, 1] as const;
