/**
 * Story 3.6 — Claude Desktop / Cursor Conformance Verification
 *
 * Tests that the Mosaic MCP server conforms to the MCP protocol as expected
 * by Claude Desktop and Cursor IDE. Uses in-memory transports so no Unity
 * connection is required.
 */
import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { McpError } from '@modelcontextprotocol/sdk/types.js';
import {
  createTestHarness,
  MOCK_TOOLS,
  MOCK_KB_ENTRIES,
  MOCK_DISCOVERY,
  type TestHarness,
} from './helpers.js';
import type { BridgeToolResult } from '../src/types.js';

// ==========================================================================
// 1. Protocol Handshake
// ==========================================================================

describe('Protocol Handshake', () => {
  let h: TestHarness;

  beforeAll(async () => { h = await createTestHarness(); });
  afterAll(async () => { await h.cleanup(); });

  it('server info includes name and version', () => {
    // After connect(), the client has received the server's initialize response
    const info = h.client.getServerVersion();
    expect(info).toBeDefined();
    expect(info?.name).toBe('mosaic-bridge');
    expect(info?.version).toBe('1.0.0');
  });

  it('server declares tools capability', () => {
    const caps = h.client.getServerCapabilities();
    expect(caps).toBeDefined();
    expect(caps?.tools).toBeDefined();
  });

  it('server declares resources capability', () => {
    const caps = h.client.getServerCapabilities();
    expect(caps).toBeDefined();
    expect(caps?.resources).toBeDefined();
  });
});

// ==========================================================================
// 2. Tools Protocol
// ==========================================================================

describe('Tools Protocol', () => {
  let h: TestHarness;

  beforeAll(async () => { h = await createTestHarness(); });
  afterAll(async () => { await h.cleanup(); });

  it('tools/list returns all tools', async () => {
    const result = await h.client.listTools();
    expect(result.tools).toHaveLength(MOCK_TOOLS.length);
  });

  it('each tool has name, description, and inputSchema', async () => {
    const { tools } = await h.client.listTools();
    for (const tool of tools) {
      expect(typeof tool.name).toBe('string');
      expect(tool.name.length).toBeGreaterThan(0);
      expect(typeof tool.description).toBe('string');
      expect(tool.description!.length).toBeGreaterThan(0);
      expect(tool.inputSchema).toBeDefined();
      expect(typeof tool.inputSchema).toBe('object');
    }
  });

  it('each tool inputSchema has valid JSON Schema structure', async () => {
    const { tools } = await h.client.listTools();
    for (const tool of tools) {
      const schema = tool.inputSchema as Record<string, unknown>;
      expect(schema.type).toBe('object');
      // Must have properties (may be empty)
      expect(schema).toHaveProperty('properties');
    }
  });

  it('tools/call with valid params returns content array', async () => {
    const result = await h.client.callTool({
      name: 'mosaic_gameobject_create',
      arguments: { name: 'TestCube' },
    });
    expect(result.content).toBeDefined();
    expect(Array.isArray(result.content)).toBe(true);
    expect(result.content.length).toBeGreaterThan(0);
  });

  it('tools/call response content has correct text structure', async () => {
    const result = await h.client.callTool({
      name: 'mosaic_gameobject_create',
      arguments: { name: 'TestCube' },
    });
    const textBlock = result.content[0];
    expect(textBlock).toHaveProperty('type', 'text');
    expect(textBlock).toHaveProperty('text');
    expect(typeof (textBlock as { text: string }).text).toBe('string');
  });

  it('tools/call with unknown tool returns error', async () => {
    // The server refreshes tool list when tool not found; mock still won't have it.
    // With our mock bridge, it will succeed (executeToolWithRetry returns success).
    // We need to configure the mock to fail for unknown tools.
    const harness = await createTestHarness({
      toolHandler: (name) => {
        if (name === 'nonexistent_tool') {
          return {
            success: false,
            data: null,
            error: `Tool not found: ${name}`,
            errorCode: 'NOT_FOUND',
            suggestedFix: null,
            schemaVersion: '1.0',
          };
        }
        return {
          success: true,
          data: {},
          error: null,
          errorCode: null,
          suggestedFix: null,
          schemaVersion: '1.0',
        };
      },
    });

    try {
      await harness.client.callTool({
        name: 'nonexistent_tool',
        arguments: {},
      });
      // NOT_FOUND maps to InvalidParams which throws McpError
      expect.fail('Should have thrown');
    } catch (err) {
      expect(err).toBeInstanceOf(McpError);
      expect((err as McpError).code).toBe(-32602); // InvalidParams
    } finally {
      await harness.cleanup();
    }
  });

  it('tools/call with invalid params returns validation error', async () => {
    const harness = await createTestHarness({
      toolHandler: (_name, params) => {
        if (!params.name) {
          return {
            success: false,
            data: null,
            error: 'Missing required parameter: name',
            errorCode: 'INVALID_PARAM',
            suggestedFix: 'Provide a "name" parameter',
            schemaVersion: '1.0',
          };
        }
        return {
          success: true,
          data: {},
          error: null,
          errorCode: null,
          suggestedFix: null,
          schemaVersion: '1.0',
        };
      },
    });

    try {
      await harness.client.callTool({
        name: 'mosaic_gameobject_create',
        arguments: {},
      });
      expect.fail('Should have thrown');
    } catch (err) {
      expect(err).toBeInstanceOf(McpError);
      expect((err as McpError).code).toBe(-32602); // InvalidParams
    } finally {
      await harness.cleanup();
    }
  });
});

