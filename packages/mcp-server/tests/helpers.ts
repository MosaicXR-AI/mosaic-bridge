import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { createMosaicServer, type IBridgeClient } from '../src/server.js';
import type {
  BridgeTool,
  BridgeToolResult,
  HealthResponse,
  KbEntry,
  KbReadResult,
} from '../src/types.js';

// ---------------------------------------------------------------------------
// Mock data
// ---------------------------------------------------------------------------

export const MOCK_TOOLS: BridgeTool[] = [
  {
    name: 'mosaic_gameobject_create',
    description: 'Create a new GameObject in the active scene',
    category: 'gameobject',
    isReadOnly: false,
    inputSchema: {
      type: 'object',
      properties: {
        name: { type: 'string', description: 'Name of the new GameObject' },
        parent: { type: 'string', description: 'Parent path (optional)' },
      },
      required: ['name'],
    },
  },
  {
    name: 'mosaic_scene_get_hierarchy',
    description: 'Get the scene hierarchy tree',
    category: 'scene',
    isReadOnly: true,
    inputSchema: {},
  },
  {
    name: 'mosaic_health_test',
    description: 'Test the bridge connection',
    category: 'health',
    isReadOnly: true,
    inputSchema: {},
  },
];

export const MOCK_KB_ENTRIES: KbEntry[] = [
  {
    uri: 'mosaic://knowledge/physics/rigidbody-best-practices',
    name: 'Rigidbody Best Practices',
    category: 'physics',
    description: 'Best practices for configuring Rigidbody components',
  },
];

export const MOCK_HEALTH: HealthResponse = {
  status: 'ok',
  bridge_state: 'connected',
  tool_count: 3,
  version: '1.0.0',
};

export const MOCK_DISCOVERY = {
  port: 19400,
  unity_version: '2022.3.50f1',
  unity_project_path: '/Users/test/UnityProject',
};

// ---------------------------------------------------------------------------
// Mock bridge client
// ---------------------------------------------------------------------------

export type ToolHandler = (
  name: string,
  params: Record<string, unknown>,
  executionMode?: string
) => BridgeToolResult;

/**
 * A mock bridge client for conformance testing.
 * All methods return predictable data without needing Unity.
 */
export class MockBridgeClient implements IBridgeClient {
  tools: BridgeTool[] = [...MOCK_TOOLS];
  kbEntries: KbEntry[] = [...MOCK_KB_ENTRIES];
  health_: HealthResponse = { ...MOCK_HEALTH };
  toolHandler: ToolHandler | null = null;

  async health(): Promise<HealthResponse> {
    return this.health_;
  }

  async listTools(): Promise<BridgeTool[]> {
    return this.tools;
  }

  async listKbEntries(): Promise<{ entries: KbEntry[] }> {
    return { entries: this.kbEntries };
  }

  async readKbEntry(category: string, key: string): Promise<KbReadResult> {
    const uri = `mosaic://knowledge/${category}/${key}`;
    const entry = this.kbEntries.find(e => e.uri === uri);
    if (!entry) {
      throw new Error(`KB entry not found: ${uri}`);
    }
    return {
      uri,
      category,
      key,
      data: { title: entry.name, content: `Mock content for ${entry.name}` },
    };
  }

  async executeToolWithRetry(
    toolName: string,
    parameters: Record<string, unknown>,
    executionMode?: string
  ): Promise<BridgeToolResult> {
    if (this.toolHandler) {
      return this.toolHandler(toolName, parameters, executionMode);
    }

    // Default: return a successful result
    return {
      success: true,
      data: { message: `Executed ${toolName}`, parameters },
      error: null,
      errorCode: null,
      suggestedFix: null,
      schemaVersion: '1.0',
    };
  }
}

// ---------------------------------------------------------------------------
// Test harness — creates a linked Client + Server pair
// ---------------------------------------------------------------------------

export interface TestHarness {
  client: Client;
  mockBridge: MockBridgeClient;
  cleanup: () => Promise<void>;
}

/**
 * Creates an in-memory MCP client/server pair for testing.
 * The server uses a MockBridgeClient so no Unity connection is needed.
 */
export async function createTestHarness(
  overrides?: Partial<{
    tools: BridgeTool[];
    kbEntries: KbEntry[];
    toolHandler: ToolHandler;
  }>
): Promise<TestHarness> {
  const mockBridge = new MockBridgeClient();
  if (overrides?.tools) mockBridge.tools = overrides.tools;
  if (overrides?.kbEntries) mockBridge.kbEntries = overrides.kbEntries;
  if (overrides?.toolHandler) mockBridge.toolHandler = overrides.toolHandler;

  const server = createMosaicServer({
    client: mockBridge,
    discovery: MOCK_DISCOVERY,
    initialTools: mockBridge.tools,
  });

  const [clientTransport, serverTransport] = InMemoryTransport.createLinkedPair();

  const client = new Client(
    { name: 'conformance-test-client', version: '1.0.0' },
    { capabilities: {} }
  );

  await server.connect(serverTransport);
  await client.connect(clientTransport);

  return {
    client,
    mockBridge,
    cleanup: async () => {
      await client.close();
      await server.close();
    },
  };
}
