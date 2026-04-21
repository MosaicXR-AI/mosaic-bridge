import { configureClaudeCode } from './claude-code.js';
import { configureClaudeDesktop } from './claude-desktop.js';
import { configureCursor } from './cursor.js';
import { configureGemini } from './gemini.js';
import { configureCodex } from './codex.js';

/**
 * Registry of MCP clients the installer knows how to configure.
 * Each entry has:
 *   - label:      human-readable name (shown in prompts + spinners)
 *   - configure:  async (ctx) => { summary, configPath, action } — writes the
 *                 client's config and returns a brief outcome summary.
 *
 * `ctx` is:
 *   {
 *     projectPath:  absolute Unity project root
 *     projectName:  directory name of the project (informational)
 *     serverName:   name to use for the MCP server entry (already hyphenated)
 *     force:        boolean — if true, overwrite existing entries; otherwise skip
 *   }
 */
export function getClientRegistry() {
  return {
    'claude-code':    { label: 'Claude Code',    configure: configureClaudeCode    },
    'claude-desktop': { label: 'Claude Desktop', configure: configureClaudeDesktop },
    'cursor':         { label: 'Cursor',         configure: configureCursor        },
    'gemini':         { label: 'Gemini CLI',     configure: configureGemini        },
    'codex':          { label: 'OpenAI Codex',   configure: configureCodex         },
  };
}

/**
 * Builds the `{ command, args }` pair that actually launches the MCP server.
 *
 * On Windows `npx` is a `.cmd` shim, and MCP clients spawn stdio servers
 * without a shell — a bare `command: "npx"` fails to launch and Claude Code
 * surfaces a "Windows requires 'cmd /c' wrapper to execute npx" warning.
 * Wrap through `cmd /c` so the shim resolves correctly.
 */
export function buildCommandAndArgs({ projectPath }) {
  const npxArgs = ['-y', '@mosaicxr-ai/mcp-server', '--project-path', projectPath];
  if (process.platform === 'win32') {
    return { command: 'cmd', args: ['/c', 'npx', ...npxArgs] };
  }
  return { command: 'npx', args: npxArgs };
}

/**
 * Shared server-entry template. All four JSON-based clients accept the same
 * top-level `mcpServers` key with a stdio command definition, so this produces
 * a single entry that we then splice into each client's file.
 *
 * The server command is `npx @mosaicxr-ai/mcp-server` so users don't need
 * a pre-installed global binary — npx downloads on first run and caches it.
 */
export function buildMcpServerEntry({ projectPath }) {
  return {
    type: 'stdio',
    ...buildCommandAndArgs({ projectPath }),
  };
}
