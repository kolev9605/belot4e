import type { Seat as SeatId, TableView } from "../protocol/types";
import { useLanguage } from "../i18n/LanguageProvider";
import { isMyTeam } from "../theme/geometry";
import { CardBack } from "./Card";
import { CardFan } from "./CardFan";
import { DealerMarker } from "./DealerMarker";
import { seatName } from "./labels";

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

  return (
    <div
      data-testid={`seat-${seat}`}
      data-turn={toAct ? "true" : undefined}
      className={`panel flex flex-col items-center gap-1 px-2 py-1.5 text-center ${
        toAct ? "!border-accent shadow-[0_0_0_2px_var(--color-accent)]" : ""
      } ${side ? "w-[4.75rem] sm:w-28" : ""}`}
    >
      <div className="flex flex-wrap items-center justify-center gap-x-1 text-sm font-semibold">
        <span>{seatName(language, seat)}</span>
        <span className="text-xs font-normal text-muted">({role})</span>
      </div>
      <div className="flex flex-wrap items-center justify-center gap-1">
        <span className={`chip ${mine ? "" : "border-dashed"}`}>{mine ? t("label.yourTeam") : t("label.theirTeam")}</span>
        {isDealer && <DealerMarker />}
        {toAct && <span className="chip chip-accent">{t("label.turn")}</span>}
      </div>
      {seat !== "south" && (
        <CardFan
          seat={seat}
          count={count}
          vertical={side}
          dealing={dealing}
          dealer={game.dealer}
          perSeat={perSeat}
        />
      )}
      <span className="text-xs text-muted">{t("label.cards", { count })}</span>
      {dealing && isDealer && (
        <div className="relative h-9 w-7" data-testid="deal-origin">
          <CardBack className="absolute left-0 top-1 h-8 w-6 rotate-[-4deg]" />
          <CardBack className="absolute left-0.5 top-0.5 h-8 w-6" />
          <CardBack className="absolute left-1 top-0 h-8 w-6 rotate-[4deg]" />
        </div>
      )}
    </div>
  );
}
