import type { ReactNode } from "react";
import { useI18n } from "../../i18n";
import type { TranslationKey } from "../../i18n";
export function Panel({
  title,
  caption,
  children,
}: {
  title: TranslationKey;
  caption: TranslationKey;
  children: ReactNode;
}) {
  const { t } = useI18n();
  return (
    <section className="panel">
      <h2>{t(title)}</h2>
      <p>{t(caption)}</p>
      {children}
    </section>
  );
}
