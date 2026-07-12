import { describe, it, expect } from 'vitest';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const pkgDir = path.resolve(fileURLToPath(import.meta.url), '../..');

function packedFiles() {
  const out = execFileSync('npm', ['pack', '--dry-run', '--json'], {
    cwd: pkgDir,
    encoding: 'utf8',
  });
  return JSON.parse(out)[0].files.map((f) => f.path);
}

describe('published tarball', () => {
  const files = packedFiles();

  it('includes the full plugin: agents, commands, and .claude-plugin manifest', () => {
    expect(files.some((f) => f.startsWith('plugin/agents/'))).toBe(true);
    expect(files.some((f) => f.startsWith('plugin/commands/'))).toBe(true);
    expect(files.some((f) => f === 'plugin/.claude-plugin/plugin.json')).toBe(true);
  });

  it('still includes skills and workflows', () => {
    expect(files.some((f) => f.startsWith('plugin/skills/'))).toBe(true);
    expect(files.some((f) => f.startsWith('plugin/workflows/'))).toBe(true);
  });
});
