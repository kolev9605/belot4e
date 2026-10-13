import { m } from "motion/react";
import type { Seat } from "../protocol/types";
import { dealOrder, travelFrom } from "../theme/geometry";
import { EASE_OUT } from "../theme/motion";
import { useMotionPrefs } from "../theme/useMotionPrefs";
import { CardBack } from "./Card";

interface CardFanProps {
  seat: Seat;
  count: number;
  vertical: boolean;
  dealing: boolean;
  dealer: Seat;
  /** Cards each seat is receiving in the running deal step. */
  perSeat: number;
}

/** An opponent's remaining cards as a compact stack of backs. Decorative: the count is text on the seat. */
export function CardFan({ seat, count, vertical, dealing, dealer, perSeat }: CardFanProps) {
  const { pacing, instant, reduced, seconds } = useMotionPrefs();
  const origin = travelFrom(dealer, seat, 90);
  const turn = dealOrder(seat, dealer);
  const firstNew = Math.max(0, count - perSeat);

  return (
    <div
      aria-hidden="true"
      data-testid={`fan-${seat}`}
      data-count={count}
      className={`flex ${vertical ? "flex-col" : "flex-row"} items-center justify-center`}
    >
      {Array.from({ length: count }, (_, i) => {
        const flyIn = dealing && !instant && i >= firstNew;
        const delay = seconds(((i - firstNew) * 4 + turn) * pacing.dealStaggerMs);
        return (
          <m.span
            key={i}
            style={i === 0 ? undefined : vertical ? { marginTop: "calc(var(--card-h) * -0.3)" } : { marginLeft: "calc(var(--card-w) * -0.3)" }}
            initial={flyIn ? (reduced ? { opacity: 0 } : { opacity: 0, x: origin.x, y: origin.y }) : false}
            animate={{ opacity: 1, x: 0, y: 0 }}
            transition={{ duration: seconds(pacing.dealTravelMs), ease: EASE_OUT, delay: flyIn ? delay : 0 }}
          >
            <CardBack className="h-[calc(var(--card-h)*0.5)] w-[calc(var(--card-w)*0.5)]" />
          </m.span>
        );
      })}
    </div>
  );
}
