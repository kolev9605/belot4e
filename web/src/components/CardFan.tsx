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
      className={`flex ${vertical ? "flex-col -space-y-6" : "flex-row -space-x-4 sm:-space-x-5"} items-center justify-center`}
    >
      {Array.from({ length: count }, (_, i) => {
        const flyIn = dealing && !instant && i >= firstNew;
        const delay = seconds(((i - firstNew) * 4 + turn) * pacing.dealStaggerMs);
        return (
          <m.span
            key={i}
            initial={flyIn ? (reduced ? { opacity: 0 } : { opacity: 0, x: origin.x, y: origin.y }) : false}
            animate={{ opacity: 1, x: 0, y: 0 }}
            transition={{ duration: seconds(pacing.dealTravelMs), ease: EASE_OUT, delay: flyIn ? delay : 0 }}
          >
            <CardBack className={vertical ? "h-9 w-6" : "h-9 w-6 sm:h-11 sm:w-8"} />
          </m.span>
        );
      })}
    </div>
  );
}
