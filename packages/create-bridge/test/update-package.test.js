import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { injectBridgePackage, invalidatePackageLock } from '../src/unity.js';

const PKG = 'com.mosaic.bridge';
const GIT_URL =
  'https://github.com/MosaicXR-AI/mosaic-bridge.git?path=/packages/com.mosaic.bridge';

let project;

function makeProject() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'mosaic-upd-'));
  fs.mkdirSync(path.join(dir, 'Assets'));
  fs.mkdirSync(path.join(dir, 'ProjectSettings'));
  fs.mkdirSync(path.join(dir, 'Packages'));
  return dir;
}

const manifest = () =>
  JSON.parse(fs.readFileSync(path.join(project, 'Packages', 'manifest.json'), 'utf8'));
const lockFile = () =>
  JSON.parse(fs.readFileSync(path.join(project, 'Packages', 'packages-lock.json'), 'utf8'));

function writeManifest(deps) {
  fs.writeFileSync(
    path.join(project, 'Packages', 'manifest.json'),
    JSON.stringify({ dependencies: deps }, null, 2)
  );
}

function writeLock(hash) {
  fs.writeFileSync(
    path.join(project, 'Packages', 'packages-lock.json'),
    JSON.stringify(
      { dependencies: { [PKG]: { version: GIT_URL, depth: 0, source: 'git', hash } } },
      null,
      2
    )
  );
}

beforeEach(() => {
  project = makeProject();
});
afterEach(() => {
  fs.rmSync(project, { recursive: true, force: true });
});

describe('injectBridgePackage — install', () => {
  it('adds the entry when absent', () => {
    const r = injectBridgePackage(project, { packageName: PKG, gitUrl: GIT_URL });
    expect(r.added).toBe(true);
    expect(manifest().dependencies[PKG]).toBe(GIT_URL);
  });

  it('leaves an existing entry alone by default', () => {
    writeManifest({ [PKG]: 'file:../local-checkout' });
    const r = injectBridgePackage(project, { packageName: PKG, gitUrl: GIT_URL });
    expect(r.added).toBe(false);
    expect(r.updated).toBe(false);
    expect(manifest().dependencies[PKG]).toBe('file:../local-checkout');
  });
});

describe('injectBridgePackage — update', () => {
  it('rewrites an existing entry when update is set', () => {
    writeManifest({ [PKG]: 'file:../local-checkout' });
    const r = injectBridgePackage(project, {
      packageName: PKG,
      gitUrl: GIT_URL,
      update: true,
    });
    expect(r.updated).toBe(true);
    expect(r.previousUrl).toBe('file:../local-checkout');
    expect(manifest().dependencies[PKG]).toBe(GIT_URL);
  });

  it('reports updated=false when the entry already matches', () => {
    writeManifest({ [PKG]: GIT_URL });
    const r = injectBridgePackage(project, {
      packageName: PKG,
      gitUrl: GIT_URL,
      update: true,
    });
    expect(r.updated).toBe(false);
  });

  it('pins to a ref', () => {
    const r = injectBridgePackage(project, {
      packageName: PKG,
      gitUrl: GIT_URL,
      update: true,
      ref: 'c644c3e',
    });
    expect(r.resolvedUrl).toBe(`${GIT_URL}#c644c3e`);
  });

  it('replaces a previous ref instead of stacking fragments', () => {
    writeManifest({ [PKG]: `${GIT_URL}#oldsha` });
    const r = injectBridgePackage(project, {
      packageName: PKG,
      gitUrl: GIT_URL,
      update: true,
      ref: 'newsha',
    });
    expect(r.resolvedUrl).toBe(`${GIT_URL}#newsha`);
    expect(r.resolvedUrl).not.toContain('oldsha');
  });

  it('preserves unrelated dependencies', () => {
    writeManifest({ [PKG]: 'file:../old', 'com.unity.ugui': '2.5.0' });
    injectBridgePackage(project, { packageName: PKG, gitUrl: GIT_URL, update: true });
    expect(manifest().dependencies['com.unity.ugui']).toBe('2.5.0');
  });
});

describe('invalidatePackageLock', () => {
  it('removes the pin and reports the previous hash', () => {
    writeLock('ab6a815c7d59aaaabbbbccccddddeeeeffff0000');
    const r = invalidatePackageLock(project, PKG);
    expect(r.removed).toBe(true);
    expect(r.previousHash).toBe('ab6a815c7d59aaaabbbbccccddddeeeeffff0000');
    expect(lockFile().dependencies[PKG]).toBeUndefined();
  });

  it('leaves other locked packages intact', () => {
    fs.writeFileSync(
      path.join(project, 'Packages', 'packages-lock.json'),
      JSON.stringify({
        dependencies: {
          [PKG]: { source: 'git', hash: 'deadbeef' },
          'com.unity.ugui': { version: '2.5.0', source: 'registry' },
        },
      })
    );
    invalidatePackageLock(project, PKG);
    expect(lockFile().dependencies['com.unity.ugui']).toEqual({
      version: '2.5.0',
      source: 'registry',
    });
  });

  it('is a no-op when the lock file is absent', () => {
    const r = invalidatePackageLock(project, PKG);
    expect(r.removed).toBe(false);
    expect(r.previousHash).toBeNull();
  });

  it('is a no-op when the package is not pinned', () => {
    fs.writeFileSync(
      path.join(project, 'Packages', 'packages-lock.json'),
      JSON.stringify({ dependencies: { 'com.unity.ugui': { version: '2.5.0' } } })
    );
    const r = invalidatePackageLock(project, PKG);
    expect(r.removed).toBe(false);
  });

  it('fails loudly on a malformed lock file rather than reporting a false success', () => {
    // Silently ignoring it would leave the package pinned while telling the user
    // the update succeeded, which is worse than surfacing the corrupt file.
    fs.writeFileSync(path.join(project, 'Packages', 'packages-lock.json'), 'not json');
    expect(() => invalidatePackageLock(project, PKG)).toThrow(/Invalid JSON/);
  });
});
