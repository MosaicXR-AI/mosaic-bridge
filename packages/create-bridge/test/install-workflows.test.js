import { describe, it, expect, afterEach } from 'vitest';
import { execFileSync } from 'node:child_process';
import {
  mkdtempSync,
  rmSync,
  existsSync,
  readdirSync,
  writeFileSync,
  mkdirSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { copyDirSync } from '../src/utils.js';

const PKG_DIR = path.resolve(fileURLToPath(import.meta.url), '../..');
const PLUGIN = path.join(PKG_DIR, 'plugin');
const BIN = path.join(PKG_DIR, 'bin', 'create-bridge.js');

let tmp;
afterEach(() => {
  if (tmp) rmSync(tmp, { recursive: true, force: true });
  tmp = undefined;
});

describe('workflow installation', () => {
  it('copyDirSync brings the workflow set across', () => {
    tmp = mkdtempSync(path.join(tmpdir(), 'mosaic-'));
    const dst = path.join(tmp, '.claude', 'workflows');
    copyDirSync(path.join(PLUGIN, 'workflows'), dst);
    expect(existsSync(dst)).toBe(true);
    const dirs = readdirSync(dst);
    expect(dirs).toEqual(
      expect.arrayContaining(['preflight', 'scene-plan', 'shader-guide', 'session-handoff'])
    );
  });

  it('the installer writes skills and workflows into the project', () => {
    tmp = mkdtempSync(path.join(tmpdir(), 'mosaic-proj-'));
    // Make it look like a Unity project so validation passes.
    mkdirSync(path.join(tmp, 'Assets'), { recursive: true });
    mkdirSync(path.join(tmp, 'ProjectSettings'), { recursive: true });
    writeFileSync(
      path.join(tmp, 'ProjectSettings', 'ProjectVersion.txt'),
      'm_EditorVersion: 6000.5.5f1\n'
    );

    execFileSync(
      process.execPath,
      [BIN, '--project-path', tmp, '--yes', '--skip-unity', '--skip-clients', '--skip-claude'],
      { encoding: 'utf8' }
    );

    expect(existsSync(path.join(tmp, '.claude', 'skills', 'mosaic-guide'))).toBe(true);
    expect(existsSync(path.join(tmp, '.agents', 'skills', 'mosaic-guide'))).toBe(true);
    expect(existsSync(path.join(tmp, '.claude', 'workflows', 'preflight'))).toBe(true);
  });
});
