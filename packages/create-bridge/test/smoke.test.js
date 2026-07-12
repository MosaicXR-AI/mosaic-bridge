import { describe, it, expect } from 'vitest';
import pkg from '../package.json' with { type: 'json' };

describe('create-bridge test harness', () => {
  it('loads package.json and sees the published version', () => {
    expect(pkg.name).toBe('@mosaicxr-ai/create-bridge');
    expect(pkg.version).toMatch(/^\d+\.\d+\.\d+/);
  });
});
