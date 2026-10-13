import { useState, type FormEvent } from "react";
import { useLanguage } from "../i18n/LanguageProvider";
import { navigate, parseRoomLink, roomPath } from "../routing/router";

/** A text box that accepts a bare room code or a pasted room link. */
export function JoinGame({ onBack }: { onBack: () => void }) {
  const { t } = useLanguage();
  const [text, setText] = useState("");
  const [invalid, setInvalid] = useState(false);

  function submit(event: FormEvent) {
    event.preventDefault();
    const id = parseRoomLink(text);
    if (!id) {
      setInvalid(true);
      return;
    }
    navigate(roomPath(id));
  }

  return (
    <section className="panel mx-auto mt-6 w-full max-w-md p-4">
      <h2 className="mb-3 text-lg font-bold">{t("join.title")}</h2>
      <form onSubmit={submit} className="flex flex-col gap-2" noValidate>
        <label htmlFor="join-code" className="text-sm font-semibold">
          {t("join.label")}
        </label>
        <input
          id="join-code"
          type="text"
          value={text}
          autoFocus
          placeholder={t("join.placeholder")}
          aria-invalid={invalid}
          aria-describedby={invalid ? "join-error" : undefined}
          onChange={(e) => {
            setText(e.target.value);
            if (invalid) setInvalid(false);
          }}
          className="min-h-11 rounded-lg border border-line bg-raised px-3 text-base"
          data-testid="join-input"
        />
        {invalid && (
          <p id="join-error" role="alert" data-testid="join-error" className="text-sm text-danger">
            {t("join.invalid")}
          </p>
        )}
        <div className="flex flex-wrap gap-2">
          <button type="submit" className="btn btn-primary">
            {t("join.submit")}
          </button>
          <button type="button" className="btn" onClick={onBack}>
            {t("join.back")}
          </button>
        </div>
      </form>
    </section>
  );
}
