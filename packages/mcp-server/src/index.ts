#!/usr/bin/env node
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { readDiscovery, type DiscoveryOptions } from './discovery.js';
import { BridgeClient } from './bridge-client.js';
import { createMosaicServer } from './server.js';

/**
 * Parses CLI arguments. Supports:
 *   --project-path <path>   Path to Unity project root (or its Assets dir)
 *   --project-hash <hash>   16-hex-char project hash (shortcut if already known)
 *   --runtime-dir <dir>     Explicit per-project runtime directory (bypass hashing)
 *   --help                  Show usage and exit
 *   --version               Show version and exit
 */
function parseArgs(argv: string[]): DiscoveryOptions & { help?: boolean; version?: boolean } {
  const opts: DiscoveryOptions & { help?: boolean; version?: boolean } = {};
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    switch (arg) {
      case '--project-path':
        opts.projectPath = argv[++i];
        break;
      case '--project-hash':
        opts.projectHash = argv[++i];
        break;
      case '--runtime-dir':
        opts.runtimeDir = argv[++i];
        break;
      case '--discovery':        // Used by the bridge's McpServerProcess launcher
      case '--discovery-file':   // Alias — direct path to bridge-discovery.json
        opts.discoveryFile = argv[++i];
        break;
      case '--help':
      case '-h':
        opts.help = true;
        break;
      case '--version':
      case '-v':
        opts.version = true;
        break;
    }
  }
  return opts;
}

function printUsage(): void {
  const lines = [
    'mosaic-mcp — MCP server for Mosaic Bridge (Unity Editor)',
    '',
    'Usage:',
    '  mosaic-mcp [options]',
    '',
    'Options:',
    '  --project-path <path>   Path to Unity project root or its Assets directory.',
    '                          Required when multiple Unity Editors are running.',
    '  --project-hash <hash>   16-hex-char project hash (advanced; prefer --project-path).',
    '  --runtime-dir <dir>     Explicit per-project runtime directory (overrides hashing).',
    '  -h, --help              Show this help and exit.',
    '  -v, --version           Show version and exit.',
    '',
    'Multi-project configuration example (claude_desktop_config.json):',
    '  {',
    '    "mcpServers": {',
    '      "mosaic-bridge-MyGame": {',
    '        "command": "mosaic-mcp",',
    '        "args": ["--project-path", "/Users/me/Projects/MyGame"]',
    '      },',
    '      "mosaic-bridge-Factory": {',
    '        "command": "mosaic-mcp",',
    '        "args": ["--project-path", "/Users/me/Projects/Factory"]',
    '      }',
    '    }',
    '  }',
    '',
  ];
  process.stdout.write(lines.join('\n'));
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));

  if (opts.help) {
    printUsage();
    process.exit(0);
  }
  if (opts.version) {
    // Emit a simple version line. The real version is in package.json.
    process.stdout.write('mosaic-mcp (see package.json for version)\n');
    process.exit(0);
  }

  // 1. Read discovery file (with project-aware routing).
  const discovery = readDiscovery({
    projectPath: opts.projectPath,
    projectHash: opts.projectHash,
    runtimeDir: opts.runtimeDir,
    discoveryFile: opts.discoveryFile,
  });

  const client = new BridgeClient(discovery.port, discovery.secret_base64);

  // 2. Verify bridge is alive.
  const health = await client.health();
  if (health.status !== 'ok') {
    process.stderr.write(`Bridge health check failed: ${JSON.stringify(health)}\n`);
    process.exit(1);
  }
  process.stderr.write(
    `Connected to Mosaic Bridge — Unity ${discovery.unity_version}, ` +
    `${health.tool_count} tools, port ${discovery.port}, project ${discovery.unity_project_path}\n`
  );

  // 3. Fetch tool list.
  const tools = await client.listTools();

  // 4. Create MCP server with all handlers.
  const server = createMosaicServer({
    client,
    discovery,
    initialTools: tools,
  });

  // 5. Start serving over stdio.
  const transport = new StdioServerTransport();
  await server.connect(transport);
  process.stderr.write('Mosaic MCP server ready\n');
}

main().catch(err => {
  process.stderr.write(`Fatal: ${err.message}\n`);
  process.exit(1);
});
