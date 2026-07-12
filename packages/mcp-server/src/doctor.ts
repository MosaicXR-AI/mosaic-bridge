import { existsSync, readFileSync, statSync } from 'node:fs';
import { createConnection } from 'node:net';
import {
  resolveDiscoveryFilePath,
  readInstanceRegistry,
  type DiscoveryOptions,
} from './discovery.js';
import { BridgeClient } from './bridge-client.js';
import type { DiscoveryData } from './types.js';

export type CheckStatus = 'pass' | 'warn' | 'fail';

export interface DoctorCheck {
  name: string;
  status: CheckStatus;
  detail: string;
}

export interface DoctorReport {
  checks: DoctorCheck[];
  /** True when no check failed. Warnings do not flip this to false. */
  ok: boolean;
}

/** How far in the future the discovery file's mtime may sit before we warn about clock skew. */
const CLOCK_SKEW_WARN_MS = 5 * 60 * 1000;

/**
 * Runs a sequence of connection diagnostics and returns a structured report.
 * Never throws: every check is fault-isolated into a {name, status, detail} record,
 * so a failure early in the chain still produces a full, readable report.
 */
export async function runDoctor(opts: DiscoveryOptions = {}): Promise<DoctorReport> {
  const checks: DoctorCheck[] = [];

  // 1. Discovery file — resolve, exist, parse, schema, signature.
  let discovery: DiscoveryData | undefined;
  let discoveryPath: string | undefined;
  try {
    discoveryPath = resolveDiscoveryFilePath(opts);
    if (!existsSync(discoveryPath)) {
      checks.push({
        name: 'Discovery file',
        status: 'fail',
        detail: `Not found at ${discoveryPath}. Is the Unity Editor running with Mosaic Bridge installed?`,
      });
    } else {
      const raw = readFileSync(discoveryPath, 'utf8').replace(/^\uFEFF/, '');
      const data = JSON.parse(raw) as DiscoveryData;
      if (!data.schema_version?.startsWith('1.')) {
        checks.push({
          name: 'Discovery file',
          status: 'fail',
          detail: `Unsupported schema_version "${data.schema_version}" at ${discoveryPath}.`,
        });
      } else if (!data.signature) {
        discovery = data;
        checks.push({
          name: 'Discovery file',
          status: 'warn',
          detail: `Found (${discoveryPath}) but unsigned. Upgrade the Unity bridge to v1.2+ for integrity verification.`,
        });
      } else {
        discovery = data;
        checks.push({
          name: 'Discovery file',
          status: 'pass',
          detail: `Found and signed (${discoveryPath}), Unity ${data.unity_version}, port ${data.port}.`,
        });
      }
    }
  } catch (err) {
    checks.push({
      name: 'Discovery file',
      status: 'fail',
      detail: message(err),
    });
  }

  // 2. Live editor — instance registry filtered to running PIDs.
  try {
    const alive = readInstanceRegistry().filter((e) => isProcessAlive(e.pid));
    if (alive.length === 1) {
      checks.push({
        name: 'Live editor',
        status: 'pass',
        detail: `1 running Unity Editor (pid ${alive[0].pid}, port ${alive[0].port}).`,
      });
    } else if (alive.length === 0) {
      checks.push({
        name: 'Live editor',
        status: 'warn',
        detail: 'No running Unity Editor found in the instance registry. If the bridge is running, this is harmless.',
      });
    } else {
      checks.push({
        name: 'Live editor',
        status: 'warn',
        detail: `${alive.length} running editors — pass --project-path <path> so the server targets the right one.`,
      });
    }
  } catch (err) {
    checks.push({ name: 'Live editor', status: 'warn', detail: message(err) });
  }

  // 3. Port reachable — TCP connect to 127.0.0.1:<port>.
  if (discovery?.port) {
    const reachable = await isPortReachable(discovery.port);
    checks.push(
      reachable
        ? { name: 'Port reachable', status: 'pass', detail: `127.0.0.1:${discovery.port} accepted a connection.` }
        : { name: 'Port reachable', status: 'fail', detail: `Nothing listening on 127.0.0.1:${discovery.port}. The bridge may be stopped or on a different port.` }
    );
  } else {
    checks.push({ name: 'Port reachable', status: 'warn', detail: 'Skipped — no port from discovery.' });
  }

  // 4. Health + HMAC — a signed request that exercises the shared secret.
  if (discovery?.port && discovery?.secret_base64) {
    try {
      const client = new BridgeClient(discovery.port, discovery.secret_base64);
      const health = await client.health();
      checks.push(
        health.status === 'ok'
          ? { name: 'Health + HMAC', status: 'pass', detail: `Bridge healthy — ${health.tool_count} tools, state ${health.bridge_state}.` }
          : { name: 'Health + HMAC', status: 'fail', detail: `Bridge health returned status "${health.status}".` }
      );
    } catch (err) {
      const m = message(err);
      const hint = m.includes('401')
        ? ' HMAC secret mismatch — the bridge likely rotated it on a domain reload. Restart the MCP client.'
        : '';
      checks.push({ name: 'Health + HMAC', status: 'fail', detail: m + hint });
    }
  } else {
    checks.push({ name: 'Health + HMAC', status: 'warn', detail: 'Skipped — no port/secret from discovery.' });
  }

  // 5. Clock skew — discovery file written far in the future means the local clock lags,
  //    which can trip the bridge's HMAC timestamp window.
  if (discoveryPath && existsSync(discoveryPath)) {
    try {
      const mtimeMs = statSync(discoveryPath).mtimeMs;
      const skew = mtimeMs - Date.now();
      checks.push(
        skew > CLOCK_SKEW_WARN_MS
          ? { name: 'Clock skew', status: 'warn', detail: `Discovery file mtime is ${Math.round(skew / 1000)}s ahead of this machine — a large clock gap can cause HMAC timestamp rejections.` }
          : { name: 'Clock skew', status: 'pass', detail: 'System clock within tolerance of the bridge host.' }
      );
    } catch (err) {
      checks.push({ name: 'Clock skew', status: 'warn', detail: message(err) });
    }
  }

  return { checks, ok: checks.every((c) => c.status !== 'fail') };
}

/** Renders a report as a human-readable, copy-pasteable block. */
export function formatDoctorReport(r: DoctorReport): string {
  const glyph: Record<CheckStatus, string> = { pass: '✓', warn: '⚠', fail: '✗' };
  const lines = ['Mosaic Bridge — connection doctor', ''];
  for (const c of r.checks) {
    lines.push(`  ${glyph[c.status]} ${c.name}: ${c.detail}`);
  }
  lines.push('');
  lines.push(r.ok ? 'Result: OK — the bridge is reachable.' : 'Result: PROBLEMS FOUND — see the ✗ lines above.');
  lines.push('');
  return lines.join('\n');
}

function isProcessAlive(pid: number): boolean {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

function isPortReachable(port: number, host = '127.0.0.1', timeoutMs = 2000): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = createConnection({ port, host });
    const done = (ok: boolean) => {
      socket.destroy();
      resolve(ok);
    };
    socket.setTimeout(timeoutMs);
    socket.once('connect', () => done(true));
    socket.once('timeout', () => done(false));
    socket.once('error', () => done(false));
  });
}

function message(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}
