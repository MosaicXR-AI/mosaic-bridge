import { signRequest } from './hmac.js';
import { readDiscovery, getDiscoveryFilePath } from './discovery.js';
import type { BridgeTool, BridgeToolResult, HealthResponse, KbEntry, KbReadResult } from './types.js';

const RETRY_POLL_MS = 1000;
const RETRY_TIMEOUT_MS = 30_000;
const MAX_QUEUE_SIZE = 100;

/** Default timeout for tool execution requests (ms). */
const TOOL_TIMEOUT_MS = 30_000;
/** Default timeout for lightweight health/meta requests (ms). */
const HEALTH_TIMEOUT_MS = 5_000;

export class BridgeClient {
  private baseUrl: string;
  private secretBase64: string;
  private port: number;

  constructor(port: number, secretBase64: string) {
    this.port = port;
    this.secretBase64 = secretBase64;
    this.baseUrl = `http://127.0.0.1:${port}`;
  }

  private async request<T>(method: string, path: string, body?: unknown, timeoutMs?: number): Promise<T> {
    const bodyBuffer = body
      ? Buffer.from(JSON.stringify(body), 'utf8')
      : Buffer.alloc(0);

    const { nonce, timestamp, signature } = signRequest(
      this.secretBase64, method, path, bodyBuffer
    );

    // Story 2.11: AbortController with configurable timeout for cancellation support
    const controller = new AbortController();
    const effectiveTimeout = timeoutMs ?? TOOL_TIMEOUT_MS;
    const timeout = setTimeout(() => controller.abort(), effectiveTimeout);

    let response: Response;
    try {
      response = await fetch(`${this.baseUrl}${path}`, {
        method,
        headers: {
          'Content-Type': 'application/json',
          'X-Mosaic-Nonce': nonce,
          'X-Mosaic-Timestamp': timestamp,
          'X-Mosaic-Signature': signature,
        },
        body: bodyBuffer.length > 0 ? bodyBuffer : undefined,
        signal: controller.signal,
      });
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') {
        throw new Error(`Bridge request timed out after ${effectiveTimeout}ms: ${method} ${path}`);
      }
      throw err;
    } finally {
      clearTimeout(timeout);
    }

    if (!response.ok && response.status !== 404) {
      const text = await response.text();

      // Story 3.3: Detect domain reload and trigger retry
      if (response.status === 503 && text.includes('DOMAIN_RELOAD')) {
        throw new DomainReloadError('Bridge is reloading');
      }

      // Secret changed after domain reload — re-read discovery file
      if (response.status === 401) {
        try {
          const discovery = readDiscovery();
          this.port = discovery.port;
          this.secretBase64 = discovery.secret_base64;
          this.baseUrl = `http://127.0.0.1:${discovery.port}`;
        } catch { /* discovery file may be temporarily absent */ }
        throw new DomainReloadError('HMAC secret changed (domain reload)');
      }

      throw new Error(`Bridge returned ${response.status}: ${text}`);
    }
    return response.json() as Promise<T>;
  }

  async health(): Promise<HealthResponse> {
    return this.request<HealthResponse>('GET', '/health', undefined, HEALTH_TIMEOUT_MS);
  }

  async listTools(): Promise<BridgeTool[]> {
    const result = await this.request<{ tools: BridgeTool[] }>('GET', '/tools', undefined, HEALTH_TIMEOUT_MS);
    return result.tools;
  }

  async listKbEntries(): Promise<{ entries: KbEntry[] }> {
    return this.request<{ entries: KbEntry[] }>('GET', '/kb/list', undefined, HEALTH_TIMEOUT_MS);
  }

  async readKbEntry(category: string, key: string): Promise<KbReadResult> {
    return this.request<KbReadResult>('GET', `/kb/read/${category}/${key}`, undefined, HEALTH_TIMEOUT_MS);
  }

  async executeTool(toolName: string, parameters: Record<string, unknown>, executionMode?: string): Promise<BridgeToolResult> {
    const body: Record<string, unknown> = { tool: toolName, parameters };
    if (executionMode) body.execution_mode = executionMode;
    return this.request<BridgeToolResult>('POST', '/execute', body);
  }

  /**
   * Executes a tool call with automatic retry during domain reloads.
   * Waits up to 30 seconds for the bridge to reconnect, polling every 1 second.
   */
  async executeToolWithRetry(toolName: string, parameters: Record<string, unknown>, executionMode?: string): Promise<BridgeToolResult> {
    try {
      return await this.executeTool(toolName, parameters, executionMode);
    } catch (err) {
      if (err instanceof DomainReloadError || isConnectionError(err)) {
        return this.waitAndRetry(toolName, parameters, executionMode);
      }
      throw err;
    }
  }

  /**
   * Waits for the bridge to reconnect after a domain reload, then retries.
   * Re-reads the discovery file in case port/secret changed.
   */
  private async waitAndRetry(toolName: string, parameters: Record<string, unknown>, executionMode?: string): Promise<BridgeToolResult> {
    const deadline = Date.now() + RETRY_TIMEOUT_MS;
    let lastError: Error | null = null;

    process.stderr.write(`[mosaic-mcp] Bridge unavailable, waiting for reconnect (${RETRY_TIMEOUT_MS / 1000}s timeout)...\n`);

    while (Date.now() < deadline) {
      await sleep(RETRY_POLL_MS);

      // Re-read discovery file — port and secret may change after reload
      try {
        const discovery = readDiscovery();
        this.port = discovery.port;
        this.secretBase64 = discovery.secret_base64;
        this.baseUrl = `http://127.0.0.1:${discovery.port}`;
      } catch {
        // Discovery file may be temporarily absent during reload
        continue;
      }

      // Check if bridge is healthy
      try {
        const h = await this.health();
        if (h.status === 'ok') {
          process.stderr.write(`[mosaic-mcp] Bridge reconnected on port ${this.port}, retrying tool call\n`);
          return await this.executeTool(toolName, parameters, executionMode);
        }
      } catch (err) {
        lastError = err instanceof Error ? err : new Error(String(err));
        // Bridge not ready yet, keep polling
      }
    }

    throw new Error(
      `Bridge did not reconnect within ${RETRY_TIMEOUT_MS / 1000}s after domain reload. ` +
      `Last error: ${lastError?.message ?? 'unknown'}`
    );
  }
}

export class DomainReloadError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'DomainReloadError';
  }
}

function isConnectionError(err: unknown): boolean {
  if (err instanceof Error) {
    const msg = err.message.toLowerCase();
    return msg.includes('econnrefused') ||
           msg.includes('econnreset') ||
           msg.includes('fetch failed') ||
           msg.includes('network') ||
           msg.includes('socket');
  }
  return false;
}

function sleep(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}
