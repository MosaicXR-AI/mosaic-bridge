import os from 'node:os';
import path from 'node:path';
import { atomicWriteJson, readJson, homePath } from '../utils.js';
import { buildMcpServerEntry } from './index.js';

/**
 * Claude Desktop config lives in a user-global file. Path varies by OS:
 *   macOS:   ~/Library/Application Support/Claude/claude_desktop_config.json
 *   Windows: %APPDATA%\Claude\claude_desktop_config.json
 *   Linux:   ~/.config/Claude/claude_desktop_config.json
 *
 * We read the existing config (if any), merge our entry into mcpServers
 * without disturbing other entries, and write it back atomically.
 */
export async function configureClaudeDesktop(ctx) {
  const { projectPath, serverName, force } = ctx;
  const configPath = getClaudeDesktopConfigPath();

  const existing = readJson(configPath) || {};
  if (!existing.mcpServers || typeof existing.mcpServers !== 'object') {
    existing.mcpServers = {};
  }

  const already = Object.prototype.hasOwnProperty.call(existing.mcpServers, serverName);
  if (already && !force) {
    return {
      action: 'skipped',
      configPath,
      summary: `skipped (entry for "${serverName}" already present — use --force to overwrite)`,
    };
  }

  existing.mcpServers[serverName] = buildMcpServerEntry({ projectPath });
  atomicWriteJson(configPath, existing);

  return {
    action: already ? 'overwritten' : 'added',
    configPath,
    summary: `${already ? 'overwrote' : 'added'} "${serverName}" in ${tildify(configPath)}`,
  };
}

function getClaudeDesktopConfigPath() {
  if (process.platform === 'darwin') {
    return homePath('Library', 'Application Support', 'Claude', 'claude_desktop_config.json');
  }
  if (process.platform === 'win32') {
    const appData = process.env.APPDATA || homePath('AppData', 'Roaming');
    return path.join(appData, 'Claude', 'claude_desktop_config.json');
  }
  // Linux / other Unix
  return homePath('.config', 'Claude', 'claude_desktop_config.json');
}

function tildify(p) {
  const home = os.homedir();
  return p.startsWith(home) ? '~' + p.slice(home.length) : p;
}
