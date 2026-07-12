import { describe, it, expect, afterEach } from 'vitest';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { runDoctor, formatDoctorReport } from '../src/doctor.js';

let tmp: string | undefined;
afterEach(() => {
  if (tmp) rmSync(tmp, { recursive: true, force: true });
  tmp = undefined;
});

describe('runDoctor', () => {
  it('fails the discovery check when the file is missing', async () => {
    tmp = mkdtempSync(path.join(tmpdir(), 'mosaic-doctor-'));
    const report = await runDoctor({ runtimeDir: tmp });
    const discovery = report.checks.find((c) => c.name === 'Discovery file');
    expect(discovery?.status).toBe('fail');
    expect(report.ok).toBe(false);
  });

  it('fails the port check when a discovery file points at a dead port', async () => {
    tmp = mkdtempSync(path.join(tmpdir(), 'mosaic-doctor-'));
    // Port 1 is privileged and unbound in test — connect refuses immediately.
    writeFileSync(
      path.join(tmp, 'bridge-discovery.json'),
      JSON.stringify({
        schema_version: '1.0',
        port: 1,
        process_id: 999999,
        started_unix_seconds: Math.floor(Date.now() / 1000),
        secret_base64: Buffer.from('test-secret').toString('base64'),
        unity_project_path: '/tmp/fake',
        unity_version: '2022.3.50f1',
      })
    );
    const report = await runDoctor({ runtimeDir: tmp });
    const discovery = report.checks.find((c) => c.name === 'Discovery file');
    const port = report.checks.find((c) => c.name === 'Port reachable');
    expect(discovery?.status).toBe('warn'); // present but unsigned
    expect(port?.status).toBe('fail');
    expect(report.ok).toBe(false);
  });

  it('never throws — always returns a full report', async () => {
    const report = await runDoctor({ runtimeDir: '/nonexistent/does/not/exist' });
    expect(Array.isArray(report.checks)).toBe(true);
    expect(report.checks.length).toBeGreaterThan(0);
  });
});

describe('formatDoctorReport', () => {
  it('renders one glyph-prefixed line per check and a result line', () => {
    const text = formatDoctorReport({
      checks: [
        { name: 'Discovery file', status: 'pass', detail: 'ok' },
        { name: 'Port reachable', status: 'fail', detail: 'refused' },
      ],
      ok: false,
    });
    expect(text).toContain('✓ Discovery file');
    expect(text).toContain('✗ Port reachable');
    expect(text).toContain('PROBLEMS FOUND');
  });
});
