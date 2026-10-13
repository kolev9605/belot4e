import { useLanguage } from "../i18n/LanguageProvider";
import type { RoomErrorKind } from "../state/reducer";

/** Shown when a room cannot be entered: never a blank screen, always a way onward. */
export function RoomUnavailable({ kind, onMenu }: { kind: RoomErrorKind; onMenu: () => void }) {
  const { t } = useLanguage();
  const message = kind === "ended" ? t("room.ended") : kind === "full" ? t("room.full") : t("room.notFound");
  return (
    <section role="alert" data-testid="room-unavailable" className="panel mx-auto mt-6 w-full max-w-md p-4">
      <p className="mb-3 text-base font-semibold">{message}</p>
      <button type="button" className="btn btn-primary" onClick={onMenu}>
        {t("room.toMenu")}
      </button>
    </section>
  );
}
