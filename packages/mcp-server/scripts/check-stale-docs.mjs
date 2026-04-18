#!/usr/bin/env node
/**
 * Story 11.5: Stale Documentation Detection
 *
 * Checks for drift between the actual tool registry and generated docs.
 * - Compares tool count in docs vs actual tools from test helpers
 * - Checks if any tool categories are missing from docs
 * - Warns if docs haven't been regenerated recently (7+ days)
 *
 * Zero dependencies -- uses only Node.js built-ins.
 */

import { readdir, readFile, stat } from 'node:fs/promises';
import { join } from 'node:path';

const ROOT = join(import.meta.dirname, '..');
const DOCS_DIR = join(ROOT, 'docs');
const HELPERS_PATH = join(ROOT, 'tests', 'helpers.ts');
const TOOLS_DIR = join(
  ROOT,
  '..',
  '..',
  'packages',
  'unity-bridge',
  'com.mosaic.bridge',
  'Editor',
  'Tools',
);

const STALE_THRESHOLD_DAYS = 7;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Extract tool names and categories from test helpers file. */
async function getToolsFromHelpers() {
  const content = await readFile(HELPERS_PATH, 'utf-8');
  const tools = [];
  const nameRegex = /name:\s*'([^']+)'/g;
  const catRegex = /category:\s*'([^']+)'/g;

  let m;
  while ((m = nameRegex.exec(content)) !== null) {
    tools.push(m[1]);
  }

  const categories = new Set();
  while ((m = catRegex.exec(content)) !== null) {
    categories.add(m[1]);
  }

  return { tools, categories };
}

/** Count actual C# tool classes (files ending in Tool.cs). */
async function countCSharpTools() {
  let count = 0;
  const categories = new Set();

  let dirs;
  try {
    dirs = await readdir(TOOLS_DIR, { withFileTypes: true });
  } catch {
    return { count: 0, categories };
  }

  for (const dir of dirs) {
    if (!dir.isDirectory()) continue;
    const files = await readdir(join(TOOLS_DIR, dir.name));
    const toolFiles = files.filter(f => f.endsWith('Tool.cs'));
    count += toolFiles.length;
    if (toolFiles.length > 0) {
      categories.add(dir.name.toLowerCase());
    }
  }

  return { count, categories };
}

/** Scan docs directory for generated documentation files. */
async function scanDocs() {
  let docFiles = [];
  let oldestMtime = Date.now();

  try {
    docFiles = await readdir(DOCS_DIR);
  } catch {
    return { exists: false, files: [], toolsMentioned: 0, categoriesMentioned: new Set(), oldestMtime };
  }

  let toolsMentioned = 0;
  const categoriesMentioned = new Set();

  for (const file of docFiles) {
    const filePath = join(DOCS_DIR, file);
    const fileStat = await stat(filePath);
    if (fileStat.mtime.getTime() < oldestMtime) {
      oldestMtime = fileStat.mtime.getTime();
    }

    if (!file.endsWith('.md') && !file.endsWith('.json')) continue;

    const content = await readFile(filePath, 'utf-8');
    // Count mosaic_ tool references
    const toolRefs = content.match(/mosaic_\w+/g);
    if (toolRefs) {
      toolsMentioned += new Set(toolRefs).size;
    }

    // Detect category references
    const catKeywords = ['gameobject', 'component', 'scene', 'asset', 'script', 'selection', 'console', 'undo', 'search', 'material', 'prefab', 'build', 'settings', 'test', 'health'];
    for (const kw of catKeywords) {
      if (content.toLowerCase().includes(kw)) {
        categoriesMentioned.add(kw);
      }
    }
  }

  return { exists: true, files: docFiles, toolsMentioned, categoriesMentioned, oldestMtime };
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  const issues = [];
  let warningCount = 0;

  // Gather data
  const helpers = await getToolsFromHelpers();
  const csharp = await countCSharpTools();
  const docs = await scanDocs();

  console.log(`C# tool classes: ${csharp.count}`);
  console.log(`C# tool categories: ${[...csharp.categories].join(', ')}`);
  console.log(`Test helper tools: ${helpers.tools.length}`);
  console.log(`Test helper categories: ${[...helpers.categories].join(', ')}`);

  if (!docs.exists) {
    issues.push('WARNING: No docs directory found at packages/mcp-server/docs/');
    warningCount++;
  } else {
    console.log(`Doc files: ${docs.files.length}`);
    console.log(`Tools mentioned in docs: ${docs.toolsMentioned}`);

    // Check for missing categories in docs
    for (const cat of csharp.categories) {
      if (!docs.categoriesMentioned.has(cat.toLowerCase())) {
        issues.push(`WARNING: Category "${cat}" has C# tools but is not mentioned in docs`);
        warningCount++;
      }
    }

    // Check staleness
    const daysSinceUpdate = (Date.now() - docs.oldestMtime) / (1000 * 60 * 60 * 24);
    if (daysSinceUpdate > STALE_THRESHOLD_DAYS) {
      issues.push(
        `WARNING: Oldest doc file is ${Math.floor(daysSinceUpdate)} days old (threshold: ${STALE_THRESHOLD_DAYS} days). Consider regenerating docs.`,
      );
      warningCount++;
    }
  }

  // Tool count sanity check: if C# has significantly more tools than helpers
  if (csharp.count > helpers.tools.length + 5) {
    issues.push(
      `INFO: C# has ${csharp.count} tool classes but test helpers only define ${helpers.tools.length} mock tools. Consider expanding test coverage.`,
    );
  }

  // Report
  console.log('');
  if (issues.length === 0) {
    console.log('No stale documentation issues detected.');
    process.exit(0);
  } else {
    for (const issue of issues) {
      console.log(`  ${issue}`);
    }
    console.log('');
    // Warnings don't fail CI -- only errors would
    if (warningCount > 0) {
      console.log(`${warningCount} warning(s) found. Review documentation freshness.`);
    }
    // Exit 0 since these are warnings, not hard failures
    process.exit(0);
  }
}

main().catch(err => {
  console.error('Stale docs check failed:', err.message);
  process.exit(1);
});
