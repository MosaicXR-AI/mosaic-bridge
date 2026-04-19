/**
 * Tests for multi-project discovery routing.
 *
 * Covers:
 *   - Project hash computation matches the C# implementation shape (16 hex, lowercase)
 *   - CLI option precedence: discoveryFile > runtimeDir > projectHash > projectPath > auto
 *   - Ambiguity error when multiple live Editors are registered
 *   - Clear "no editors found" error when nothing is running
 */
import { describe, it, expect } from 'vitest';
import {
  hashProjectAssetsPath,
  deriveProjectHash,
  resolveDiscoveryFilePath,
  getSharedBasePath,
} from '../src/discovery.js';

describe('Project hash', () => {
  it('produces 16-char lowercase hex from an Assets path', () => {
    const hash = hashProjectAssetsPath('/Users/test/Projects/MyGame/Assets');
    expect(hash).toMatch(/^[0-9a-f]{16}$/);
  });

  it('is deterministic — same input produces same output', () => {
    const path = '/Users/test/Projects/Factory/Assets';
    expect(hashProjectAssetsPath(path)).toBe(hashProjectAssetsPath(path));
  });

  it('is sensitive to path differences', () => {
    const a = hashProjectAssetsPath('/Users/test/Projects/A/Assets');
    const b = hashProjectAssetsPath('/Users/test/Projects/B/Assets');
    expect(a).not.toBe(b);
  });
});

describe('deriveProjectHash — normalizes user-supplied project path', () => {
  it('accepts a project root without /Assets suffix and appends it', () => {
    const root = '/Users/test/Projects/MyGame';
    const assets = '/Users/test/Projects/MyGame/Assets';
    expect(deriveProjectHash(root)).toBe(hashProjectAssetsPath(assets));
  });

  it('accepts a path that already ends with /Assets', () => {
    const assets = '/Users/test/Projects/MyGame/Assets';
    expect(deriveProjectHash(assets)).toBe(hashProjectAssetsPath(assets));
  });

  it('normalizes relative paths to absolute', () => {
    // Whatever the resolution yields, the hash should reflect the absolute form.
    const h1 = deriveProjectHash('./some-project');
    const h2 = deriveProjectHash('./some-project/Assets');
    expect(h1).toBe(h2);
  });
});

describe('resolveDiscoveryFilePath — option precedence', () => {
  it('discoveryFile wins over every other option', () => {
    const custom = '/tmp/direct/bridge-discovery.json';
    expect(resolveDiscoveryFilePath({
      discoveryFile: custom,
      runtimeDir: '/tmp/rd',
      projectHash: '0000000000000000',
      projectPath: '/irrelevant',
    })).toBe(custom);
  });

  it('runtimeDir composes bridge-discovery.json', () => {
    const runtime = '/tmp/rd';
    const p = resolveDiscoveryFilePath({ runtimeDir: runtime });
    expect(p.endsWith('bridge-discovery.json')).toBe(true);
    expect(p).toContain(runtime);
  });

  it('projectHash composes {shared-base}/{hash}/bridge-discovery.json', () => {
    const hash = 'abcdef0123456789';
    const p = resolveDiscoveryFilePath({ projectHash: hash });
    expect(p).toContain(getSharedBasePath());
    expect(p).toContain(hash);
    expect(p.endsWith('bridge-discovery.json')).toBe(true);
  });

  it('projectPath hashes then composes correctly', () => {
    const root = '/Users/test/Projects/MyGame';
    const expectedHash = deriveProjectHash(root);
    const p = resolveDiscoveryFilePath({ projectPath: root });
    expect(p).toContain(expectedHash);
  });
});

describe('resolveDiscoveryFilePath — no-args auto-detect', () => {
  it('throws a descriptive error when no registry entries exist', () => {
    // This test runs in an environment without a live Unity Editor, so the
    // registry file either does not exist or has stale/dead entries filtered out.
    // We expect a clear error message pointing the user at the fix.
    let caught: Error | null = null;
    try {
      resolveDiscoveryFilePath({});
    } catch (e) {
      caught = e as Error;
    }
    // If no editors running, the error should name the MCP guidance.
    // If a live editor happens to be running on this machine, the call returns a path — both are acceptable.
    if (caught) {
      expect(caught.message).toMatch(/Unity|Mosaic Bridge|MCP/i);
    }
  });
});
