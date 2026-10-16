import type { SessionMessage } from "../models";

const guid = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
const runRecord = new RegExp(`^Team: ([^\\r\\n]+) \\(${guid}\\); version: [0-9]+; ordered members: ${guid}(?:, ${guid})*$`, "i");
const firstEnvelope = new RegExp(`^## ([^\\r\\n]+) \\(${guid}\\)\\r?\\n`, "i");

export function presentMessage(message: Pick<SessionMessage, "role" | "content" | "agentRunId">) {
  const record = message.role === "system" ? runRecord.exec(message.content) : null;
  if (record) return { kind: "execution" as const, label: record[1], body: "", hasDetails: true };
  const envelope = message.role === "assistant" && message.agentRunId ? firstEnvelope.exec(message.content) : null;
  if (envelope) return {
    kind: "message" as const, label: envelope[1],
    body: message.content.slice(envelope[0].length), hasDetails: true,
  };
  return { kind: "message" as const, label: "", body: message.content, hasDetails: false };
}
