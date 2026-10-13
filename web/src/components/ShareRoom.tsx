import { useRef, useState } from "react";
import { useLanguage } from "../i18n/LanguageProvider";
import { roomUrl } from "../routing/router";

/** The room link and code, with a one-tap copy and (where the device offers it) native sharing. */
export function ShareRoom({ roomId }: { roomId: string }) {
  const { t } = useLanguage();
  const [status, setStatus] = useState<"idle" | "copied" | "failed">("idle");
  const linkRef = useRef<HTMLInputElement>(null);
  const link = roomUrl(roomId);
  const canShare = typeof navigator !== "undefined" && typeof navigator.share === "function";

  async function copy() {
    try {
      await navigator.clipboard.writeText(link);
      setStatus("copied");
    } catch {
      // No clipboard permission: leave the link selected so it can be copied by hand.
      linkRef.current?.focus();
      linkRef.current?.select();
      setStatus("failed");
    }
  }

  async function share() {
    try {
      await navigator.share({ title: t("app.title"), url: link });
    } catch {
      // Cancelled or unsupported: the visible link remains.
    }
  }

  return (
    <section aria-labelledby="share-heading" className="panel flex flex-col gap-2 p-3" data-testid="share-room">
      <h2 id="share-heading" className="text-base font-bold">
        {t("share.title")}
      </h2>
      <label className="text-sm text-muted" htmlFor="room-link">
        {t("share.link")}
      </label>
      <input
        id="room-link"
        ref={linkRef}
        readOnly
        value={link}
        onFocus={(e) => e.currentTarget.select()}
        className="min-h-11 rounded-lg border border-line bg-raised px-3 text-sm"
        data-testid="room-link"
      />
      <p className="text-sm text-muted">
        {t("share.code")}:{" "}
        <code data-testid="room-code" className="select-all break-all font-mono text-ink">
          {roomId}
        </code>
      </p>
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" className="btn btn-primary" onClick={copy}>
          {t("share.copy")}
        </button>
        {canShare && (
          <button type="button" className="btn" onClick={share}>
            {t("share.share")}
          </button>
        )}
        <span role="status" aria-live="polite" data-testid="copy-status" className="text-sm">
          {status === "copied" ? t("share.copied") : status === "failed" ? t("share.copyFailed") : ""}
        </span>
      </div>
    </section>
  );
}
