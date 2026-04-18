import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { readDiscovery } from './discovery.js';
import { BridgeClient } from './bridge-client.js';
import { createMosaicServer } from './server.js';

async function main() {
  // 1. Read discovery file
  const discovery = readDiscovery();
  const client = new BridgeClient(discovery.port, discovery.secret_base64);

  // 2. Verify bridge is alive
  const health = await client.health();
  if (health.status !== 'ok') {
    process.stderr.write(`Bridge health check failed: ${JSON.stringify(health)}\n`);
    process.exit(1);
  }
  process.stderr.write(
    `Connected to Mosaic Bridge — Unity ${discovery.unity_version}, ` +
    `${health.tool_count} tools, port ${discovery.port}\n`
  );

  // 3. Fetch tool list
  const tools = await client.listTools();

  // 4. Create MCP server with all handlers
  const server = createMosaicServer({
    client,
    discovery,
    initialTools: tools,
  });

  // 5. Start serving over stdio
  const transport = new StdioServerTransport();
  await server.connect(transport);
  process.stderr.write('Mosaic MCP server ready\n');
}

main().catch(err => {
  process.stderr.write(`Fatal: ${err.message}\n`);
  process.exit(1);
});
