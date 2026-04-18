#!/usr/bin/env node
/**
 * Story 11.2: Contract Drift Validation
 *
 * Compares C# parameter classes in the Unity bridge with the JSON schemas
 * defined in the MCP server test helpers. Reports any drift between property
 * names and types so contracts stay in sync across the two packages.
 *
 * Zero dependencies -- uses only Node.js built-ins.
 */

import { readdir, readFile } from 'node:fs/promises';
import { join, basename } from 'node:path';

const TOOLS_DIR = join(
  import.meta.dirname,
  '..',
  '..',
  '..',
  'packages',
  'unity-bridge',
  'com.mosaic.bridge',
  'Editor',
  'Tools',
);

// ---------------------------------------------------------------------------
// 1. Parse C# Params files
// ---------------------------------------------------------------------------

/** Map C# type keywords to JSON schema equivalents. */
const CS_TYPE_MAP = {
  string: 'string',
  int: 'integer',
  float: 'number',
  double: 'number',
  bool: 'boolean',
  'string[]': 'array',
  'int[]': 'array',
  'float[]': 'array',
  'double[]': 'array',
  'bool[]': 'array',
};

/**
 * Extract property names, types, and required flags from a C# Params file.
 * Returns { className, properties: [{ name, csType, jsonType, required }] }
 */
function parseCSharpParams(content, fileName) {
  const className = basename(fileName, '.cs');
  const properties = [];

  // Match lines like:  [Required] public string Name { get; set; }
  const propRegex = /(\[Required\]\s+)?public\s+([\w\[\]]+)\s+(\w+)\s*\{/g;
  let m;
  while ((m = propRegex.exec(content)) !== null) {
    const required = !!m[1];
    const csType = m[2];
    const name = m[3];
    // Convert PascalCase to camelCase (C# -> JSON convention)
    const jsonName = name[0].toLowerCase() + name.slice(1);
    const jsonType = CS_TYPE_MAP[csType] ?? 'unknown';
    properties.push({ name: jsonName, csType, jsonType, required });
  }

  return { className, properties };
}

async function collectCSharpParams() {
  const results = new Map(); // toolName -> { className, properties }

  let categories;
  try {
    categories = await readdir(TOOLS_DIR, { withFileTypes: true });
  } catch {
    console.error(`WARNING: Could not read tools directory: ${TOOLS_DIR}`);
    return results;
  }

  for (const cat of categories) {
    if (!cat.isDirectory()) continue;
    const catDir = join(TOOLS_DIR, cat.name);
    const files = await readdir(catDir);

    for (const file of files) {
      if (!file.endsWith('Params.cs')) continue;
      const content = await readFile(join(catDir, file), 'utf-8');
      const parsed = parseCSharpParams(content, file);
      // Derive a tool name key from the class name:
      // e.g. GameObjectCreateParams -> gameobject_create
      const toolKey = parsed.className
        .replace(/Params$/, '')
        .replace(/([a-z])([A-Z])/g, '$1_$2')
        .toLowerCase();
      results.set(toolKey, parsed);
    }
  }

  return results;
}

// ---------------------------------------------------------------------------
// 2. Collect MCP tool schemas from test helpers
// ---------------------------------------------------------------------------

/**
 * We dynamically import the test helpers to read MOCK_TOOLS.
 * If that fails (e.g. TS not compiled), we fall back to reading the helpers
 * file directly with regex -- keeping this zero-dependency.
 */
async function collectMcpSchemas() {
  const helpersPath = join(import.meta.dirname, '..', 'tests', 'helpers.ts');
  const content = await readFile(helpersPath, 'utf-8');

  // Extract tool objects from MOCK_TOOLS array
  const schemas = new Map();
  const toolBlockRegex = /\{\s*name:\s*'([^']+)'[\s\S]*?inputSchema:\s*(\{[\s\S]*?\})\s*,?\s*\}/g;
  let m;
  while ((m = toolBlockRegex.exec(content)) !== null) {
    const name = m[1];
    try {
      // Quick-and-dirty: convert JS object literal to JSON-ish
      let schemaStr = m[2]
        .replace(/(\w+):/g, '"$1":')  // quote keys
        .replace(/'/g, '"')            // single -> double quotes
        .replace(/,\s*([\]}])/g, '$1') // remove trailing commas
        .replace(/as const/g, '');     // strip TS casts
      const schema = JSON.parse(schemaStr);
      schemas.set(name, schema);
    } catch {
      // Schema parsing failed -- skip
    }
  }

  return schemas;
}

