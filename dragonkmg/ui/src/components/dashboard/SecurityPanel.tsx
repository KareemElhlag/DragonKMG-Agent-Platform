import { useCallback, useEffect, useState } from "react";
import { request } from "../../services/api";
import { useI18n } from "../../i18n";
import { List } from "../ui/List";
type Audit = {
  id: string;
  occurredAt: string;
  action: string;
  outcome: string;
  statusCode: number;
};
type Approval = {
  id: string;
  action: string;
  status: number;
  requestedAt: string;
};
export function SecurityPanel() {
  const { t, locale } = useI18n();
  const [audit, setAudit] = useState<Audit[]>([]);
  const [approvals, setApprovals] = useState<Approval[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const load = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const [a, p] = await Promise.all([
        request<Audit[]>("/api/audit-events?limit=100"),
        request<Approval[]>("/api/approvals"),
      ]);
      setAudit(a);
      setApprovals(p);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => {
    void Promise.resolve().then(load);
  }, [load]);
  return (
    <div className="grid" aria-busy={loading}>
      {loading && <p role="status">{t("Loading workspace…")}</p>}
      {error && (
        <p role="alert">
          {t("The operation could not be completed.")}{" "}
          <button onClick={() => void load()}>{t("Retry")}</button>
        </p>
      )}
      {!loading && !error && (
        <>
          <section className="panel">
            <h2>{t("Audit trail")}</h2>
            <List
              values={audit}
              render={(item) => (
                <>
                  <strong dir="ltr">{item.action}</strong>
                  <span>
                    {new Date(item.occurredAt).toLocaleString(locale)}
                  </span>
                  <span className="badge">{item.statusCode}</span>
                </>
              )}
            />
          </section>
          <section className="panel">
            <h2>{t("Approvals")}</h2>
            <List
              values={approvals}
              render={(item) => (
                <>
                  <strong>{item.action}</strong>
                  <span>
                    {new Date(item.requestedAt).toLocaleString(locale)}
                  </span>
                  <span className="badge">
                    {[t("Pending"), t("Approved"), t("Rejected"), t("Expired")][
                      item.status
                    ] ?? t("Unknown")}
                  </span>
                </>
              )}
            />
          </section>
        </>
      )}
    </div>
  );
}
