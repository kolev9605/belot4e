import type { RoomSeat, RoomState, Seat } from "../protocol/types";
import { useLanguage } from "../i18n/LanguageProvider";
import { translate, type MessageKey } from "../i18n";

const DISPLAY_ORDER: Seat[] = ["north", "east", "south", "west"];

interface SeatPickerProps {
  room: RoomState;
  onClaim: (seat: Seat) => void;
  onRelease: () => void;
}

/** The four seats with who holds them, and a button for every seat the person may take. */
export function SeatPicker({ room, onClaim, onRelease }: SeatPickerProps) {
  const { language, t } = useLanguage();
  const mySeat = room.you.seat;
  const inGame = room.lifecycle === "ingame";
  const pending = new Map(room.pendingTakeovers.map((p) => [p.seat, p.name]));
  // Lobby seats are always shown by compass position: the room is not yet drawn from anyone's side.
  const compass = (seat: Seat) => translate(language, `seat.${seat}` as MessageKey);

  function describe(seat: RoomSeat): string {
    if (seat.kind === "human") return seat.name ?? "";
    if (seat.kind === "bot") return seat.botFor ? t("seat.botFor", { name: seat.botFor }) : t("seat.bot");
    return t("lobby.free");
  }

  return (
    <ul aria-label={t("lobby.seats")} className="grid grid-cols-1 gap-2 sm:grid-cols-2" data-testid="seat-picker">
      {DISPLAY_ORDER.map((id) => {
        const seat = room.seats.find((s) => s.seat === id)!;
        const mine = mySeat === id;
        const waitingName = pending.get(id);
        // Anyone may take a free seat in the lobby; in a game only a bot seat that is not held for
        // a replaced player is open, and only to someone without a seat.
        const open =
          !mine &&
          ((!inGame && seat.kind === "free") ||
            (!inGame && seat.kind === "bot") ||
            (inGame && seat.kind === "bot" && seat.origin !== "replaced" && mySeat === null && !waitingName));
        const partnerName = room.seats.find((s) => s.seat === partnerSeat(id))?.name;
        return (
          <li
            key={id}
            data-testid={`lobby-seat-${id}`}
            data-kind={seat.kind}
            className={`panel flex flex-col gap-1 p-3 ${mine ? "!border-accent" : ""}`}
          >
            <div className="flex flex-wrap items-center justify-between gap-1">
              <span className="font-semibold">{compass(id)}</span>
              <span className="text-xs text-muted">
                {t("lobby.partnerOf", { name: partnerName ?? compass(partnerSeat(id)) })}
              </span>
            </div>
            <p className="text-base" data-testid={`lobby-seat-name-${id}`}>
              {describe(seat)}
              {mine && <span className="chip chip-accent ml-2">{t("lobby.you")}</span>}
              {seat.kind === "human" && seat.connection && seat.connection !== "connected" && (
                <span className="chip ml-2 text-warn">{t("conn.reconnecting")}</span>
              )}
            </p>
            {waitingName && <p className="text-xs text-muted">{t("lobby.pendingFor", { name: waitingName })}</p>}
            {open && (
              <button type="button" className="btn" onClick={() => onClaim(id)} data-testid={`take-${id}`}>
                {inGame ? t("lobby.takeNextHand") : t("lobby.take")}
              </button>
            )}
            {mine && !inGame && (
              <button type="button" className="btn" onClick={onRelease} data-testid="stand-up">
                {t("lobby.standUp")}
              </button>
            )}
          </li>
        );
      })}
    </ul>
  );
}

function partnerSeat(seat: Seat): Seat {
  return seat === "north" ? "south" : seat === "south" ? "north" : seat === "east" ? "west" : "east";
}