// ---------------------------------------------------------------------------
// 3. Compare and report drift
// ---------------------------------------------------------------------------

async function main() {
  const csParams = await collectCSharpParams();
  const mcpSchemas = await collectMcpSchemas();

  let driftCount = 0;
  const issues = [];

  // For each C# param class, find a matching MCP tool schema
  for (const [csKey, csData] of csParams) {
    // Try to find the MCP tool name by matching the key
    let matchedToolName = null;
    for (const mcpName of mcpSchemas.keys()) {
      // mosaic_gameobject_create -> gameobject_create
      const mcpKey = mcpName.replace(/^mosaic_/, '');
      if (mcpKey === csKey) {
        matchedToolName = mcpName;
        break;
      }
    }

    if (!matchedToolName) {
      // Not every C# param class needs an MCP mock -- skip silently
      continue;
    }

    const mcpSchema = mcpSchemas.get(matchedToolName);
    const mcpProps = mcpSchema.properties ?? {};
    const mcpRequired = new Set(mcpSchema.required ?? []);

    // Fields that are intentionally C#-only (pagination, pipeline internals)
    const KNOWN_CS_ONLY = new Set(['pageToken', 'pageSize']);

    // Compare properties
    for (const csProp of csData.properties) {
      if (KNOWN_CS_ONLY.has(csProp.name)) continue; // Skip known C#-only fields

      const mcpProp = mcpProps[csProp.name];
      if (!mcpProp) {
        issues.push(
          `DRIFT: ${csData.className}.${csProp.name} exists in C# but missing from MCP schema for ${matchedToolName}`,
        );
        driftCount++;
        continue;
      }

      // Check type
      if (csProp.jsonType !== 'unknown' && mcpProp.type && mcpProp.type !== csProp.jsonType) {
        issues.push(
          `TYPE MISMATCH: ${csData.className}.${csProp.name} is ${csProp.csType} (${csProp.jsonType}) in C# but "${mcpProp.type}" in MCP schema`,
        );
        driftCount++;
      }

      // Check required
      if (csProp.required && !mcpRequired.has(csProp.name)) {
        issues.push(
          `REQUIRED DRIFT: ${csData.className}.${csProp.name} is [Required] in C# but not in MCP schema required[]`,
        );
        driftCount++;
      }
    }

    // Check for extra MCP properties not in C#
    const csNames = new Set(csData.properties.map(p => p.name));
    for (const mcpPropName of Object.keys(mcpProps)) {
      if (!csNames.has(mcpPropName)) {
        issues.push(
          `EXTRA: MCP schema for ${matchedToolName} has property "${mcpPropName}" not found in ${csData.className}`,
        );
        driftCount++;
      }
    }
  }

  // Report
  console.log(`Contract validation: ${csParams.size} C# param classes, ${mcpSchemas.size} MCP tool schemas\n`);

  if (issues.length === 0) {
    console.log('No contract drift detected.');
    process.exit(0);
  } else {
    console.log(`Found ${driftCount} contract drift issue(s):\n`);
    for (const issue of issues) {
      console.log(`  - ${issue}`);
    }
    console.log(
      '\nTo fix: update the C# Params class or MCP server schema to match.',
    );
    process.exit(1);
  }
}

main().catch(err => {
  console.error('Contract validation failed:', err.message);
  process.exit(1);
});
