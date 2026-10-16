import { useRef, useState } from "react";
import { ApiError, providerErrorKey, request } from "../services/api";
import type { TranslationKey } from "../i18n";

type FilePreview = { relativePath: string; content: string; bytes: number; sha256: string };
type Context = { sessionId: string; teamId: string; defaultTeamId: string; projectId: string; draft: string; setDraft: (value: string) => void; busy: boolean };
const codes = ["FileAccessDenied", "FileUnavailable", "FileContextTooLarge", "ProjectRequired", "FileEgressApprovalRequired"] as const;
export function fileErrorKey(cause: unknown): TranslationKey {
  if (cause instanceof ApiError) {
    const code = codes.find(value => value === cause.code);
    if (code) return code;
  }
  return providerErrorKey(cause);
}
export function useFileContext(context: Context) {
  const { sessionId, teamId, defaultTeamId, projectId, draft, setDraft, busy } = context;
  const teamDirective = /^\/team\s+/i.test(draft.trimStart()) ? draft.trimStart().split("\n")[0] : "";
  const scope = `${sessionId}:${projectId}:${teamId || defaultTeamId}:${teamDirective}`;
  const [filePath, setFilePath] = useState("");
  const [preview, setPreview] = useState<{ scope: string; path: string; value: FilePreview } | null>(null);
  const [failure, setFailure] = useState<{ scope: string; path: string; key: TranslationKey } | null>(null);
  const [fileBusy, setFileBusy] = useState(false);
  const [approval, setApproval] = useState({ scope: "", draft: "", allowed: false, serverScope: "" });
  const consentGeneration = useRef(0);
  const lock = useRef(false);
  const filePreview = preview?.scope === scope && preview.path === filePath ? preview.value : null;
  const fileError = failure?.scope === scope && failure.path === filePath ? failure.key : "";
  const resetFileConsent = () => { consentGeneration.current++; setApproval({ scope: "", draft: "", allowed: false, serverScope: "" }); };
  const setAllowFileEgress = async (allowed: boolean) => {
    if (!allowed) { resetFileConsent(); return; }
    if (lock.current || busy) return;
    const current = ++consentGeneration.current;
    lock.current = true; setFileBusy(true); setFailure(null);
    try {
      const result = await request<{ scope: string }>(`/api/sessions/${sessionId}/files/approval-context`, { method: "POST", body: JSON.stringify({ input: draft, ...(teamId ? { teamId } : {}) }) });
      if (consentGeneration.current === current) setApproval({ scope, draft, allowed: true, serverScope: result.scope });
    } catch (cause) { setFailure({ scope, path: filePath, key: fileErrorKey(cause) }); }
    finally { lock.current = false; setFileBusy(false); }
  };
  const previewFile = async () => {
    if (lock.current || busy || !sessionId || !filePath.trim()) return;
    lock.current = true; setFileBusy(true); setFailure(null); setPreview(null);
    try {
      const path = filePath.trim().replace(/^"(.*)"$/, "$1");
      const value = await request<FilePreview>(`/api/sessions/${sessionId}/files/preview`, { method: "POST", body: JSON.stringify({ path, input: draft, ...(teamId ? { teamId } : {}) }) });
      setPreview({ scope, path: filePath, value });
    } catch (cause) { setFailure({ scope, path: filePath, key: fileErrorKey(cause) }); }
    finally { lock.current = false; setFileBusy(false); }
  };
  const addFileReference = () => {
    if (!filePreview || busy || fileBusy || filePreview.relativePath.includes('"')) return;
    const directive = `/file "${filePreview.relativePath}"`;
    if (!draft.split("\n").includes(directive)) {
      const lines = draft.trimStart().split("\n");
      if (/^\/team\s+/i.test(lines[0])) setDraft([lines[0], directive, ...lines.slice(1)].join("\n"));
      else setDraft(`${directive}\n${draft}`);
    }
  };
  return { filePath, setFilePath, filePreview, fileBusy, fileError, previewFile, addFileReference,
    allowFileEgress: approval.scope === scope && approval.draft === draft && approval.allowed,
    fileApprovalScope: approval.scope === scope && approval.draft === draft && approval.allowed ? approval.serverScope : null,
    setAllowFileEgress, resetFileConsent,
  };
}
