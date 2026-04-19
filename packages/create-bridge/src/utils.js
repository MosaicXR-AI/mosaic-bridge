import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';

/**
 * Reads a JSON file and strips a UTF-8 BOM if present. Returns null if the file
 * doesn't exist; throws on parse errors with a clearer message.
 */
export function readJson(filePath) {
  if (!fs.existsSync(filePath)) return null;
  const raw = fs.readFileSync(filePath, 'utf8').replace(/^\uFEFF/, '');
  try {
    return JSON.parse(raw);
  } catch (err) {
    throw new Error(
      `Invalid JSON at ${filePath}: ${err.message}\n` +
        `Fix or remove that file and rerun.`
    );
  }
}

/**
 * Writes JSON to disk atomically: write to {file}.tmp, then rename. Creates
 * parent directories as needed. Uses 2-space indent.
 */
export function atomicWriteJson(filePath, data) {
  const dir = path.dirname(filePath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  const tmp = filePath + '.tmp';
  const text = JSON.stringify(data, null, 2) + '\n';
  fs.writeFileSync(tmp, text, { encoding: 'utf8' });
  // fs.renameSync replaces the target atomically on POSIX and Windows (Node 18+).
  fs.renameSync(tmp, filePath);
}

/**
 * Expands a leading `~` to the user's home directory. Useful for config paths
 * like `~/.cursor/mcp.json`.
 */
export function expandHome(p) {
  if (!p) return p;
  if (p === '~') return os.homedir();
  if (p.startsWith('~/')) return path.join(os.homedir(), p.slice(2));
  return p;
}

/**
 * Platform-aware path composition using home-dir expansion. Accepts forward
 * slashes in the path template and normalizes them.
 */
export function homePath(...parts) {
  return path.join(os.homedir(), ...parts);
}

/**
 * Checks whether a command exists in PATH. Returns the path or null.
 * Uses `which` on Unix, `where` on Windows.
 */
export function which(cmd) {
  try {
    const { execSync } = require('node:child_process');
    const whichBin = process.platform === 'win32' ? 'where' : 'which';
    const out = execSync(`${whichBin} ${cmd}`, {
      stdio: ['ignore', 'pipe', 'ignore'],
      encoding: 'utf8',
    })
      .trim()
      .split('\n')[0];
    return out || null;
  } catch {
    return null;
  }
}
