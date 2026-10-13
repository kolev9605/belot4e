import { useState } from "react";
import { NameField } from "../components/NameField";
import { NoticeBar } from "../components/Chrome";
import { SeatPicker } from "../components/SeatPicker";
import { ShareRoom } from "../components/ShareRoom";
import { useLanguage } from "../i18n/LanguageProvider";
import { useStore } from "../state/StoreProvider";

/** The room before (or between) games: who sits where, how to invite, and the start button. */
export function Lobby() {
  const { t } = useLanguage();
  const { state, claimSeat, releaseSeat, startRoomGame, leaveRoom, setName, dismissNotice } = useStore();
  const room = state.room!;
  const [renaming, setRenaming] = useState(false);
  const isHost = room.you.isHost;
  const hostName = room.seats.find((s) => s.participantId === room.hostId)?.name ?? t("lobby.theHost");
  const seated = room.seats.some((s) => s.kind === "human");
  const myName = room.seats.find((s) => s.participantId === room.you.participantId)?.name ?? "";
  const inGame = room.lifecycle === "ingame";

  return (
    <section className="mx-auto mt-3 flex w-full max-w-2xl flex-col gap-3" data-testid="lobby">
      <NoticeBar notice={state.notice} onDismiss={dismissNotice} />
      <h2 className="text-lg font-bold">{t("lobby.title")}</h2>
      <ShareRoom roomId={room.roomId} />
      {inGame && <p className="text-sm text-muted">{t("lobby.inProgress")}</p>}
      <SeatPicker room={room} onClaim={(seat) => void claimSeat(seat)} onRelease={() => void releaseSeat()} />

      {room.you.seat === null && !inGame && <p className="text-sm text-muted">{t("lobby.needSeat")}</p>}

      {renaming ? (
        <div className="panel p-3">
          <NameField
            initial={myName}
            submitLabel={t("name.save")}
            onCancel={() => setRenaming(false)}
            onSubmit={async (name) => {
              const ok = await setName(name);
              if (ok) setRenaming(false);
              return ok ? null : "NameTaken";
            }}
          />
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          {!inGame &&
            (isHost ? (
              <button
                type="button"
                className="btn btn-primary"
                disabled={!seated}
                onClick={() => void startRoomGame()}
                data-testid="start-room-game"
              >
                {t("lobby.start")}
              </button>
            ) : (
              <p className="text-sm" data-testid="waiting-host">
                {t("lobby.waitingHost", { name: hostName })}
              </p>
            ))}
          <button type="button" className="btn" onClick={() => setRenaming(true)} data-testid="change-name">
            {t("name.change")}
          </button>
          <button type="button" className="btn" onClick={() => void leaveRoom()} data-testid="leave-room">
            {t("lobby.leave")}
          </button>
        </div>
      )}
      {!inGame && <p className="text-xs text-muted">{t("lobby.botsFill")}</p>}
    </section>
  );
}
