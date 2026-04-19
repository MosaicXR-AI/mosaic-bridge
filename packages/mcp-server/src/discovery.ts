import { readFileSync, existsSync, statSync } from 'fs';
import { join, resolve as resolvePath } from 'path';
import { createHash, createHmac, timingSafeEqual } from 'crypto';
import type { DiscoveryData } from './types.js';

/**
 * Resolved options driving where the MCP server looks for the bridge-discovery.json file.
 * Supports three modes (in order of precedence):
 *   1. --runtime-dir       — explicit directory, used verbatim
 *   2. --project-hash      — 16-hex-char hash, combined with shared base
 *   3. --project-path      — path to Unity project root, hashed to derive project hash
 *   4. (none)              — auto-detect via instance-registry.json; error if ambiguous
 */
export interface DiscoveryOptions {
  projectPath?: string;
  projectHash?: string;
  runtimeDir?: string;
  /** Direct path to bridge-discovery.json, bypassing all path resolution. */
  discoveryFile?: string;
}

/**
 * Returns the shared base directory (parent of all per-project runtime dirs).
 * This is where the cross-project instance-registry.json lives.
 */
export function getSharedBasePath(): string {
  const platform = process.platform;
  const home = process.env.HOME ?? process.env.USERPROFILE ?? '';

  if (platform === 'darwin') {
    return join(home, 'Library', 'Application Support', 'Mosaic', 'Bridge');
  }
  if (platform === 'win32') {
    const localAppData = process.env.LOCALAPPDATA ?? join(home, 'AppData', 'Local');
    return join(localAppData, 'Mosaic', 'Bridge');
  }
  // Linux
  const xdgRuntime = process.env.XDG_RUNTIME_DIR;
  return (xdgRuntime && existsSync(xdgRuntime))
    ? join(xdgRuntime, 'mosaic-bridge')
    : join(home, '.cache', 'mosaic-bridge');
}

/**
 * Computes the 16-hex-char SHA-256 hash of Unity's Application.dataPath (which is
 * the project root + '/Assets'). Must match the C# implementation in
 * RuntimeDirectoryResolver.GetProjectHash() exactly.
 */
export function hashProjectAssetsPath(assetsPath: string): string {
  return createHash('sha256').update(assetsPath, 'utf8').digest('hex').substring(0, 16).toLowerCase();
}

/**
 * Derives a project hash from a user-supplied project path. If the path does not
 * already end with '/Assets', appends it before hashing. This handles both
 * `--project-path /path/to/project` and `--project-path /path/to/project/Assets`.
 */
export function deriveProjectHash(projectPath: string): string {
  const normalized = resolvePath(projectPath);
  const assetsSuffix = process.platform === 'win32' ? '\\Assets' : '/Assets';
  const assetsPath = normalized.endsWith(assetsSuffix) || normalized.endsWith('/Assets')
    ? normalized
    : join(normalized, 'Assets');
  return hashProjectAssetsPath(assetsPath);
}

/**
 * One entry in the shared instance-registry.json file.
 */
export interface InstanceRegistryEntry {
  pid: number;
  port: number;
  projectHash: string;
  projectPath: string;
  startedUnixSeconds?: number;
  lastHeartbeatUnixSeconds?: number;
}

/**
 * Reads the shared instance-registry.json. Returns an empty array if the file
 * does not exist or cannot be parsed.
 */
export function readInstanceRegistry(): InstanceRegistryEntry[] {
  const path = join(getSharedBasePath(), 'instance-registry.json');
  if (!existsSync(path)) return [];

  try {
    // Strip UTF-8 BOM — Windows/macOS .NET File.WriteAllText() writes a BOM by default,
    // and Node's JSON.parse does NOT auto-strip it. Without this, a registry file written
    // by the Unity bridge fails to parse here and the auto-detect path sees 0 editors.
    const raw = readFileSync(path, 'utf8').replace(/^\uFEFF/, '');
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed)) return parsed as InstanceRegistryEntry[];
    if (parsed && Array.isArray(parsed.instances)) return parsed.instances as InstanceRegistryEntry[];
    if (parsed && Array.isArray(parsed.entries)) return parsed.entries as InstanceRegistryEntry[];
    return [];
  } catch {
    return [];
  }
}

/**
 * Returns true if a process with the given PID is currently running. Used to
 * filter the registry to live instances only.
 */
