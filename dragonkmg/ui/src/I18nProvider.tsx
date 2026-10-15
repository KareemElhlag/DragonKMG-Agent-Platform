import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Context, dictionaries, isLocale } from "./i18n";
import type { Locale } from "./i18n";
export function I18nProvider({ children }: { children: ReactNode }) {
  const [locale, setLocale] = useState<Locale>(() => {
    const saved = localStorage.getItem("dragonkmg.locale");
    return isLocale(saved) ? saved : "ar";
  });
  useEffect(() => {
    localStorage.setItem("dragonkmg.locale", locale);
    document.documentElement.lang = locale;
    document.documentElement.dir = locale === "ar" ? "rtl" : "ltr";
  }, [locale]);
  return (
    <Context.Provider
      value={{ locale, setLocale, t: (key) => dictionaries[locale][key] }}
    >
      {children}
    </Context.Provider>
  );
}
