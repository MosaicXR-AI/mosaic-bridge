import os from 'node:os';
import { atomicWriteJson, readJson, homePath } from '../utils.js';
import { buildMcpServerEntry } from './index.js';

/**
 * Gemini CLI stores MCP config in:
 *   ~/.gemini/settings.json       (user-global, what we write)
 *   <project>/.gemini/settings.json (project-level override — not used here)
 *
 * Same top-level key as Claude (`mcpServers`). Two important differences:
 *   1. Server names MUST NOT contain underscores — Gemini parses tool FQN as
 *      mcp_<serverName>_<toolName> and underscores in the name break it.
 *      The caller (flow.js#deriveServerName) already hyphenates, so our
 *      serverName is safe here.
 *   2. The inner schema supports extras (cwd, timeout, trust, env, etc.) that
 *      Claude ignores. We include the minimal stdio entry Claude uses, which
 *      Gemini also accepts unchanged.
 */
export async function configureGemini(ctx) {
  const { projectPath, serverName, force } = ctx;
  const configPath = homePath('.gemini', 'settings.json');

  // Defense-in-depth: the name should already be hyphenated by flow.js, but
  // if a custom --server-name was passed with underscores, we sanitize here
  // too so Gemini doesn't silently reject the entry.
  if (serverName.includes('_')) {
    throw new Error(
      `Gemini rejects server names with underscores. Use --server-name with hyphens only (got "${serverName}").`
    );
  }

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
