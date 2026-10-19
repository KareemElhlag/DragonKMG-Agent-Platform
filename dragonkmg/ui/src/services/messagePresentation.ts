import type { SessionMessage } from "../models";

const guid = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
const runRecord = new RegExp(`^Team: ([^\\r\\n]+) \\(${guid}\\); version: [0-9]+; ordered members: ${guid}(?:, ${guid})*$`, "i");
const firstEnvelope = new RegExp(`^## ([^\\r\\n]+?)(?: \\(${guid}\\))?\\r?\\n`, "i");
const localReplyPrefixes = ["[Local instant reply — no model call]", "[رد محلي سريع — بدون استدعاء الموديل]"];

export function presentMessage(message: Pick<SessionMessage, "role" | "content" | "agentRunId">) {
  const record = message.role === "system" ? runRecord.exec(message.content) : null;
  if (record) return { kind: "execution" as const, label: record[1], body: "", hasDetails: true };
  const envelope = message.role === "assistant" && message.agentRunId ? firstEnvelope.exec(message.content) : null;
  const body = envelope ? message.content.slice(envelope[0].length) : message.content;
  const localPrefix = message.role === "assistant" ? localReplyPrefixes.find(prefix => body.startsWith(prefix)) : undefined;
  const local = Boolean(localPrefix);
  return {
    kind: "message" as const,
    label: envelope?.[1] ?? "",
    body: localPrefix ? body.slice(localPrefix.length).trimStart() : body,
    hasDetails: Boolean(envelope || local),
    localReply: Boolean(local),
  };
}
