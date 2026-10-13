import { useState, type ReactNode } from "react";
import { LazyMotion, domMax } from "motion/react";
import { SignalRHubClient, type HubClient } from "./connection/hubClient";
import { LanguageProvider, useLanguage } from "./i18n/LanguageProvider";
import { StoreProvider, useStore } from "./state/StoreProvider";
import { ConnectionBanner } from "./components/Chrome";
import { Table } from "./components/Table";
import { PacingProvider } from "./presentation/PacingProvider";
import { PresenterProvider } from "./presentation/PresenterProvider";
import type { PacingConfig } from "./theme/motion";

function Header({ status }: { status: ReactNode }) {
  const { language, setLanguage, t } = useLanguage();
  return (
    <header className="flex items-center justify-between gap-2">
      <h1 className="text-xl font-bold tracking-tight">{t("app.title")}</h1>
      <div className="min-w-0 flex-1 text-center">{status}</div>
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

function Shell() {
  const { state } = useStore();
  return (
    <main className="mx-auto flex h-dvh max-w-4xl flex-col gap-2 overflow-hidden bg-page px-3 py-2 text-ink">
      <Header status={<ConnectionBanner status={state.connection} showStale={state.game !== null} />} />
      <Table />
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
