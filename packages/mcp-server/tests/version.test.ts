import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { getVersion } from '../src/version.js';

const pkg = JSON.parse(
  readFileSync(path.resolve(fileURLToPath(import.meta.url), '../../package.json'), 'utf8')
);

describe('mosaic-mcp getVersion()', () => {
  it('returns the package.json version, not a hardcoded string', () => {
    expect(getVersion()).toBe(pkg.version);
    expect(getVersion()).not.toBe('1.0.0');
  });
});
