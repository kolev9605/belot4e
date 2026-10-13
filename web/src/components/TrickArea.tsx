import { m } from "motion/react";
import type { Seat } from "../protocol/types";
import type { TrickFrame } from "../presentation/steps";
import { useLanguage } from "../i18n/LanguageProvider";
import { offsetFrom, SEAT_POSITIONS } from "../theme/geometry";
import { EASE_OUT } from "../theme/motion";
import { useMotionPrefs } from "../theme/useMotionPrefs";
import { TableCard } from "./Card";
import { seatName } from "./labels";

const SLOT: Record<Seat, string> = {
  north: "col-start-2 row-start-1",
  west: "col-start-1 row-start-2",
  east: "col-start-3 row-start-2",
  south: "col-start-2 row-start-3",
};

/** Distance (px) from the table centre to a played card's resting place, and to the seat edge. */
const SLOT_SCALE = 44;
const SEAT_SCALE = 170;

interface TrickAreaProps {
  trick: TrickFrame;
}

export function TrickArea({ trick }: TrickAreaProps) {
  const { language, t } = useLanguage();
  const { pacing, instant, reduced, seconds } = useMotionPrefs();

  const winner = trick.winner;
  const showWinner = winner !== null && trick.mode !== "live";

  return (
    <section aria-labelledby="trick-heading" className="felt relative flex min-h-0 flex-col rounded-2xl p-2">
      <h2 id="trick-heading" className="sr-only">
        {t("label.table")}
      </h2>
      <div className="mx-auto grid min-h-0 w-full max-w-[22rem] flex-1 grid-cols-3 grid-rows-3 place-items-center gap-0.5">
        {trick.plays.length === 0 && (
          <p className="col-span-3 col-start-1 row-start-2 text-center text-sm text-white/70">{t("label.noCards")}</p>
        )}
        {trick.plays.map((play) => {
          const rest = offsetFrom(play.seat, 0);
          const from = {
            x: (SEAT_POSITIONS[play.seat].x * SEAT_SCALE) - SEAT_POSITIONS[play.seat].x * SLOT_SCALE,
            y: (SEAT_POSITIONS[play.seat].y * SEAT_SCALE) - SEAT_POSITIONS[play.seat].y * SLOT_SCALE,
          };
          const sweep = {
            x: SEAT_POSITIONS[winner ?? play.seat].x * SEAT_SCALE - SEAT_POSITIONS[play.seat].x * SLOT_SCALE,
            y: SEAT_POSITIONS[winner ?? play.seat].y * SEAT_SCALE - SEAT_POSITIONS[play.seat].y * SLOT_SCALE,
          };
          const isWinner = winner === play.seat;
          const target =
            trick.mode === "collect"
              ? reduced
                ? { x: rest.x, y: rest.y, opacity: 0, scale: 1 }
                : { x: sweep.x, y: sweep.y, opacity: 0, scale: 0.7 }
              : trick.mode === "hold"
                ? { x: 0, y: 0, opacity: isWinner ? 1 : 0.55, scale: isWinner ? 1.08 : 0.96 }
                : { x: 0, y: 0, opacity: 1, scale: 1 };
          return (
            <m.div
              key={`${play.seat}-${play.card}`}
              data-testid={`trick-card-${play.seat}`}
              data-winner={isWinner && showWinner ? "true" : undefined}
              className={`${SLOT[play.seat]} rounded-card ${isWinner && showWinner ? "ring-2 ring-accent" : ""}`}
              initial={instant ? false : reduced ? { opacity: 0 } : { opacity: 0, x: from.x, y: from.y, scale: 0.8 }}
              animate={target}
              transition={{
                duration: seconds(trick.mode === "collect" ? pacing.collectMs : pacing.playTravelMs),
                ease: EASE_OUT,
              }}
            >
              <TableCard card={play.card} />
            </m.div>
          );
        })}
      </div>
      <p
        className="min-h-5 text-center text-sm font-semibold text-accent"
        data-testid="trick-winner"
        aria-live="off"
      >
        {showWinner && winner ? t("label.trickWon", { seat: seatName(language, winner) }) : ""}
      </p>
    </section>
  );
}
