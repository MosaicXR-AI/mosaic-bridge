import { describe, it, expect } from 'vitest';
import {
  parseUnityVersion,
  compareUnityVersions,
  checkUnityVersion,
  checkNodeVersion,
} from '../src/requirements.js';

const cmp = (a, b) => compareUnityVersions(parseUnityVersion(a), parseUnityVersion(b));

describe('parseUnityVersion', () => {
  it('parses a final release', () => {
    expect(parseUnityVersion('6000.5.5f1')).toMatchObject({
      major: 6000, minor: 5, patch: 5, stream: 'f', streamNumber: 1,
    });
  });

  it('parses alpha and beta streams', () => {
    expect(parseUnityVersion('6000.6.0a2')).toMatchObject({ stream: 'a', streamNumber: 2 });
    expect(parseUnityVersion('6000.6.0b5')).toMatchObject({ stream: 'b', streamNumber: 5 });
  });

  it('returns null for unparseable input', () => {
    for (const bad of ['', 'not-a-version', null, undefined, 42]) {
      expect(parseUnityVersion(bad)).toBeNull();
    }
  });
});

describe('compareUnityVersions', () => {
  it('orders by major/minor/patch', () => {
    expect(cmp('6000.3.0f1', '6000.5.0f1')).toBeLessThan(0);
    expect(cmp('6000.5.5f1', '6000.5.4f1')).toBeGreaterThan(0);
    expect(cmp('6000.5.5f1', '6000.5.5f1')).toBe(0);
  });

  it('orders prerelease streams alpha < beta < final', () => {
    expect(cmp('6000.6.0a2', '6000.6.0b5')).toBeLessThan(0);
    expect(cmp('6000.6.0b5', '6000.6.0f1')).toBeLessThan(0);
    expect(cmp('6000.6.0b1', '6000.6.0b5')).toBeLessThan(0);
  });
});

describe('checkUnityVersion — matches the documented support matrix', () => {
  it('accepts the verified releases', () => {
    expect(checkUnityVersion('6000.3.10f1').level).toBe('ok');
    expect(checkUnityVersion('6000.5.5f1').level).toBe('ok');
  });

  it('rejects everything below 6000.3 (no EntityId)', () => {
    for (const v of ['6000.0.0f1', '6000.2.9f1', '2022.3.10f1', '2021.3.0f1']) {
      const r = checkUnityVersion(v);
      expect(r.level, v).toBe('error');
      expect(r.message).toMatch(/EntityId/);
    }
  });

  it('warns on 6000.4 rather than blocking', () => {
    const r = checkUnityVersion('6000.4.12f1');
    expect(r.level).toBe('warn');
    expect(r.message).toMatch(/deprecation warnings/);
  });

  it('blocks every prerelease, not just the ones we happened to test', () => {
    for (const v of ['6000.6.0a2', '6000.6.0b5', '6000.7.0a1', '7000.1.0b3']) {
      const r = checkUnityVersion(v);
      expect(r.level, v).toBe('error');
      expect(r.message).toMatch(/prerelease/);
    }
  });

  it('still accepts a stable release of an unverified version', () => {
    expect(checkUnityVersion('6000.6.0f1').level).toBe('warn');
  });

  it('warns, but does not block, on unverified future versions', () => {
    expect(checkUnityVersion('6000.7.0f1').level).toBe('warn');
    expect(checkUnityVersion('7000.1.0f1').level).toBe('warn');
  });

  it('reports unknown rather than guessing when the version is unreadable', () => {
    expect(checkUnityVersion(null).level).toBe('unknown');
    expect(checkUnityVersion('garbage').level).toBe('unknown');
  });

  it('always explains what to do', () => {
    for (const v of ['6000.0.0f1', '6000.6.0b5', 'garbage']) {
      expect(checkUnityVersion(v).message.length).toBeGreaterThan(40);
    }
  });
});

describe('checkNodeVersion', () => {
  it('accepts Node 18 and newer', () => {
    for (const v of ['v18.0.0', 'v20.11.1', 'v22.3.0']) {
      expect(checkNodeVersion(v).level, v).toBe('ok');
    }
  });

  it('rejects Node older than 18 with an actionable message', () => {
    const r = checkNodeVersion('v16.20.0');
    expect(r.level).toBe('error');
    expect(r.message).toMatch(/nodejs\.org/);
  });

  it('defaults to the running process version', () => {
    expect(checkNodeVersion().level).toBe('ok');
  });
});

// ── end-to-end: the CLI must actually act on these checks ──────────────────

import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const BIN = path.resolve(fileURLToPath(import.meta.url), '../../bin/create-bridge.js');

function unityProject(version) {
  const dir = mkdtempSync(path.join(tmpdir(), 'mosaic-req-'));
  mkdirSync(path.join(dir, 'Assets'), { recursive: true });
  mkdirSync(path.join(dir, 'ProjectSettings'), { recursive: true });
  mkdirSync(path.join(dir, 'Packages'), { recursive: true });
  writeFileSync(path.join(dir, 'ProjectSettings', 'ProjectVersion.txt'), `m_EditorVersion: ${version}\n`);
  return dir;
}

const runInstaller = (dir, extra = []) =>
  execFileSync(process.execPath,
    [BIN, '--project-path', dir, '--yes', '--skip-clients', '--skip-claude', ...extra],
    { encoding: 'utf8', stdio: 'pipe' });

describe('CLI enforces the requirements', () => {
  it('refuses to install into an unsupported editor', () => {
    const dir = unityProject('6000.0.0f1');
    try {
      expect(() => runInstaller(dir)).toThrow();
    } finally { rmSync(dir, { recursive: true, force: true }); }
  });

  it('--ignore-unity-version overrides the refusal', () => {
    const dir = unityProject('6000.0.0f1');
    try {
      expect(() => runInstaller(dir, ['--ignore-unity-version'])).not.toThrow();
    } finally { rmSync(dir, { recursive: true, force: true }); }
  });

  it('reads a Windows CRLF ProjectVersion.txt', () => {
    // Unity on Windows writes CRLF; a version parsed as "6000.5.5f1\r" would be
    // unrecognisable and wrongly reported as unknown.
    const dir = mkdtempSync(path.join(tmpdir(), 'mosaic-crlf-'));
    mkdirSync(path.join(dir, 'Assets'), { recursive: true });
    mkdirSync(path.join(dir, 'ProjectSettings'), { recursive: true });
    mkdirSync(path.join(dir, 'Packages'), { recursive: true });
    writeFileSync(
      path.join(dir, 'ProjectSettings', 'ProjectVersion.txt'),
      'm_EditorVersion: 6000.5.5f1\r\nm_EditorVersionWithRevision: 6000.5.5f1 (abc)\r\n'
    );
    try {
      expect(() => runInstaller(dir)).not.toThrow();
    } finally { rmSync(dir, { recursive: true, force: true }); }
  });

  it('installs normally into a supported editor', () => {
    const dir = unityProject('6000.5.5f1');
    try {
      expect(() => runInstaller(dir)).not.toThrow();
    } finally { rmSync(dir, { recursive: true, force: true }); }
  });
});
