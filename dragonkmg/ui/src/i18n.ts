import { createContext, useContext } from "react";
import ar from "./locales/ar.json";
import en from "./locales/en.json";
import es from "./locales/es.json";
import de from "./locales/de.json";

export const languages = {
  ar: "العربية",
  en: "English",
  es: "Español",
  de: "Deutsch",
} as const;
export type Locale = keyof typeof languages;
export type TranslationKey = keyof typeof en;
export function systemTextKey(value: string): value is TranslationKey {
  return Object.hasOwn(en, value);
}
export const dictionaries: Record<Locale, Record<TranslationKey, string>> = {
  ar,
  en,
  es,
  de,
};
export function isLocale(value: string | null): value is Locale {
  return value !== null && Object.hasOwn(languages, value);
}
export const Context = createContext<{
  locale: Locale;
  setLocale: (value: Locale) => void;
  t: (key: TranslationKey) => string;
} | null>(null);

export function useI18n() {
  const context = useContext(Context);
  if (!context) throw new Error("I18nProvider is required");
  return context;
}
