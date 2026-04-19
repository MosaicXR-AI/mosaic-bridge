import os from 'node:os';
import path from 'node:path';
import { atomicWriteJson, readJson, homePath } from '../utils.js';
import { buildMcpServerEntry } from './index.js';

/**
 * Cursor reads MCP config from `~/.cursor/mcp.json` (user-global) or a
 * project-level `.cursor/mcp.json`. We write to the user-global location
 * because Cursor's project-level config is relatively new and has no config
 * path difference from Claude's project-local — using user-global keeps
 * behavior consistent with Claude Desktop + Gemini.
 */
export async function configureCursor(ctx) {
  const { projectPath, serverName, force } = ctx;
  const configPath = homePath('.cursor', 'mcp.json');

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

function tildify(p) {
  const home = os.homedir();
  return p.startsWith(home) ? '~' + p.slice(home.length) : p;
}
