import type { ReactNode } from "react";
import { useI18n } from "../../i18n";
export function List<T>({
  values,
  render,
}: {
  values: T[];
  render: (value: T) => ReactNode;
}) {
  const { t } = useI18n();
  return (
    <div className="list">
      {values.length ? (
        values.map((value, index) => (
          <div className="row" key={index}>
            {render(value)}
          </div>
        ))
      ) : (
        <p className="empty">{t("Nothing configured yet.")}</p>
      )}
    </div>
  );
}
