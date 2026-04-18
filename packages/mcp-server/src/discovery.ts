import { readFileSync, existsSync } from 'fs';
import { join } from 'path';
import { createHmac, timingSafeEqual } from 'crypto';
import type { DiscoveryData } from './types.js';

export function getDiscoveryFilePath(): string {
  const platform = process.platform;
  const home = process.env.HOME ?? process.env.USERPROFILE ?? '';

  if (platform === 'darwin') {
    return join(home, 'Library', 'Application Support', 'Mosaic', 'Bridge', 'bridge-discovery.json');
  } else if (platform === 'win32') {
    const localAppData = process.env.LOCALAPPDATA ?? join(home, 'AppData', 'Local');
    return join(localAppData, 'Mosaic', 'Bridge', 'bridge-discovery.json');
  } else {
    // Linux
    const xdgRuntime = process.env.XDG_RUNTIME_DIR;
    const base = (xdgRuntime && existsSync(xdgRuntime))
      ? join(xdgRuntime, 'mosaic-bridge')
      : join(home, '.cache', 'mosaic-bridge');
    return join(base, 'bridge-discovery.json');
  }
}

/**
 * Strips the "signature" field from discovery JSON so the remaining content
 * can be used for HMAC verification. Mirrors DiscoveryIntegrity.StripSignatureField in C#.
 */
function stripSignatureField(json: string): string {
  let stripped = json.replace(/\s*"signature"\s*:\s*"[^"]*"\s*,?\s*/g, '');
  // Fix trailing comma before closing brace
  stripped = stripped.replace(/,\s*}/g, '}');
  return stripped;
}

/**
 * Verifies the HMAC-SHA256 signature of a discovery file.
 * Returns true if valid, false if missing or invalid.
 */
function verifyDiscoverySignature(raw: string, data: DiscoveryData): boolean {
  if (!data.signature || !data.secret_base64) {
    return false;
  }

  try {
    const secret = Buffer.from(data.secret_base64, 'base64');
    const payload = stripSignatureField(raw);
    const expected = createHmac('sha256', secret)
      .update(Buffer.from(payload, 'utf8'))
      .digest('hex');

    // Constant-time comparison
    if (expected.length !== data.signature.length) {
      return false;
    }
    const expectedBuf = Buffer.from(expected, 'utf8');
    const actualBuf = Buffer.from(data.signature, 'utf8');
    return timingSafeEqual(expectedBuf, actualBuf);
  } catch {
    return false;
  }
}

export function readDiscovery(): DiscoveryData {
  const path = getDiscoveryFilePath();
  if (!existsSync(path)) {
    throw new Error(
      `Unity bridge discovery file not found at: ${path}\n` +
      `Make sure Unity Editor is open with the Mosaic Bridge plugin installed.`
    );
  }
  const raw = readFileSync(path, 'utf8');
  const data = JSON.parse(raw) as DiscoveryData;
  if (!data.schema_version?.startsWith('1.')) {
    throw new Error(`Unsupported discovery file schema version: ${data.schema_version}`);
  }

  // Story 8.5: Verify discovery file integrity (graceful degradation — warn but proceed)
  if (data.signature) {
    if (!verifyDiscoverySignature(raw, data)) {
      console.warn(
        '[mosaic-mcp] Discovery file signature verification failed. ' +
        'The file may have been tampered with. Proceeding with caution.'
      );
    }
  } else {
    console.warn(
      '[mosaic-mcp] Discovery file has no signature field. ' +
      'Upgrade the Unity bridge to v1.2+ for integrity verification.'
    );
  }

  return data;
}
