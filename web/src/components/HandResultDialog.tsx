import { useEffect, useRef } from "react";
import type { HandSummary, Team, TeamHand } from "../protocol/types";
import { useLanguage } from "../i18n/LanguageProvider";
import type { MessageKey } from "../i18n";
import { TableCard } from "./Card";
import { contractName, seatName, teamName } from "./labels";

interface HandResultDialogProps {
  summary: HandSummary;
  /** Continues the game (or closes the final result). */
  onDismiss: () => void;
  /** Present only when the game is over: starts a new game. */
  onPlayAgain?: () => void;
  canStart?: boolean;
}

/** The person always sits south, so their team is North-South. */
const MY_TEAM: Team = "northSouth";
const TEAMS: Team[] = ["northSouth", "eastWest"];

function Row({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="text-muted">{label}</dt>
      <dd className="font-semibold tabular-nums">{value}</dd>
    </div>
  );
}

function TeamSection({ team, hand, summary }: { team: Team; hand: TeamHand; summary: HandSummary }) {
  const { language, t } = useLanguage();
  const mine = team === MY_TEAM;
  const total = summary.matchScore[team];
  return (
    <section
      aria-label={teamName(language, team)}
      data-testid={`hand-result-${team}`}
      className={`rounded-lg border p-2 ${mine ? "border-accent" : "border-line border-dashed"}`}
    >
      <h3 className="flex flex-wrap items-center gap-2 text-sm font-semibold">
        {teamName(language, team)}
        <span className={`chip ${mine ? "chip-accent" : ""}`}>{mine ? t("label.yourTeam") : t("label.theirTeam")}</span>
      </h3>
      <p className="mt-1 text-xs text-muted">{t("handResult.wonCards", { count: hand.wonCards.length })}</p>
      {hand.wonCards.length === 0 ? (
        <p className="py-2 text-sm text-muted">{t("handResult.noCards")}</p>
      ) : (
        <ul className="my-1 flex flex-wrap" data-testid={`won-cards-${team}`}>
          {hand.wonCards.map((card, i) => (
            <li key={card} style={{ marginRight: "calc(var(--card-w) * -0.3)" }} data-index={i}>
              <TableCard card={card} size="small" />
            </li>
          ))}
        </ul>
      )}
      <dl className="mt-1 text-sm">
        <Row label={t("handResult.cardPoints")} value={hand.cardPoints} />
        {hand.lastTrickBonus > 0 && <Row label={t("handResult.lastTrick")} value={hand.lastTrickBonus} />}
        {hand.announcementPoints > 0 && <Row label={t("handResult.announcements")} value={hand.announcementPoints} />}
        {hand.belotePoints > 0 && <Row label={t("handResult.belote")} value={hand.belotePoints} />}
        {hand.allTricksBonus > 0 && <Row label={t("handResult.allTricks")} value={hand.allTricksBonus} />}
        <Row label={t("handResult.rawTotal")} value={hand.rawTotal} />
        <div className="mt-1 border-t border-line pt-1">
          <Row label={t("handResult.handScore")} value={hand.gamePoints} />
          <Row label={t("handResult.gameTotal")} value={total} />
        </div>
      </dl>
    </section>
  );
}

/** Pop-up shown after every scored hand: what each team won, how it scored, and the game standing. */
export function HandResultDialog({ summary, onDismiss, onPlayAgain, canStart = true }: HandResultDialogProps) {
  const { language, t } = useLanguage();
  const ref = useRef<HTMLDialogElement>(null);
  const primary = useRef<HTMLButtonElement>(null);
  const finished = summary.winner !== null;

  useEffect(() => {
    const el = ref.current;
    if (!el || el.open) return;
    if (typeof el.showModal === "function") el.showModal();
    else el.setAttribute("open", "");
    primary.current?.focus();
  }, []);

  const outcomeKey = `handResult.${summary.outcome}` as MessageKey;
  const contract = summary.contract;

  return (
    <dialog
      ref={ref}
      aria-labelledby="hand-result-heading"
      data-testid="hand-result"
      data-final={finished ? "true" : undefined}
      onCancel={(e) => {
        e.preventDefault();
        onDismiss();
      }}
      className="m-auto max-h-[94dvh] w-[min(94vw,46rem)] overflow-auto rounded-xl border border-accent bg-surface p-3 text-ink backdrop:bg-black/70"
    >
      <h2 id="hand-result-heading" className="text-lg font-bold">
        {t("handResult.title", { number: summary.dealNumber })}
      </h2>
      <p className="text-sm">
        {t("handResult.contract", { contract: contractName(language, contract.kind), declarer: t("label.declarer", { seat: seatName(language, contract.declarer) }) })}
        {contract.multiplier >= 2 && <span> ({contract.multiplier >= 4 ? t("label.redoubled") : t("label.doubled")})</span>}
        {" · "}
        <strong data-testid="hand-result-outcome">{t(outcomeKey)}</strong>
      </p>
      {summary.heldOver.newlyHeld > 0 && (
        <p className="text-sm text-warn">{t("label.heldOver", { points: summary.heldOver.after })}</p>
      )}
      <div className="mt-2 grid gap-2 sm:grid-cols-2">
        {TEAMS.map((team) => (
          <TeamSection key={team} team={team} hand={summary.teams[team]} summary={summary} />
        ))}
      </div>
      {finished && summary.winner && (
        <div className="mt-2" data-testid="hand-result-winner">
          <p className="font-bold">{t("result.winner", { team: teamName(language, summary.winner) })}</p>
          <p>{summary.winner === MY_TEAM ? t("result.youWon") : t("result.youLost")}</p>
        </div>
      )}
      <div className="mt-3 flex flex-wrap justify-end gap-2">
        {finished && onPlayAgain ? (
          <>
            <button type="button" className="btn" onClick={onDismiss}>
              {t("handResult.close")}
            </button>
            <button
              ref={primary}
              type="button"
              className="btn btn-primary"
              disabled={!canStart}
              onClick={() => {
                onPlayAgain();
                onDismiss();
              }}
            >
              {t("game.playAgain")}
            </button>
          </>
        ) : (
          <button ref={primary} type="button" className="btn btn-primary" onClick={onDismiss}>
            {t("handResult.continue")}
          </button>
        )}
      </div>
    </dialog>
  );
}