// ==========================================================================
// 3. Resources Protocol
// ==========================================================================

describe('Resources Protocol', () => {
  let h: TestHarness;

  beforeAll(async () => { h = await createTestHarness(); });
  afterAll(async () => { await h.cleanup(); });

  it('resources/list returns built-in resources', async () => {
    const result = await h.client.listResources();
    expect(result.resources.length).toBeGreaterThanOrEqual(2);

    const uris = result.resources.map(r => r.uri);
    expect(uris).toContain('mosaic://bridge/status');
    expect(uris).toContain('mosaic://bridge/tools');
  });

  it('resources/list includes KB entries', async () => {
    const result = await h.client.listResources();
    const kbResources = result.resources.filter(r =>
      r.uri.startsWith('mosaic://knowledge/')
    );
    expect(kbResources).toHaveLength(MOCK_KB_ENTRIES.length);
  });

  it('each resource has uri, name, and description', async () => {
    const { resources } = await h.client.listResources();
    for (const res of resources) {
      expect(typeof res.uri).toBe('string');
      expect(res.uri.length).toBeGreaterThan(0);
      expect(typeof res.name).toBe('string');
      expect(res.name.length).toBeGreaterThan(0);
      expect(typeof res.description).toBe('string');
    }
  });

  it('resources/read returns content for bridge/status', async () => {
    const result = await h.client.readResource({ uri: 'mosaic://bridge/status' });
    expect(result.contents).toHaveLength(1);

    const content = result.contents[0];
    expect(content.uri).toBe('mosaic://bridge/status');
    expect(content.mimeType).toBe('application/json');
    expect(typeof content.text).toBe('string');

    const parsed = JSON.parse(content.text!);
    expect(parsed.status).toBe('ok');
    expect(parsed.unityVersion).toBe(MOCK_DISCOVERY.unity_version);
    expect(parsed.port).toBe(MOCK_DISCOVERY.port);
  });

  it('resources/read returns content for bridge/tools', async () => {
    const result = await h.client.readResource({ uri: 'mosaic://bridge/tools' });
    expect(result.contents).toHaveLength(1);

    const content = result.contents[0];
    const parsed = JSON.parse(content.text!);
    expect(Array.isArray(parsed)).toBe(true);
    expect(parsed).toHaveLength(MOCK_TOOLS.length);
    expect(parsed[0]).toHaveProperty('name');
    expect(parsed[0]).toHaveProperty('category');
  });

  it('resources/read returns content for KB entry', async () => {
    const result = await h.client.readResource({
      uri: 'mosaic://knowledge/physics/rigidbody-best-practices',
    });
    expect(result.contents).toHaveLength(1);

    const content = result.contents[0];
    expect(content.uri).toBe('mosaic://knowledge/physics/rigidbody-best-practices');
    const parsed = JSON.parse(content.text!);
    expect(parsed).toHaveProperty('title');
    expect(parsed).toHaveProperty('content');
  });

  it('resources/read with invalid URI returns error', async () => {
    try {
      await h.client.readResource({ uri: 'mosaic://nonexistent/thing' });
      expect.fail('Should have thrown');
    } catch (err) {
      expect(err).toBeInstanceOf(McpError);
      expect((err as McpError).code).toBe(-32600); // InvalidRequest
    }
  });
});

