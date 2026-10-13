import type { Seat as SeatId, TableView } from "../protocol/types";
import { useLanguage } from "../i18n/LanguageProvider";
import { isMyTeam } from "../theme/geometry";
import { CardBack } from "./Card";
import { CardFan } from "./CardFan";
import { DealerMarker } from "./DealerMarker";
import { callLabel, seatName } from "./labels";

interface SeatPanelProps {
  seat: SeatId;
  game: TableView;
  dealing?: boolean;
  perSeat?: number;
}

export function SeatPanel({ seat, game, dealing = false, perSeat = 0 }: SeatPanelProps) {
  const { language, t } = useLanguage();
  const role = seat === "south" ? t("label.you") : seat === "north" ? t("label.partner") : t("label.opponent");
  const toAct = game.phase !== "finished" && game.toAct === seat;
  const count = game.handSizes[seat];
  const mine = isMyTeam(seat);
  const isDealer = game.dealer === seat;
  const side = seat === "west" || seat === "east";
  // Every call this seat made in the current bidding round, in the order made.
  const calls = game.biddingHistory.filter((entry) => entry.seat === seat);

  const callChips = (
    <ul
      aria-label={t("label.calls")}
      data-testid={`seat-calls-${seat}`}
      className={`flex items-center gap-1 ${side ? "flex-wrap justify-center" : "flex-nowrap [direction:ltr]"}`}
    >
      {calls.map((entry, i) => {
        const latest = i === calls.length - 1;
        return (
          <li
            key={i}
            data-latest={latest ? "true" : undefined}
            className={`chip shrink-0 ${latest ? "chip-accent font-semibold" : "text-muted"}`}
          >
            {callLabel(language, entry)}
          </li>
        );
      })}
    </ul>
  );

  // The box never changes size: side seats fill the table row, top and bottom seats have a fixed
  // width and reserved rows, and anything that does not fit is clipped (the newest call stays visible).
  return (
    <div
      data-testid={`seat-${seat}`}
      data-turn={toAct ? "true" : undefined}
      className={`panel relative flex flex-col items-center gap-0.5 overflow-hidden px-2 py-1 text-center ${
        toAct ? "!border-accent shadow-[0_0_0_2px_var(--color-accent)]" : ""
      } ${side ? "h-full w-[5.5rem] sm:w-28" : "w-64 max-w-full"}`}
    >
      <div
        className={`flex items-center justify-center gap-x-1 text-sm font-semibold leading-tight ${
          side ? "min-h-[2.2rem] flex-wrap" : "w-full"
        }`}
      >
        <span className={side ? "" : "truncate"}>{seatName(language, seat)}</span>
        <span className="shrink-0 text-xs font-normal text-muted">({role})</span>
      </div>
      <div
        className={`flex items-center justify-center gap-1 ${
          side ? "min-h-[3.2rem] flex-wrap content-start" : "h-6 w-full flex-nowrap overflow-hidden"
        }`}
      >
        <span className={`chip shrink-0 ${mine ? "" : "border-dashed"}`}>{mine ? t("label.yourTeam") : t("label.theirTeam")}</span>
        {isDealer && <DealerMarker />}
        {toAct && <span className="chip chip-accent shrink-0">{t("label.turn")}</span>}
      </div>
      {side ? (
        calls.length > 0 && callChips
      ) : (
        <div className="flex h-6 w-full items-center justify-center-safe overflow-hidden [direction:rtl]">
          {calls.length > 0 && callChips}
        </div>
      )}
      {seat !== "south" && (
        <div
          className={`flex items-center justify-center overflow-hidden ${
            side ? "min-h-0 flex-1" : "h-[calc(var(--card-h)*0.5)]"
          }`}
        >
          <CardFan
            seat={seat}
            count={count}
            vertical={side}
            dealing={dealing}
            dealer={game.dealer}
            perSeat={perSeat}
          />
        </div>
      )}
      <span className="shrink-0 text-xs text-muted">{t("label.cards", { count })}</span>
      {dealing && isDealer && (
        <div className="absolute right-1 top-1 h-9 w-7" data-testid="deal-origin">
          <CardBack className="absolute left-0 top-1 h-8 w-6 rotate-[-4deg]" />
          <CardBack className="absolute left-0.5 top-0.5 h-8 w-6" />
          <CardBack className="absolute left-1 top-0 h-8 w-6 rotate-[4deg]" />
        </div>
      )}
    </div>
  );
}
