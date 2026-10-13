import { useEffect, useRef } from "react";
import { AnimatePresence, m } from "motion/react";
import type { IntentDto, Seat, TableView } from "../protocol/types";
import { useLanguage } from "../i18n/LanguageProvider";
import { dealOrder, travelFrom } from "../theme/geometry";
import { EASE_OUT } from "../theme/motion";
import { useMotionPrefs } from "../theme/useMotionPrefs";
import { HandCard, type HandCardMode } from "./Card";
import { groupBySuit } from "./handOrder";

interface HandProps {
  game: TableView;
  enabled: boolean;
  onAct: (intent: IntentDto) => void;
  /** True while the deal animation runs; new cards then fly in from the dealer. */
  dealing?: boolean;
  dealer?: Seat;
  /** True when it is the person's turn to play a card: only then do cards read as playable or not. */
  playing?: boolean;
}

export function Hand({ game, enabled, onAct, dealing = false, dealer, playing = false }: HandProps) {
  const { t } = useLanguage();
  const { pacing, instant, reduced, seconds } = useMotionPrefs();
  const seen = useRef<Set<string>>(new Set());

  // Legality comes only from the server's legal actions; the card itself is echoed back unchanged.
  const playAction = new Map<string, IntentDto>();
  for (const action of game.legalActions) {
    if (action.kind === "play" && action.card) playAction.set(action.card, action);
  }
  const groups = groupBySuit(game.ownHand, game.contract?.kind ?? null);

  // Cards that were not in the hand a moment ago are the ones being dealt right now.
  const fresh = new Set(game.ownHand.filter((c) => !seen.current.has(c)));
  const freshOrder = game.ownHand.filter((c) => fresh.has(c));
  useEffect(() => {
    seen.current = new Set(game.ownHand);
  });

  const origin = travelFrom(dealer ?? "north", "south");
  const turn = dealer ? dealOrder("south", dealer) : 0;

  return (
    <section aria-labelledby="hand-heading">
      <h2 id="hand-heading" className="sr-only">
        {t("label.hand")}
      </h2>
      {game.ownHand.length === 0 ? (
        <p className="text-center text-muted">{t("label.emptyHand")}</p>
      ) : (
        <ul className="flex flex-nowrap items-end justify-center pt-1" data-testid="hand">
          {groups.map((group, groupIndex) => (
            <li
              key={group.suit}
              data-testid={`suit-group-${group.suit}`}
              className="flex flex-col"
              style={{ marginLeft: groupIndex === 0 ? 0 : "var(--suit-gap)" }}
            >
              <ul className="flex">
                <AnimatePresence mode="popLayout" initial={false}>
                  {group.cards.map((card, cardIndex) => {
                    const action = playAction.get(card);
                    const order = freshOrder.indexOf(card);
                    const delay = seconds((Math.max(0, order) * 4 + turn) * pacing.dealStaggerMs);
                    const flyIn = dealing && fresh.has(card) && !instant;
                    const mode: HandCardMode = !playing ? "inert" : action !== undefined ? "playable" : "unplayable";
                    return (
                      <m.li
                        key={card}
                        layout={!instant}
                        style={{ marginLeft: cardIndex === 0 ? 0 : "var(--card-overlap)" }}
                        initial={
                          flyIn
                            ? reduced
                              ? { opacity: 0 }
                              : { opacity: 0, x: origin.x, y: origin.y, scale: 0.6 }
                            : false
                        }
                        animate={{ opacity: 1, x: 0, y: 0, scale: 1 }}
                        exit={instant ? undefined : { opacity: 0, y: -20, transition: { duration: 0.15 } }}
                        transition={{ duration: seconds(pacing.dealTravelMs), ease: EASE_OUT, delay: flyIn ? delay : 0 }}
                      >
                        <HandCard card={card} mode={mode} enabled={enabled} onPlay={() => action && onAct(action)} />
                      </m.li>
                    );
                  })}
                </AnimatePresence>
              </ul>
              {/* A same-height marker under every group, so a trump suit never changes the baseline. */}
              <span
                data-trump={group.trump ? "true" : undefined}
                className={`mx-1 mt-0.5 block h-1 rounded ${group.trump ? "bg-accent" : "bg-transparent"}`}
              >
                {group.trump && <span className="sr-only">{t("label.trumps")}</span>}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
