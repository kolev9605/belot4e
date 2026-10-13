import { useState } from "react";
import { NameField } from "../components/NameField";
import { useLanguage } from "../i18n/LanguageProvider";
import { navigate, roomPath } from "../routing/router";
import { loadName, loadSession } from "../session/storage";
import { useStore } from "../state/StoreProvider";

/** Start game / Join game, plus a way back into a room that is still running. */
export function MainMenu({ onJoin }: { onJoin: () => void }) {
  const { t } = useLanguage();
  const { state, createRoom } = useStore();
  const [asking, setAsking] = useState(false);
  const [creating, setCreating] = useState(false);
  const saved = loadSession();
  const connected = state.connection === "connected";

  async function start(name: string): Promise<string | null> {
    setCreating(true);
    const id = await createRoom(name);
    setCreating(false);
    if (!id) setAsking(false);
    return id ? null : "create";
  }

  async function onStart() {
    const name = loadName();
    if (!name) {
      setAsking(true);
      return;
    }
    await start(name);
  }

  if (asking) {
    return (
      <section className="panel mx-auto mt-6 w-full max-w-md p-4">
        <h2 className="mb-3 text-lg font-bold">{t("name.title")}</h2>
        <NameField
          initial={t("name.default")}
          submitLabel={t("name.confirm")}
          onSubmit={async (name) => {
            const failure = await start(name);
            return failure === "create" ? null : failure;
          }}
          onCancel={() => setAsking(false)}
        />
      </section>
    );
  }

  return (
    <section className="mx-auto mt-6 flex w-full max-w-md flex-col gap-3" data-testid="main-menu">
      <p className="text-center text-muted">{t("menu.tagline")}</p>
      {saved && (
        <button
          type="button"
          className="btn btn-primary"
          data-testid="return-to-game"
          onClick={() => navigate(roomPath(saved.roomId))}
        >
          {t("menu.return")}
          <span className="ml-2 text-xs font-normal">{t("menu.returnHint")}</span>
        </button>
      )}
      <button
        type="button"
        className={saved ? "btn" : "btn btn-primary"}
        disabled={!connected || creating}
        onClick={() => void onStart()}
        data-testid="start-game"
      >
        {creating ? t("menu.starting") : t("menu.start")}
      </button>
      <button type="button" className="btn" onClick={onJoin} data-testid="join-game">
        {t("menu.join")}
      </button>
    </section>
  );
}
