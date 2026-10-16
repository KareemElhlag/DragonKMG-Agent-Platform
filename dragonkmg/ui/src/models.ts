export type TeamDefinition = { id: string; name: string; description: string; enabled: boolean; agentIds: string[]; version: number };
export type Role = {
  id: string;
  name: string;
  description: string;
  defaultInstructions: string;
  isSystem: boolean;
  version: number;
};
export type Skill = {
  id: string;
  key: string;
  name: string;
  description: string;
  version: number;
  enabled: boolean;
  type: number;
  instructionContent: string;
  requiredToolIdsJson: string;
  trustLevel: string;
  sourceType: string;
  sourceRef: string;
};
export type Connection = {
  id: string;
  name: string;
  kind: number;
  endpoint: string;
  secretReference: string;
  enabled: boolean;
  trustZone: number;
  cloudEgressAllowed: boolean;
  createdAt: string;
};
export type ConnectionModel = { connectionId: string; modelId: string; displayName: string; enabled: boolean; isDefault: boolean; source: string; updatedAt: string };
export type Agent = {
  id: string;
  name: string;
  description: string;
  roleId: string;
  instructions: string;
  enabled: boolean;
  modelId: string;
  connectionId: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  mode: number;
};
export type Assignment = {
  agentId: string;
  skillId: string;
  enabled: boolean;
  priority: number;
  configurationJson: string;
  version: number;
};
export type ToolGrant = {
  agentId: string;
  toolId: string;
  requiresApproval: boolean;
  version: number;
};
export type AgentRun = {
  id: string;
  status: number;
  input: string;
  output?: string;
  errorCode?: string;
  startedAt: string;
  finishedAt?: string;
};
export type CouncilRun = {
  executionRunId: string;
  qualityReviewRunId: string;
  securityReviewRunId: string;
};
export type CouncilWorkflowStep = {
  name: string;
  owner: string;
  checklist: string[];
  expectedEvidence: string;
};
export type TerminalSession = {
  id: string;
  name: string;
  selectedAgentId?: string;
  createdAt: string;
  lastOpenedAt: string;
};
export type SessionMessage = {
  id: string;
  sessionId: string;
  role: string;
  content: string;
  agentRunId?: string;
  createdAt: string;
};
