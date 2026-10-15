import { useEffect, useRef, useState } from "react";
import { languages, useI18n } from "../../i18n";
import { Icon } from "../ui/Icon";

export function LanguagePicker() {
  const { t, locale, setLocale } = useI18n();
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const menu = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    menu.current
      ?.querySelector<HTMLButtonElement>('[aria-checked="true"]')
      ?.focus();
    const outside = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("pointerdown", outside);
    return () => document.removeEventListener("pointerdown", outside);
  }, [open]);
  return (
    <div
      className="language-picker"
      ref={root}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node))
          setOpen(false);
      }}
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          event.preventDefault();
          setOpen(false);
          trigger.current?.focus();
        }
      }}
    >
      <button
        className="header-control language-trigger"
        ref={trigger}
        type="button"
        aria-label={t("Change language")}
        title={t("Change language")}
        aria-expanded={open}
        aria-haspopup="menu"
        aria-controls={open ? "language-options" : undefined}
        onClick={() => setOpen((value) => !value)}
        onKeyDown={(event) => {
          if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault();
            setOpen(true);
          }
        }}
      >
        <Icon name="globe" />
        <span lang={locale}>{languages[locale]}</span>
        <Icon name="chevron" className="chevron-icon" />
      </button>
      {open && (
        <div className="language-popover">
          <p className="popover-label">{t("language")}</p>
          <div
            id="language-options"
            role="menu"
            aria-label={t("language")}
            ref={menu}
            onKeyDown={(event) => {
              if (!["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key))
                return;
              event.preventDefault();
              const items = Array.from(
                menu.current?.querySelectorAll<HTMLButtonElement>(
                  '[role="menuitemradio"]',
                ) ?? [],
              );
              const index = items.indexOf(
                document.activeElement as HTMLButtonElement,
              );
              const next =
                event.key === "Home"
                  ? 0
                  : event.key === "End"
                    ? items.length - 1
                    : (index +
                        (event.key === "ArrowDown" ? 1 : -1) +
                        items.length) %
                      items.length;
              items[next]?.focus();
            }}
          >
            {Object.entries(languages).map(([value, name]) => (
              <button
                type="button"
                role="menuitemradio"
                aria-checked={locale === value}
                key={value}
                className="language-option"
                onClick={() => {
                  setLocale(value as keyof typeof languages);
                  setOpen(false);
                  trigger.current?.focus();
                }}
              >
                <span className="locale-code">{value.toUpperCase()}</span>
                <span lang={value} dir="auto">
                  {name}
                </span>
                {locale === value && <Icon name="check" />}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
