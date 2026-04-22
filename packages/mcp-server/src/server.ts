import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  ListResourcesRequestSchema,
  ReadResourceRequestSchema,
  McpError,
  ErrorCode,
} from '@modelcontextprotocol/sdk/types.js';
import type { BridgeTool, BridgeToolResult, HealthResponse, KbEntry, KbReadResult, DiscoveryData } from './types.js';
import { DomainReloadError } from './bridge-client.js';

// Story 3.4: Map bridge error codes to JSON-RPC error codes
const ERROR_CODE_MAP: Record<string, number> = {
  INVALID_PARAM: ErrorCode.InvalidParams,
  TYPE_MISMATCH: ErrorCode.InvalidParams,
  OUT_OF_RANGE: ErrorCode.InvalidParams,
  TOOL_EXECUTION_FAILED: ErrorCode.InternalError,
  INTERNAL_ERROR: ErrorCode.InternalError,
  NOT_FOUND: ErrorCode.InvalidParams,
  NOT_PERMITTED: ErrorCode.InvalidRequest,
  CANCELLED: -32001,
  BRIDGE_BUSY: -32002,
  BRIDGE_BACKPRESSURE: -32002,
  BRIDGE_UNAVAILABLE: -32000,
  DOMAIN_RELOAD: -32000,
};

/**
 * Interface for bridge operations needed by the MCP server.
 * Allows injection of real or mock implementations.
 */
export interface IBridgeClient {
  health(): Promise<HealthResponse>;
  listTools(): Promise<BridgeTool[]>;
  listKbEntries(): Promise<{ entries: KbEntry[] }>;
  readKbEntry(category: string, key: string): Promise<KbReadResult>;
  executeToolWithRetry(toolName: string, parameters: Record<string, unknown>, executionMode?: string): Promise<BridgeToolResult>;
}

export interface CreateServerOptions {
  client: IBridgeClient;
  discovery: Pick<DiscoveryData, 'port' | 'unity_version' | 'unity_project_path'>;
  initialTools: BridgeTool[];
}

/**
 * Creates a configured MCP Server instance with all handlers registered.
 * Extracted from main() to support testing with in-memory transports.
 */
