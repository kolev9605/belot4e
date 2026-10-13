import { useEffect, useRef } from "react";
import { useLanguage } from "../i18n/LanguageProvider";
import type { MessageKey } from "../i18n";
import type { RoomState } from "../protocol/types";
import { useCountdown } from "../session/useCountdown";

/** Modal-ish panel shell: the table behind stays visible; focus moves into the panel. */
function Overlay({ children, label, testId }: { children: React.ReactNode; label: string; testId: string }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    ref.current?.focus();
  }, []);
  return (
    <div className="fixed inset-0 z-40 flex items-end justify-center bg-black/50 p-3 sm:items-center" data-testid={testId}>
      <div
        ref={ref}
        tabIndex={-1}
        role="alertdialog"
        aria-modal="true"
        aria-label={label}
        className="panel w-full max-w-md p-4 outline-none"
      >
        {children}
      </div>
    </div>
  );
}

/** Shown to everyone while a missing player is being waited for. */
export function PauseOverlay({
  room,
  clockOffsetMs,
  onSkip,
}: {
  room: RoomState;
  clockOffsetMs: number;
  onSkip: () => void;
}) {
  const { t } = useLanguage();
  const iAmMissing = room.missing.some((m) => m.participantId === room.you.participantId);
  const names = room.missing.map((m) => m.name).join(", ");
  const soonest = room.missing[0]?.deadlineUtc ?? null;
  const seconds = useCountdown(soonest, clockOffsetMs);

  return (
    <Overlay label={t("pause.title", { names })} testId="pause-overlay">
      <h2 className="text-lg font-bold">{iAmMissing ? t("pause.you") : t("pause.title", { names })}</h2>
      <p className="mt-1 text-sm text-muted">{t("pause.hint")}</p>
      <ul className="mt-3 flex flex-col gap-1" data-testid="missing-list">
        {room.missing.map((m) => (
          <li key={m.participantId} className="flex items-center justify-between gap-2">
            <span className="font-semibold">{m.name}</span>
            <MissingCountdown deadline={m.deadlineUtc} offset={clockOffsetMs} />
          </li>
        ))}
      </ul>
      <p className="sr-only" role="status" aria-live="polite">
        {t("pause.countdown", { seconds })}
      </p>
      {!iAmMissing && (
        <button type="button" className="btn btn-primary mt-4 w-full" onClick={onSkip} data-testid="skip-wait">
          {t("pause.skip")}
        </button>
      )}
    </Overlay>
  );
}

function MissingCountdown({ deadline, offset }: { deadline: string; offset: number }) {
  const { t } = useLanguage();
  const seconds = useCountdown(deadline, offset);
  return (
    <span className="text-sm tabular-nums" data-testid="countdown">
      {t("pause.countdown", { seconds })}
    </span>
  );
}

/** Shown on a device whose connection dropped; it never blocks forever. */
export function ReconnectingOverlay({ onMenu }: { onMenu: () => void }) {
  const { t } = useLanguage();
  return (
    <Overlay label={t("reconnect.title")} testId="reconnecting-overlay">
      <h2 className="text-lg font-bold">{t("reconnect.title")}</h2>
      <p className="mt-1 text-sm text-muted">{t("reconnect.body")}</p>
      <button type="button" className="btn mt-4" onClick={onMenu}>
        {t("reconnect.menu")}
      </button>
    </Overlay>
  );
}

/** Shown when another tab or window took this person's place. */
export function SupersededOverlay({ onTakeBack }: { onTakeBack: () => void }) {
  const { t } = useLanguage();
  return (
    <Overlay label={t("superseded.title")} testId="superseded-overlay">
      <h2 className="text-lg font-bold">{t("superseded.title")}</h2>
      <p className="mt-1 text-sm text-muted">{t("superseded.body")}</p>
      <button type="button" className="btn btn-primary mt-4" onClick={onTakeBack} data-testid="take-back">
        {t("superseded.take")}
      </button>
    </Overlay>
  );
}

/** Announces room events (replaced, back, host change...) to screen readers and shows the latest. */
export function RoomEventBanner({ room }: { room: RoomState }) {
  const { t } = useLanguage();
  const event = room.lastEvent;
  if (!event) return <div className="sr-only" role="status" aria-live="polite" data-testid="room-event" />;
  const key = `event.${event.kind}` as MessageKey;
  const text = t(key, { name: event.name ?? "", by: event.byName ?? "" });
  return (
    <div role="status" aria-live="polite" data-testid="room-event" className="text-center text-xs text-muted">
      {text}
    </div>
  );
}

/** One-time note for a player a bot played for. */
export function RecapNotice({ moves, onDismiss }: { moves: number; onDismiss: () => void }) {
  const { t } = useLanguage();
  return (
    <div role="status" data-testid="recap" className="panel flex items-center justify-between gap-2 border-accent p-2 text-sm">
      <span>{t("recap.body", { count: moves })}</span>
      <button type="button" className="btn" onClick={onDismiss}>
        {t("recap.dismiss")}
      </button>
    </div>
  );
}