function isProcessAlive(pid: number): boolean {
  try {
    // Signal 0 does not send a signal but checks whether the process exists.
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

/**
 * Resolves the discovery-file path from the supplied options. Throws a
 * descriptive error if ambiguity cannot be resolved.
 */
export function resolveDiscoveryFilePath(opts: DiscoveryOptions = {}): string {
  // Mode 0 (highest precedence): explicit file path.
  if (opts.discoveryFile) {
    return opts.discoveryFile;
  }

  // Mode 1: explicit runtime dir — use verbatim.
  if (opts.runtimeDir) {
    return join(opts.runtimeDir, 'bridge-discovery.json');
  }

  // Mode 2: explicit project hash.
  if (opts.projectHash) {
    return join(getSharedBasePath(), opts.projectHash, 'bridge-discovery.json');
  }

  // Mode 3: derive hash from project path.
  if (opts.projectPath) {
    const hash = deriveProjectHash(opts.projectPath);
    return join(getSharedBasePath(), hash, 'bridge-discovery.json');
  }

  // Mode 4 (auto-detect): inspect the instance registry. If exactly one live
  // instance, use it. If zero or many, error out with actionable guidance.
  const entries = readInstanceRegistry().filter(e => isProcessAlive(e.pid));

  if (entries.length === 0) {
    // Fall back to the legacy shared-base location for a clearer error message
    // (users running pre-multi-project bridges will see their old file path).
    const legacyPath = join(getSharedBasePath(), 'bridge-discovery.json');
    if (existsSync(legacyPath)) return legacyPath;

    throw new Error(
      `No live Unity Editor with Mosaic Bridge detected.\n\n` +
      `Open a Unity project with the Mosaic Bridge package installed, wait for the bridge to start,\n` +
      `and try again. If the bridge is already running, check that it binds to 127.0.0.1 and writes\n` +
      `bridge-discovery.json to:\n  ${getSharedBasePath()}/<projectHash>/`
    );
  }

  if (entries.length === 1) {
    const entry = entries[0];
    return join(getSharedBasePath(), entry.projectHash, 'bridge-discovery.json');
  }

  // Multiple live editors — ambiguous. Build a detailed message.
  const lines = entries.map((e, i) =>
    `  ${i + 1}. projectPath=${e.projectPath}  port=${e.port}  pid=${e.pid}  hash=${e.projectHash}`
  );
  throw new Error(
    `Multiple Unity Editors are running with Mosaic Bridge. The MCP server cannot auto-select.\n\n` +
    `Live projects:\n${lines.join('\n')}\n\n` +
    `Resolve by passing one of these flags when launching mosaic-mcp:\n` +
    `  --project-path <path>   Path to the Unity project root (recommended)\n` +
    `  --project-hash <hash>   The 16-hex-char project hash shown above\n` +
    `  --runtime-dir <dir>     Absolute path to the per-project runtime directory\n\n` +
    `Or configure one MCP server per Unity project in your client's config:\n` +
    `  "mcpServers": {\n` +
    `    "mosaic-bridge-projectA": { "command": "mosaic-mcp", "args": ["--project-path", "/path/to/A"] },\n` +
    `    "mosaic-bridge-projectB": { "command": "mosaic-mcp", "args": ["--project-path", "/path/to/B"] }\n` +
    `  }`
  );
}

/**
 * Platform-aware legacy path for pre-multi-project bridges. Kept only for the
 * "no live editors" error message — production code paths use resolveDiscoveryFilePath.
 */
export function getDiscoveryFilePath(opts?: DiscoveryOptions): string {
  return resolveDiscoveryFilePath(opts ?? {});
}

/**
 * Strips the "signature" field from discovery JSON so the remaining content
 * can be used for HMAC verification. Mirrors DiscoveryIntegrity.StripSignatureField in C#.
 */
function stripSignatureField(json: string): string {
  let stripped = json.replace(/\s*"signature"\s*:\s*"[^"]*"\s*,?\s*/g, '');
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

export function readDiscovery(opts?: DiscoveryOptions): DiscoveryData {
  const path = resolveDiscoveryFilePath(opts ?? {});
  if (!existsSync(path)) {
    throw new Error(
      `Unity bridge discovery file not found at: ${path}\n\n` +
      `Make sure the target Unity Editor is running with the Mosaic Bridge plugin installed,\n` +
      `and that its project's dataPath matches the --project-path you supplied (if any).`
    );
  }
  // Strip UTF-8 BOM (see readInstanceRegistry for the rationale).
  const raw = readFileSync(path, 'utf8').replace(/^\uFEFF/, '');
  const data = JSON.parse(raw) as DiscoveryData;
  if (!data.schema_version?.startsWith('1.')) {
    throw new Error(`Unsupported discovery file schema version: ${data.schema_version}`);
  }

  // Integrity check (graceful degradation — warn but proceed)
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