export function createMosaicServer(opts: CreateServerOptions): Server {
  const { client, discovery } = opts;
  let tools: BridgeTool[] = opts.initialTools;

  const server = new Server(
    { name: 'mosaic-bridge', version: '1.0.0' },
    {
      capabilities: {
        tools: {},
        resources: {},
      },
    }
  );

  // Handle tools/list
  // Always re-fetch from Unity. The bridge rebuilds its ToolRegistry on every
  // [InitializeOnLoad] (domain reload after a script recompile). If we kept
  // the startup-time `tools` snapshot, schema additions to existing tools
  // (e.g. a new param field on material/set-property) would be invisible to
  // MCP clients until the MCP server itself restarted. Refreshing on every
  // list request is cheap — the HTTP call to 127.0.0.1 is sub-millisecond in
  // practice.
  server.setRequestHandler(ListToolsRequestSchema, async () => {
    try {
      tools = await client.listTools();
    } catch {
      // Fall back to the cached list if Unity is momentarily unreachable
      // (e.g. mid-domain-reload). Better to return stale than fail the call.
    }
    return {
      tools: tools.map(t => ({
        name: t.name,
        description: t.description,
        inputSchema: (t.inputSchema && Object.keys(t.inputSchema).length > 0)
          ? t.inputSchema
          : { type: 'object' as const, properties: {}, additionalProperties: true },
      })),
    };
  });

  // Handle tools/call
  server.setRequestHandler(CallToolRequestSchema, async (req) => {
    const { name, arguments: args } = req.params;

    // Refresh tool list if tool not found (bridge may have reloaded)
    if (!tools.find(t => t.name === name)) {
      tools = await client.listTools();
    }

    // Story 12.6: Extract execution_mode from args (not a tool parameter)
    const rawArgs = (args ?? {}) as Record<string, unknown>;
    const executionMode = rawArgs.execution_mode as string | undefined;
    const toolParams = { ...rawArgs };
    delete toolParams.execution_mode;

    let result;
    try {
      result = await client.executeToolWithRetry(name, toolParams, executionMode);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      const code = (err instanceof DomainReloadError || message.includes('reconnect'))
        ? -32002
        : (ERROR_CODE_MAP['BRIDGE_UNAVAILABLE'] ?? ErrorCode.InternalError);
      throw new McpError(code, `Bridge connection error: ${message}`);
    }

    if (!result.success) {
      const code = result.errorCode ?? 'INTERNAL_ERROR';
      const jsonRpcCode = ERROR_CODE_MAP[code] ?? ErrorCode.InternalError;

      if (jsonRpcCode === ErrorCode.InvalidParams) {
        throw new McpError(jsonRpcCode, result.error ?? code);
      }

      return {
        content: [{
          type: 'text' as const,
          text: JSON.stringify({
            error: result.error,
            errorCode: result.errorCode,
            suggestedFix: result.suggestedFix ?? null,
          }, null, 2),
        }],
        isError: true,
      };
    }

    // Build response content blocks
    const content: Array<{ type: 'text'; text: string } | { type: 'image'; data: string; mimeType: string }> = [];

    // Primary result
    content.push({
      type: 'text' as const,
      text: JSON.stringify(result.data, null, 2),
    });

    // Story 12.6: Pipeline warnings
    if (result.warnings && result.warnings.length > 0) {
      content.push({
        type: 'text' as const,
        text: `⚠️ Pipeline warnings:\n${result.warnings.map(w => `• ${w}`).join('\n')}`,
      });
    }

    // Story 12.6: Screenshots as image content blocks
    if (result.screenshots && result.screenshots.length > 0) {
      for (const shot of result.screenshots) {
        content.push({
          type: 'image' as const,
          data: shot.base64Png,
          mimeType: 'image/png',
        });
      }
    }

    // Story 12.8: Review context for LLM self-evaluation
    if (result.executionMode === 'reviewed') {
      const reviewPrompt = buildReviewContext(name, toolParams, result);
      content.push({
        type: 'text' as const,
        text: reviewPrompt,
      });
    }

    return { content };
  });

  // Handle resources/list
  server.setRequestHandler(ListResourcesRequestSchema, async () => {
    const resources = [
      {
        uri: 'mosaic://bridge/status',
        name: 'Bridge Status',
        description: 'Current Mosaic Bridge connection status, tool count, and Unity version',
        mimeType: 'application/json',
      },
      {
        uri: 'mosaic://bridge/tools',
        name: 'Tool Registry',
        description: 'Complete list of registered Mosaic Bridge tools with metadata',
        mimeType: 'application/json',
      },
      // Unity project asset directories — use @mention to reference these in prompts
      {
        uri: 'mosaic://unity/assets/prefabs',
        name: 'Unity Prefabs',
        description: 'All prefab assets (.prefab) in the Unity project. Use @Unity Prefabs to browse available prefabs.',
        mimeType: 'application/json',
      },
      {
        uri: 'mosaic://unity/assets/materials',
        name: 'Unity Materials',
        description: 'All material assets (.mat) in the Unity project. Use @Unity Materials to see available materials.',
        mimeType: 'application/json',
      },
      {
        uri: 'mosaic://unity/assets/textures',
        name: 'Unity Textures',
        description: 'All texture assets (png, jpg, tga, exr, hdr) in the Unity project.',
        mimeType: 'application/json',
      },
      {
        uri: 'mosaic://unity/assets/scenes',
        name: 'Unity Scenes',
        description: 'All scene assets (.unity) in the Unity project.',
        mimeType: 'application/json',
      },
      {
        uri: 'mosaic://unity/assets/scripts',
        name: 'Unity Scripts',
        description: 'All C# script assets (.cs) in the Unity project Assets folder.',
        mimeType: 'application/json',
      },
      {
        uri: 'mosaic://unity/assets/shadergraphs',
        name: 'Unity ShaderGraphs',
        description: 'All ShaderGraph assets (.shadergraph) in the Unity project.',
        mimeType: 'application/json',
      },
    ];

    // Story 5.5 + 3.5: Add KB entries as MCP resources
    try {
      const kb = await client.listKbEntries();
      for (const entry of kb.entries) {
        resources.push({
          uri: entry.uri,
          name: `KB: ${entry.name}`,
          description: entry.description || `${entry.category} knowledge base entry`,
          mimeType: 'application/json',
        });
      }
    } catch {
      // KB not available — continue without KB resources
    }

    return { resources };
  });

  // Handle resources/read
  server.setRequestHandler(ReadResourceRequestSchema, async (req) => {
    const { uri } = req.params;

    if (uri === 'mosaic://bridge/status') {
      const h = await client.health();
      return {
        contents: [{
          uri,
          mimeType: 'application/json',
          text: JSON.stringify({
            status: h.status,
            bridgeState: h.bridge_state,
            toolCount: h.tool_count,
            version: h.version,
            unityVersion: discovery.unity_version,
            unityProjectPath: discovery.unity_project_path,
            port: discovery.port,
          }, null, 2),
        }],
      };
    }

    if (uri === 'mosaic://bridge/tools') {
      tools = await client.listTools();
      return {
        contents: [{
          uri,
          mimeType: 'application/json',
          text: JSON.stringify(tools.map(t => ({
            name: t.name,
            description: t.description,
            category: t.category,
            isReadOnly: t.isReadOnly,
          })), null, 2),
        }],
      };
    }

    // Unity project asset lists — each category calls asset/list on the bridge
    const ASSET_FILTERS: Record<string, { filter: string; label: string }> = {
      'mosaic://unity/assets/prefabs':     { filter: 't:Prefab',     label: 'Prefabs' },
      'mosaic://unity/assets/materials':   { filter: 't:Material',   label: 'Materials' },
      'mosaic://unity/assets/textures':    { filter: 't:Texture2D t:Cubemap t:Texture3D', label: 'Textures' },
      'mosaic://unity/assets/scenes':      { filter: 't:Scene',      label: 'Scenes' },
      'mosaic://unity/assets/scripts':     { filter: 't:MonoScript', label: 'Scripts' },
      'mosaic://unity/assets/shadergraphs':{ filter: 'glob:*.shadergraph', label: 'ShaderGraphs' },
    };

    if (uri in ASSET_FILTERS) {
      const { filter, label } = ASSET_FILTERS[uri];
      try {
        const result = await client.executeToolWithRetry('asset/list', { Filter: filter, PageSize: 200 });
        return {
          contents: [{
            uri,
            mimeType: 'application/json',
            text: JSON.stringify({
              category: label,
              filter,
              assets: (result.data as { Assets?: unknown[] })?.Assets ?? [],
              totalCount: (result.data as { TotalCount?: number })?.TotalCount ?? 0,
              hint: `Use these paths directly in tool calls: asset/info, asset/instantiate_prefab, material/assign, etc.`,
            }, null, 2),
          }],
        };
      } catch (err) {
        return {
          contents: [{
            uri,
            mimeType: 'application/json',
            text: JSON.stringify({ error: `Could not list ${label}: ${err instanceof Error ? err.message : String(err)}` }),
          }],
        };
      }
    }

    // Story 5.5 + 3.5: Handle KB resource reads
    if (uri.startsWith('mosaic://knowledge/')) {
      const kbPath = uri.replace('mosaic://knowledge/', '');
      const entry = await client.readKbEntry(kbPath.split('/')[0], kbPath.split('/').slice(1).join('/'));
      return {
        contents: [{
          uri,
          mimeType: 'application/json',
          text: JSON.stringify(entry.data, null, 2),
        }],
      };
    }

    throw new McpError(ErrorCode.InvalidRequest, `Unknown resource: ${uri}`);
  });

  return server;
}

