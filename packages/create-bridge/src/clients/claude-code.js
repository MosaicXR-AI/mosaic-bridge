import path from 'node:path';
import { atomicWriteJson, readJson } from '../utils.js';
import { buildMcpServerEntry } from './index.js';

/**
 * Claude Code reads `.mcp.json` from the directory where the user opens a
 * session (project-local). We place it at the Unity project root so the user
 * opens Claude Code there and gets our MCP server automatically.
 *
 * Skills are written separately in flow.js to .claude/skills/ (Claude Code) and
 * .agents/skills/ (all other LLMs) — this follows the bmad-method convention.
 *
 * Note: the Mosaic Bridge Unity package ALSO writes this file on first bridge
 * startup. This installer writes it proactively so the file is in place even
 * before the user opens Unity for the first time.
 */
export async function configureClaudeCode(ctx) {
  const { projectPath, serverName, force } = ctx;
  const configPath = path.join(projectPath, '.mcp.json');

  const existing = readJson(configPath) || {};
  if (!existing.mcpServers || typeof existing.mcpServers !== 'object') {
    existing.mcpServers = {};
  }

  const already = Object.prototype.hasOwnProperty.call(existing.mcpServers, serverName);
  if (already && !force) {
    return {
      action: 'skipped',
      configPath,
      summary: `skipped (entry for "${serverName}" already present in .mcp.json — use --force to overwrite)`,
    };
  }

  existing.mcpServers[serverName] = buildMcpServerEntry({ projectPath });
  atomicWriteJson(configPath, existing);

  return {
    action: already ? 'overwritten' : 'added',
    configPath,
    summary: `${already ? 'overwrote' : 'added'} "${serverName}" in ${path.relative(projectPath, configPath) || '.mcp.json'}`,
  };
}
