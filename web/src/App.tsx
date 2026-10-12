import { useState } from "react";
import { SignalRHubClient, type HubClient } from "./connection/hubClient";
import { LanguageProvider, useLanguage } from "./i18n/LanguageProvider";
import { StoreProvider, useStore } from "./state/StoreProvider";
import { ConnectionBanner } from "./components/Chrome";
import { Table } from "./components/Table";

function Header() {
  const { language, setLanguage, t } = useLanguage();
  return (
    <header className="flex items-center justify-between gap-2">
      <h1 className="text-2xl font-bold">{t("app.title")}</h1>
      <button
        type="button"
        lang={language === "en" ? "bg" : "en"}
        aria-label={t("lang.switchLabel")}
        onClick={() => setLanguage(language === "en" ? "bg" : "en")}
        className="min-h-11 rounded border-2 border-slate-900 bg-white px-3 py-2 focus-visible:outline-4 focus-visible:outline-offset-2 focus-visible:outline-blue-700"
      >
        {t("lang.switch")}
      </button>
    </header>
  );
}

function Shell() {
  const { state } = useStore();
  return (
    <main className="mx-auto min-h-screen max-w-3xl bg-slate-50 px-4 py-4 text-slate-900">
      <Header />
      <div className="mt-3">
        <ConnectionBanner status={state.connection} showStale={state.game !== null} />
      </div>
      <Table />
    </main>
  );
}

export function App({ client }: { client?: HubClient }) {
  const [hub] = useState<HubClient>(() => client ?? new SignalRHubClient());
  return (
    <LanguageProvider>
      <StoreProvider client={hub}>
        <Shell />
      </StoreProvider>
    </LanguageProvider>
  );
}