/**
 * Story 12.8: Builds structured review context so the LLM can self-evaluate
 * the tool result against the original intent.
 */
function buildReviewContext(
  toolName: string,
  params: Record<string, unknown>,
  result: BridgeToolResult
): string {
  const lines: string[] = [
    '--- REVIEW CONTEXT (self-evaluation) ---',
    `Tool: ${toolName}`,
    `Parameters: ${JSON.stringify(params, null, 2)}`,
    `Execution Mode: ${result.executionMode ?? 'reviewed'}`,
  ];

  if (result.warnings && result.warnings.length > 0) {
    lines.push(`Validation Warnings: ${result.warnings.join('; ')}`);
  }

  if (result.knowledgeBaseReferences && result.knowledgeBaseReferences.length > 0) {
    lines.push(`KB References: ${result.knowledgeBaseReferences.join(', ')}`);
  }

  if (result.screenshots && result.screenshots.length > 0) {
    lines.push(`Screenshots: ${result.screenshots.length} angles (${result.screenshots.map(s => s.angleLabel).join(', ')})`);
  }

  lines.push('');
  lines.push('Please review the result and screenshots above. Evaluate:');
  lines.push('1. Does the result match the requested parameters?');
  lines.push('2. Are there any visual issues (z-fighting, missing materials, incorrect positioning)?');
  lines.push('3. Were the validation warnings addressed or acceptable?');
  lines.push('4. Any suggested follow-up actions?');
  lines.push('--- END REVIEW CONTEXT ---');

  return lines.join('\n');
}
