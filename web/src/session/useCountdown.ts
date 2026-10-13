import { useEffect, useState } from "react";

/**
 * Whole seconds left until `deadlineUtc`, corrected by the server clock offset so every device shows
 * the same number regardless of its own clock. Re-evaluated every second and never below zero.
 */
export function useCountdown(deadlineUtc: string | null, clockOffsetMs: number): number {
  const compute = () =>
    deadlineUtc === null ? 0 : Math.max(0, Math.ceil((Date.parse(deadlineUtc) - (Date.now() + clockOffsetMs)) / 1000));
  const [seconds, setSeconds] = useState(compute);

  useEffect(() => {
    setSeconds(compute());
    if (deadlineUtc === null) return;
    const id = window.setInterval(() => setSeconds(compute()), 1000);
    return () => window.clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [deadlineUtc, clockOffsetMs]);

  return seconds;
}
