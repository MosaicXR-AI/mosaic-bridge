import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { MOSAIC_PROMPTS } from '../src/prompts.js';
import { createTestHarness, type TestHarness } from './helpers.js';

describe('MCP prompt definitions', () => {
  it('exposes the four workflow prompts', () => {
    const names = MOSAIC_PROMPTS.map((p) => p.name).sort();
    expect(names).toEqual(['preflight', 'scene-interview', 'session-handoff', 'shader-guide']);
  });

  it('scene-interview builds non-empty guidance text', () => {
    const p = MOSAIC_PROMPTS.find((p) => p.name === 'scene-interview')!;
    const text = p.build({});
    expect(text).toMatch(/interview/i);
    expect(text.length).toBeGreaterThan(200);
  });
});

describe('Prompts Protocol (through the server)', () => {
  let h: TestHarness;
  beforeAll(async () => {
    h = await createTestHarness();
  });
  afterAll(async () => {
    await h.cleanup();
  });

  it('declares the prompts capability', () => {
    const caps = h.client.getServerCapabilities();
    expect(caps?.prompts).toBeDefined();
  });

  it('prompts/list returns all four prompts', async () => {
    const res = await h.client.listPrompts();
    expect(res.prompts.map((p) => p.name).sort()).toEqual([
      'preflight',
      'scene-interview',
      'session-handoff',
      'shader-guide',
    ]);
  });

  it('prompts/get returns a text message for a known prompt', async () => {
    const res = await h.client.getPrompt({ name: 'preflight' });
    expect(res.messages).toHaveLength(1);
    expect(res.messages[0].role).toBe('user');
    const content = res.messages[0].content;
    expect(content.type).toBe('text');
    expect((content as { text: string }).text).toMatch(/preflight/i);
  });

  it('prompts/get rejects an unknown prompt', async () => {
    await expect(h.client.getPrompt({ name: 'does-not-exist' })).rejects.toThrow();
  });
});
