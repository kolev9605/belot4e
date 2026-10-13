import type { Seat as SeatId, TableView } from "../protocol/types";
import { useLanguage } from "../i18n/LanguageProvider";
import { seatName } from "./labels";

export function SeatPanel({ seat, game }: { seat: SeatId; game: TableView }) {
  const { language, t } = useLanguage();
  const role =
    seat === "south" ? t("label.you") : seat === "north" ? t("label.partner") : t("label.opponent");
  const toAct = game.phase !== "finished" && game.toAct === seat;
  const count = game.handSizes[seat];
  return (
    <div
      data-testid={`seat-${seat}`}
      className={`rounded border-2 px-2 py-1 text-center ${
        toAct ? "border-blue-800 bg-blue-50 font-semibold" : "border-slate-400 bg-white"
      }`}
    >
      <div>
        {seatName(language, seat)} <span className="text-sm text-slate-700">({role})</span>
      </div>
      <div className="text-sm">{t("label.cards", { count })}</div>
      {game.dealer === seat && <div className="text-xs">{t("label.dealer")}</div>}
      {toAct && <div className="text-xs">{t("label.turn")}</div>}
    </div>
  );
}