// ==========================================================================
// 4. Error Handling
// ==========================================================================

describe('Error Handling', () => {
  let h: TestHarness;

  beforeAll(async () => { h = await createTestHarness(); });
  afterAll(async () => { await h.cleanup(); });

  it('bridge connection error returns proper MCP error', async () => {
    const harness = await createTestHarness({
      toolHandler: () => {
        throw new Error('ECONNREFUSED - connect');
      },
    });

    try {
      await harness.client.callTool({
        name: 'mosaic_health_test',
        arguments: {},
      });
      expect.fail('Should have thrown');
    } catch (err) {
      expect(err).toBeInstanceOf(McpError);
      const mcpErr = err as McpError;
      expect(mcpErr.message).toContain('Bridge connection error');
    } finally {
      await harness.cleanup();
    }
  });

  it('tool execution failure returns isError content', async () => {
    const harness = await createTestHarness({
      toolHandler: () => ({
        success: false,
        data: null,
        error: 'Something went wrong in Unity',
        errorCode: 'TOOL_EXECUTION_FAILED',
        suggestedFix: 'Check the Unity console',
        schemaVersion: '1.0',
      }),
    });

    const result = await harness.client.callTool({
      name: 'mosaic_gameobject_create',
      arguments: { name: 'Test' },
    });

    expect(result.isError).toBe(true);
    expect(result.content).toHaveLength(1);
    const block = result.content[0] as { type: string; text: string };
    expect(block.type).toBe('text');

    const parsed = JSON.parse(block.text);
    expect(parsed.error).toBe('Something went wrong in Unity');
    expect(parsed.errorCode).toBe('TOOL_EXECUTION_FAILED');
    expect(parsed.suggestedFix).toBe('Check the Unity console');

    await harness.cleanup();
  });

  it('INVALID_PARAM error throws McpError with -32602', async () => {
    const harness = await createTestHarness({
      toolHandler: () => ({
        success: false,
        data: null,
        error: 'Parameter "scale" must be positive',
        errorCode: 'INVALID_PARAM',
        suggestedFix: null,
        schemaVersion: '1.0',
      }),
    });

    try {
      await harness.client.callTool({
        name: 'mosaic_gameobject_create',
        arguments: { name: 'Test', scale: -1 },
      });
      expect.fail('Should have thrown');
    } catch (err) {
      expect(err).toBeInstanceOf(McpError);
      expect((err as McpError).code).toBe(-32602);
      expect((err as McpError).message).toContain('scale');
    } finally {
      await harness.cleanup();
    }
  });
});

// ==========================================================================
// 5. Content Types
// ==========================================================================

