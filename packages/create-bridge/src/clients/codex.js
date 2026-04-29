import os from 'node:os';
import fs from 'node:fs';
import path from 'node:path';
import { parse as parseToml, stringify as stringifyToml } from 'smol-toml';
import { homePath } from '../utils.js';
import { buildCommandAndArgs } from './index.js';
import { AGENTS_MD_CONTENT } from '../templates.js';

/**
 * OpenAI Codex CLI stores MCP config in TOML (not JSON like the others).
 *
 * Path: ~/.codex/config.toml (user scope — what we write)
 *       <project>/.codex/config.toml (project scope — requires a trust entry
 *       in the user config, too finicky for an installer to handle safely)
 *
 * Schema: [mcp_servers.<name>] (snake_case table), NOT mcpServers (camelCase
 * like Claude/Cursor/Gemini).
 *
 * Example entry:
 *
 *   [mcp_servers.mosaic-bridge-foo]
 *   command = "npx"
 *   args = ["-y", "@mosaicxr-ai/mcp-server", "--project-path", "/path"]
 *
 * Codex accepts hyphens in server names (unlike Gemini's historical behavior)
 * and also supports underscores. We use the same hyphenated name as the other
 * clients to keep all four-plus-Codex entries aligned.
 *
 * Installation check:
 *   npm i -g @openai/codex
 *   brew install --cask codex
 */
export async function configureCodex(ctx) {
  const { projectPath, serverName, force } = ctx;
  const configPath = homePath('.codex', 'config.toml');

  // Read existing TOML (if any). Codex's config lives here alongside other
  // user settings, so we must preserve the entire file and only touch the
  // [mcp_servers.<serverName>] table.
  let root = {};
  if (fs.existsSync(configPath)) {
    const raw = fs.readFileSync(configPath, 'utf8').replace(/^\uFEFF/, '');
    try {
      root = parseToml(raw);
    } catch (err) {
      throw new Error(
        `Codex config at ${tildify(configPath)} is not valid TOML: ${err.message}\n` +
          `Fix or back up that file, then rerun.`
      );
    }
  }

  if (!root.mcp_servers || typeof root.mcp_servers !== 'object') {
    root.mcp_servers = {};
  }

  const already = Object.prototype.hasOwnProperty.call(root.mcp_servers, serverName);
  if (already && !force) {
    return {
      action: 'skipped',
      configPath,
      summary: `skipped (entry for "${serverName}" already present — use --force to overwrite)`,
    };
  }

  root.mcp_servers[serverName] = buildCommandAndArgs({ projectPath });

  // Ensure the parent directory exists (first-time installs won't have ~/.codex/).
  const dir = path.dirname(configPath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }

  const text = stringifyToml(root);
  fs.writeFileSync(configPath, text + '\n', { encoding: 'utf8' });

  // Write AGENTS.md to the Unity project root
  const agentsMdPath = path.join(projectPath, 'AGENTS.md');
  if (!fs.existsSync(agentsMdPath) || force) {
    fs.writeFileSync(agentsMdPath, AGENTS_MD_CONTENT, 'utf8');
  }

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
