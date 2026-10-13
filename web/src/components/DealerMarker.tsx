import { useLanguage } from "../i18n/LanguageProvider";

/** Marks the seat that deals this hand. Text and shape, never colour alone. */
export function DealerMarker() {
  const { t } = useLanguage();
  return (
    <span className="chip chip-accent" data-testid="dealer-marker">
      <svg aria-hidden="true" viewBox="0 0 16 16" width="14" height="14" fill="currentColor">
        <rect x="1" y="4" width="8" height="11" rx="1.5" opacity="0.55" />
        <rect x="5" y="1" width="8" height="11" rx="1.5" />
      </svg>
      {t("label.dealer")}
    </span>
  );
}