describe('Content Types', () => {
  it('text content has type "text" and text field', async () => {
    const h = await createTestHarness();
    const result = await h.client.callTool({
      name: 'mosaic_health_test',
      arguments: {},
    });

    const block = result.content[0] as Record<string, unknown>;
    expect(block.type).toBe('text');
    expect(typeof block.text).toBe('string');
    expect(block.text).toBeTruthy();

    await h.cleanup();
  });

  it('screenshot results include image content blocks', async () => {
    const h = await createTestHarness({
      toolHandler: () => ({
        success: true,
        data: { message: 'Created object' },
        error: null,
        errorCode: null,
        suggestedFix: null,
        schemaVersion: '1.0',
        screenshots: [
          {
            angleLabel: 'front',
            base64Png: 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
            width: 1,
            height: 1,
          },
        ],
      }),
    });

    const result = await h.client.callTool({
      name: 'mosaic_gameobject_create',
      arguments: { name: 'Cube' },
    });

    expect(result.content.length).toBeGreaterThanOrEqual(2);

    // First block is text (data), second is image
    const imgBlock = result.content.find(
      (c: Record<string, unknown>) => c.type === 'image'
    ) as Record<string, unknown> | undefined;
    expect(imgBlock).toBeDefined();
    expect(imgBlock!.type).toBe('image');
    expect(typeof imgBlock!.data).toBe('string');
    expect(imgBlock!.mimeType).toBe('image/png');

    await h.cleanup();
  });

  it('error responses have isError: true', async () => {
    const h = await createTestHarness({
      toolHandler: () => ({
        success: false,
        data: null,
        error: 'internal failure',
        errorCode: 'INTERNAL_ERROR',
        suggestedFix: null,
        schemaVersion: '1.0',
      }),
    });

    const result = await h.client.callTool({
      name: 'mosaic_health_test',
      arguments: {},
    });

    expect(result.isError).toBe(true);

    await h.cleanup();
  });

  it('reviewed execution mode includes review context', async () => {
    const h = await createTestHarness({
      toolHandler: () => ({
        success: true,
        data: { message: 'Created object' },
        error: null,
        errorCode: null,
        suggestedFix: null,
        schemaVersion: '1.0',
        executionMode: 'reviewed',
        warnings: ['Object may overlap existing geometry'],
      }),
    });

    const result = await h.client.callTool({
      name: 'mosaic_gameobject_create',
      arguments: { name: 'Cube' },
    });

    // Should have data block + warning block + review context block
    expect(result.content.length).toBeGreaterThanOrEqual(3);

    const reviewBlock = result.content.find(
      (c: Record<string, unknown>) =>
        c.type === 'text' &&
        typeof (c as { text: string }).text === 'string' &&
        (c as { text: string }).text.includes('REVIEW CONTEXT')
    );
    expect(reviewBlock).toBeDefined();

    await h.cleanup();
  });

  it('pipeline warnings appear as separate text block', async () => {
    const h = await createTestHarness({
      toolHandler: () => ({
        success: true,
        data: { result: 'ok' },
        error: null,
        errorCode: null,
        suggestedFix: null,
        schemaVersion: '1.0',
        warnings: ['Mesh has no collider', 'Scale is very small'],
      }),
    });

    const result = await h.client.callTool({
      name: 'mosaic_gameobject_create',
      arguments: { name: 'Tiny' },
    });

    const warningBlock = result.content.find(
      (c: Record<string, unknown>) =>
        c.type === 'text' &&
        typeof (c as { text: string }).text === 'string' &&
        (c as { text: string }).text.includes('Pipeline warnings')
    );
    expect(warningBlock).toBeDefined();

    await h.cleanup();
  });
});

// ==========================================================================
// 6. Resource URI Validation
// ==========================================================================

describe('Resource URI Validation', () => {
  let h: TestHarness;

  beforeAll(async () => { h = await createTestHarness(); });
  afterAll(async () => { await h.cleanup(); });

  it('all resource URIs use mosaic:// scheme', async () => {
    const { resources } = await h.client.listResources();
    for (const res of resources) {
      expect(res.uri).toMatch(/^mosaic:\/\//);
    }
  });

  it('all resources have mimeType', async () => {
    const { resources } = await h.client.listResources();
    for (const res of resources) {
      expect(res.mimeType).toBeDefined();
      expect(typeof res.mimeType).toBe('string');
    }
  });
});

// ==========================================================================
// 7. Tool Naming Convention
// ==========================================================================

describe('Tool Naming Convention', () => {
  let h: TestHarness;

  beforeAll(async () => { h = await createTestHarness(); });
  afterAll(async () => { await h.cleanup(); });

  it('all tools use mosaic_ prefix', async () => {
    const { tools } = await h.client.listTools();
    for (const tool of tools) {
      expect(tool.name).toMatch(/^mosaic_/);
    }
  });

  it('tool names use snake_case', async () => {
    const { tools } = await h.client.listTools();
    for (const tool of tools) {
      expect(tool.name).toMatch(/^[a-z][a-z0-9_]*$/);
    }
  });
});
