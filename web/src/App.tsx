import { useEffect, useState, type ReactNode } from "react";
import { LazyMotion, domMax } from "motion/react";
import { SignalRHubClient, type HubClient } from "./connection/hubClient";
import { LanguageProvider, useLanguage } from "./i18n/LanguageProvider";
import { StoreProvider, useStore } from "./state/StoreProvider";
import { ConnectionBanner, NoticeBar } from "./components/Chrome";
import { NameField } from "./components/NameField";
import { PauseOverlay, ReconnectingOverlay, RecapNotice, RoomEventBanner, SupersededOverlay } from "./components/RoomOverlays";
import { Table } from "./components/Table";
import { PacingProvider } from "./presentation/PacingProvider";
import { PresenterProvider } from "./presentation/PresenterProvider";
import { navigate, useRoute } from "./routing/router";
import { JoinGame } from "./screens/JoinGame";
import { Lobby } from "./screens/Lobby";
import { MainMenu } from "./screens/MainMenu";
import { RoomUnavailable } from "./screens/RoomUnavailable";
import { loadName } from "./session/storage";
import type { PacingConfig } from "./theme/motion";

function Header({ status }: { status: ReactNode }) {
  const { language, setLanguage, t } = useLanguage();
  const { state, leaveRoom } = useStore();
  const inGame = state.room?.lifecycle === "ingame" || state.room?.lifecycle === "finished";
  return (
    <header className="flex items-center justify-between gap-2">
      <h1 className="text-xl font-bold tracking-tight">{t("app.title")}</h1>
      <div className="min-w-0 flex-1 text-center">{status}</div>
      {inGame && state.room?.you.seat && (
        <button type="button" className="btn !min-h-9 !py-1" onClick={() => void leaveRoom()} data-testid="leave-game">
          {t("lobby.leave")}
        </button>
      )}
      <button
        type="button"
        lang={language === "en" ? "bg" : "en"}
        aria-label={t("lang.switchLabel")}
        onClick={() => setLanguage(language === "en" ? "bg" : "en")}
        className="btn !min-h-9 !py-1"
      >
        {t("lang.switch")}
      </button>
    </header>
  );
}

function NameGate() {
  const { t } = useLanguage();
  const { state, joinWithName } = useStore();
  return (
    <section className="panel mx-auto mt-6 w-full max-w-md p-4" data-testid="name-gate">
      <h2 className="mb-3 text-lg font-bold">{t("name.title")}</h2>
      <NameField
        initial={loadName() ?? t("name.default")}
        submitLabel={t("name.confirm")}
        externalError={state.needsName?.code ?? null}
        onSubmit={async (name) => {
          const ok = await joinWithName(name);
          return ok ? null : "NameTaken";
        }}
      />
    </section>
  );
}

function RoomScreen() {
  const { t } = useLanguage();
  const { state, takeBack, skipWait, dismissRecap, leaveRoom } = useStore();
  const room = state.room;

  if (state.roomError) {
    return <RoomUnavailable kind={state.roomError} onMenu={() => navigate("/")} />;
  }
  if (state.needsName) return <NameGate />;
  if (!room) {
    return (
      <section className="panel mx-auto mt-6 w-full max-w-md p-4" data-testid="room-loading">
        <p>{t("room.loading")}</p>
      </section>
    );
  }

  const playing = room.lifecycle !== "lobby" && room.you.seat !== null;
  const dropped = state.connection === "reconnecting" || state.connection === "unavailable";
  const paused = room.lifecycle === "ingame" && room.missing.length > 0;

  return (
    <>
      <RoomEventBanner room={room} />
      {state.awayRecap !== null && state.awayRecap > 0 && (
        <RecapNotice moves={state.awayRecap} onDismiss={dismissRecap} />
      )}
      {playing ? <Table /> : <Lobby />}
      {state.superseded ? (
        <SupersededOverlay onTakeBack={() => void takeBack()} />
      ) : dropped ? (
        <ReconnectingOverlay onMenu={() => void leaveRoomToMenu(leaveRoom)} />
      ) : (
        paused && playing && (
          <PauseOverlay room={room} clockOffsetMs={state.clockOffsetMs} onSkip={() => void skipWait()} />
        )
      )}
    </>
  );
}

async function leaveRoomToMenu(leaveRoom: () => Promise<void>) {
  // Going back to the menu from a dead connection keeps the saved room, so "Return to your game"
  // can still bring the person back once the connection recovers.
  navigate("/");
  void leaveRoom;
}

function Shell() {
  const { state, enterRoom, dismissNotice } = useStore();
  const route = useRoute();
  const [joining, setJoining] = useState(false);

  useEffect(() => {
    enterRoom(route.kind === "room" ? route.roomId : null);
  }, [route, enterRoom]);

  let body: ReactNode;
  if (route.kind === "room") {
    body = <RoomScreen />;
  } else if ((state.game || state.session === "lost") && !state.room) {
    // A single table with no room (the legacy start flow), or one that was just lost.
    body = <Table />;
  } else if (joining) {
    body = <JoinGame onBack={() => setJoining(false)} />;
  } else {
    body = <MainMenu onJoin={() => setJoining(true)} />;
  }

  return (
    <main className="mx-auto flex h-dvh max-w-4xl flex-col gap-2 overflow-y-auto bg-page px-3 py-2 text-ink">
      <Header status={<ConnectionBanner status={state.connection} showStale={state.game !== null} />} />
      {route.kind !== "room" && !state.game && <NoticeBar notice={state.notice} onDismiss={dismissNotice} />}
      {body}
    </main>
  );
}

export function App({ client, pacing }: { client?: HubClient; pacing?: PacingConfig }) {
  const [hub] = useState<HubClient>(() => client ?? new SignalRHubClient());
  return (
    <LanguageProvider>
      <PacingProvider value={pacing}>
        <StoreProvider client={hub}>
          <LazyMotion features={domMax} strict>
            <PresenterProvider>
              <Shell />
            </PresenterProvider>
          </LazyMotion>
        </StoreProvider>
      </PacingProvider>
    </LanguageProvider>
  );
}
