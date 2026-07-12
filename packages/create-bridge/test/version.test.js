import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { VERSION } from '../src/cli.js';

const pkg = JSON.parse(
  readFileSync(path.resolve(fileURLToPath(import.meta.url), '../../package.json'), 'utf8')
);

describe('create-bridge --version', () => {
  it('reports the version from package.json, not a hardcoded string', () => {
    expect(VERSION).toBe(pkg.version);
  });
});
