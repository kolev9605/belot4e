import { useId, useState, type FormEvent } from "react";
import { useLanguage } from "../i18n/LanguageProvider";
import { MAX_NAME_LENGTH } from "../session/storage";

interface NameFieldProps {
  initial: string;
  /** Resolves to an error code (NameInvalid / NameTaken) to show inline, or null on success. */
  onSubmit: (name: string) => Promise<string | null> | string | null;
  submitLabel: string;
  onCancel?: () => void;
  autoFocus?: boolean;
  /** An error from an earlier attempt (for example a name the server refused). */
  externalError?: string | null;
}

/** Display-name entry with the same rules the server enforces, shown inline. */
export function NameField({ initial, onSubmit, submitLabel, onCancel, autoFocus = true, externalError = null }: NameFieldProps) {
  const { t } = useLanguage();
  const id = useId();
  const [value, setValue] = useState(initial);
  const [code, setCode] = useState<string | null>(externalError);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    const name = value.trim();
    if (name.length === 0 || name.length > MAX_NAME_LENGTH) {
      setCode("NameInvalid");
      return;
    }
    setBusy(true);
    try {
      const failure = await onSubmit(name);
      setCode(failure);
    } finally {
      setBusy(false);
    }
  }

  const message =
    code === "NameTaken" ? t("name.taken") : code === "NameInvalid" ? t("name.invalid") : null;

  return (
    <form onSubmit={submit} className="flex flex-col gap-2" noValidate>
      <label htmlFor={id} className="text-sm font-semibold">
        {t("name.label")}
      </label>
      <input
        id={id}
        type="text"
        value={value}
        maxLength={MAX_NAME_LENGTH + 20}
        autoFocus={autoFocus}
        autoComplete="nickname"
        aria-invalid={message !== null}
        aria-describedby={message ? `${id}-error` : undefined}
        onChange={(e) => {
          setValue(e.target.value);
          if (code) setCode(null);
        }}
        className="min-h-11 rounded-lg border border-line bg-raised px-3 text-base"
        data-testid="name-input"
      />
      {message && (
        <p id={`${id}-error`} role="alert" data-testid="name-error" className="text-sm text-danger">
          {message}
        </p>
      )}
      <div className="flex flex-wrap gap-2">
        <button type="submit" className="btn btn-primary" disabled={busy}>
          {submitLabel}
        </button>
        {onCancel && (
          <button type="button" className="btn" onClick={onCancel}>
            {t("name.cancel")}
          </button>
        )}
      </div>
    </form>
  );
}
