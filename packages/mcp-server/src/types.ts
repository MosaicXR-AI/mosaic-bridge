export interface DiscoveryData {
  schema_version: string;
  port: number;
  process_id: number;
  started_unix_seconds: number;
  secret_base64: string;
  unity_project_path: string;
  unity_version: string;
  tls_enabled?: boolean;
  signature?: string;
}

export interface BridgeTool {
  name: string;
  description: string;
  category: string;
  isReadOnly: boolean;
  inputSchema: Record<string, unknown>;
}

export interface BridgeToolResult {
  success: boolean;
  data: unknown;
  error: string | null;
  errorCode: string | null;
  suggestedFix: string | null;
  schemaVersion: string;
  // Story 12.5: Pipeline enrichment
  executionMode?: string;
  warnings?: string[];
  knowledgeBaseReferences?: string[];
  screenshots?: ScreenshotData[];
  reviewContext?: string;
}

export interface ScreenshotData {
  angleLabel: string;
  base64Png: string;
  width: number;
  height: number;
}

export interface KbEntry {
  uri: string;
  name: string;
  category: string;
  description: string;
}

export interface KbReadResult {
  uri: string;
  category: string;
  key: string;
  data: unknown;
}

export interface HealthResponse {
  status: string;
  bridge_state: string;
  tool_count: number;
  version: string;
}
