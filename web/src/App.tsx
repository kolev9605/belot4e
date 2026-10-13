import { useState } from "react";
import { LazyMotion, domMax } from "motion/react";
import { SignalRHubClient, type HubClient } from "./connection/hubClient";
import { LanguageProvider, useLanguage } from "./i18n/LanguageProvider";
import { StoreProvider, useStore } from "./state/StoreProvider";
import { ConnectionBanner } from "./components/Chrome";
import { Table } from "./components/Table";
import { PacingProvider } from "./presentation/PacingProvider";
import { PresenterProvider } from "./presentation/PresenterProvider";
import type { PacingConfig } from "./theme/motion";

function Header() {
  const { language, setLanguage, t } = useLanguage();
  return (
    <header className="flex items-center justify-between gap-2">
      <h1 className="text-2xl font-bold tracking-tight">{t("app.title")}</h1>
      <button
        type="button"
        lang={language === "en" ? "bg" : "en"}
        aria-label={t("lang.switchLabel")}
        onClick={() => setLanguage(language === "en" ? "bg" : "en")}
        className="btn"
      >
        {t("lang.switch")}
      </button>
    </header>
  );
}

function Shell() {
  const { state } = useStore();
  return (
    <main className="mx-auto min-h-screen max-w-3xl bg-page px-4 py-4 text-ink">
      <Header />
      <div className="mt-3">
        <ConnectionBanner status={state.connection} showStale={state.game !== null} />
      </div>
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
