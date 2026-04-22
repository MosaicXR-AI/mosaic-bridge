import { Command } from 'commander';
import * as p from '@clack/prompts';
import pc from 'picocolors';
import { runInteractive } from './flow.js';

const VERSION = '1.0.0-beta.4';

export async function run(argv) {
  const program = new Command()
    .name('create-bridge')
    .description('Set up Mosaic Bridge in a Unity project and configure your MCP clients')
    .version(VERSION, '-v, --version', 'Show version')
    .option(
      '-p, --project-path <path>',
      'Path to the Unity project root (skips the interactive prompt)'
    )
    .option(
      '-c, --clients <list>',
      'Comma-separated list of MCP clients to configure: claude-code, claude-desktop, cursor, gemini, codex'
    )
    .option(
      '--skip-unity',
      'Do not modify the Unity project\'s Packages/manifest.json (only write MCP client configs)'
    )
    .option(
      '--skip-clients',
      'Do not write any MCP client configs (only install the Unity package)'
    )
    .option(
      '-y, --yes',
      'Accept all defaults without prompting (non-interactive). Requires --project-path.'
    )
    .option(
      '--server-name <name>',
      'Override the MCP server name in generated configs. Defaults to mosaic-bridge-<project-name>.'
    )
    .option(
      '--force',
      'Overwrite existing MCP client config entries for this server (instead of skipping)'
    )
    .option(
      '--skip-claude',
      'Do not write CLAUDE.md scene-building instructions into the Unity project root'
    )
    .helpOption('-h, --help', 'Show help');

  program.parse(argv);
  const opts = program.opts();

  try {
    await runInteractive({
      projectPath: opts.projectPath,
      clients: parseClientsList(opts.clients),
      skipUnity: !!opts.skipUnity,
      skipClients: !!opts.skipClients,
      nonInteractive: !!opts.yes,
      serverNameOverride: opts.serverName,
      force: !!opts.force,
      skipClaude: !!opts.skipClaude,
    });
  } catch (err) {
    if (err && err.cancelled) {
      // User Ctrl-C'd; runInteractive already printed a cancel message.
      process.exit(0);
    }
    p.log.error(pc.red(err?.message || String(err)));
    process.exit(1);
  }
}

function parseClientsList(csv) {
  if (!csv) return null;
  const valid = new Set(['claude-code', 'claude-desktop', 'cursor', 'gemini', 'codex']);
  const list = csv.split(',').map((s) => s.trim()).filter(Boolean);
  const bad = list.filter((c) => !valid.has(c));
  if (bad.length > 0) {
    throw new Error(
      `Unknown client(s): ${bad.join(', ')}. Valid: ${[...valid].join(', ')}.`
    );
  }
  return list;
}
