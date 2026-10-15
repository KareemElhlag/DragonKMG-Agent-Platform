import { useEffect, useState } from "react";
import { Panel } from "../ui/Panel";
import { List } from "../ui/List";
import { useI18n } from "../../i18n";
import { request } from "../../services/api";
import type { Skill } from "../../models";
import type { DashboardController } from "../../hooks/useDashboard";

export function SkillsPanel({ dashboard }: { dashboard: DashboardController }) {
  const { t } = useI18n();
  const { skills, busy, submit } = dashboard;
  const [editing, setEditing] = useState<Skill | null>(null);
  const [source, setSource] = useState("project-file");
  const [files, setFiles] = useState<string[]>([]);
  const [preview, setPreview] = useState("");
  const [error, setError] = useState(false);
  const [previewBusy, setPreviewBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  useEffect(() => {
    let active = true;
    request<string[]>("/api/skills/files")
      .then((values) => {
        if (active) setFiles(values);
      })
      .catch(() => {
        if (active) setError(true);
      });
    return () => {
      active = false;
    };
  }, []);
  const inspect = async (skill: Skill) => {
    setPreviewBusy(true);
    setError(false);
    try {
      setPreview(
        (await request<{ content: string }>(`/api/skills/${skill.id}/content`))
          .content,
      );
    } catch {
      setError(true);
    } finally {
      setPreviewBusy(false);
    }
  };
  return (
    <Panel
      title="Skills library"
      caption="Default skills are versioned, sourced, and never grant tools by themselves."
    >
      {error && (
        <p role="alert">{t("The operation could not be completed.")}</p>
      )}
      <form
        key={editing?.id ?? "new"}
        onSubmit={(event) => {
          setSaved(false);
          void submit(event, "skills").then((success) => {
            if (success) {
              setSaved(true);
              setEditing(null);
            }
          });
        }}
      >
        <input type="hidden" name="id" value={editing?.id ?? ""} />
        <label>
          {t("Stable key")}
          <input name="key" defaultValue={editing?.key ?? ""} required />
        </label>
        <label>
          {t("Skill name")}
          <input name="name" defaultValue={editing?.name ?? ""} required />
        </label>
        <label>
          {t("Purpose")}
          <input
            name="description"
            defaultValue={editing?.description ?? ""}
            required
          />
        </label>
        <label>
          {t("Skill source")}
          <select
            name="sourceType"
            value={source}
            onChange={(event) => {
              setSource(event.target.value);
              setSaved(false);
            }}
          >
            <option value="project-file">{t("Skill file path")}</option>
            <option value="inline">{t("Inline instructions")}</option>
          </select>
        </label>
        {source === "project-file" ? (
          <label>
            {t("Skill file path")}
            <input
              name="sourceRef"
              dir="ltr"
              list="skill-files"
              placeholder="kmg-agent-skill/SKILL.md"
              defaultValue={editing?.sourceRef ?? ""}
              required
            />
            <datalist id="skill-files">
              {files.map((file) => (
                <option key={file} value={file} />
              ))}
            </datalist>
            <small>
              {t(
                "Paths are relative to the project skills folder. Files reload on every run.",
              )}
            </small>
          </label>
        ) : (
          <label>
            {t("Versioned skill instructions")}
            <textarea
              name="instructions"
              rows={9}
              defaultValue={editing?.instructionContent ?? ""}
              required
            />
          </label>
        )}
        <div className="form-actions">
          <button disabled={busy}>
            {editing ? t("Save") : t("Create skill")}
          </button>
          {editing && (
            <button
              type="button"
              className="secondary"
              onClick={() => {
                setEditing(null);
                setSource("project-file");
                setSaved(false);
              }}
            >
              {t("New skill")}
            </button>
          )}
        </div>
        {saved && !busy && !dashboard.error && (
          <p role="status">{t("Saved successfully.")}</p>
        )}
      </form>
      <List
        values={skills}
        render={(skill) => (
          <>
            <strong>{skill.name}</strong>
            <span dir="ltr">
              {skill.sourceType === "project-file"
                ? skill.sourceRef
                : t("Inline instructions")}
            </span>
            <em>
              {skill.enabled ? t("Enabled") : t("Disabled")} · v{skill.version}
            </em>
            <div className="form-actions">
              <button
                type="button"
                className="secondary"
                disabled={busy}
                onClick={() => {
                  setEditing(skill);
                  setSource(
                    skill.sourceType === "project-file"
                      ? "project-file"
                      : "inline",
                  );
                  setSaved(false);
                }}
              >
                {t("Edit skill")}
              </button>
              <button
                type="button"
                className="secondary"
                disabled={previewBusy}
                onClick={() => void inspect(skill)}
              >
                {t("Read full skill")}
              </button>
            </div>
          </>
        )}
      />
      {previewBusy && <p role="status">{t("Loading workspace…")}</p>}
      {preview && (
        <details className="skill-preview" open>
          <summary>{t("Read full skill")}</summary>
          <pre dir="auto">{preview}</pre>
        </details>
      )}
    </Panel>
  );
}
