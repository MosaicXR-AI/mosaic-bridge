# Free Bridge Roadmap — Master Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship all 17 open-source ("free bridge") roadmap items in `mosaic-bridge` — closing packaging/diagnostics leaks, delivering the promised runtime layer, and completing the four free backlog epics (audio, ML, networking, performance) — without ever adding a Pro tool to this Apache-2.0 repo.

**Architecture:** Three subsystems move in parallel: the **npm layer** (`packages/mcp-server` TypeScript stdio server + `packages/create-bridge` installer CLI), the **Unity package** (`packages/com.mosaic.bridge` C# Editor tools + knowledge base), and **docs**. Work is sequenced in three waves by payoff-vs-effort. Each large epic (Wave 1/2) gets its own detailed sub-plan at kickoff; this master plan carries fully-executable tasks for Wave 0 and spec-level task contracts (files, interfaces, acceptance) for Waves 1–2.

**Tech Stack:** Node ≥18, TypeScript 5.3, `@modelcontextprotocol/sdk` ^1.0, vitest ^4.1 (mcp-server), `@clack/prompts`/commander (create-bridge). Unity Editor C#, NUnit EditMode, the `[MosaicTool]` reflection registry, JSON knowledge base, fixture-driven regression harness.

## Global Constraints

- **Apache-2.0 only.** Everything committed here is public. **Never** implement a Pro tool category (`measure/`, `annotation/`, `analysis/sightline`, `analysis/solar`, `data/heatmap`, `process/flow`, `process/state`, `view/explode`, `sensor/`, `timeseries/`, or anything from epics E33–E44). If a task drifts into one, stop and move it to `mosaic-pro`.
- **Patent block:** Do **not** ship Position-Based Dynamics fluid solving (E24.6) before **2027-08-14** (NVIDIA patent). Existing SPH/Verlet/spring-mass sims are fine; PBD specifically is blocked.
- **Tool pattern (Unity):** every tool = 3 files (`{Action}Params.cs` with `[Required]` on mandatory fields, `{Action}Result.cs`, `{Action}Tool.cs` as a `static class` with `[MosaicTool("category/action", …)]`) + a unit test under `Tests/Unit/Tools/{Category}/` + a `*_smoke.json` regression fixture under `Tests/Regression/Fixtures/`. `CategoryCoverageTests` fails CI if any category lacks a fixture.
- **Workflow after each Unity story:** Unity console clean (no errors/warnings) → full test suite green → BMad code review before commit.
- **Loopback only, HMAC-signed.** All MCP↔bridge traffic is `127.0.0.1` HTTP with HMAC-SHA256 request signing (nonce + timestamp). Don't weaken this.
- **Naming:** MCP server names normalize to `[a-z0-9-]` (Gemini rejects underscores). Tool IDs stay `category/action` kebab.
- **Release hygiene (repo rule):** after shipping any package version, update README + CHANGELOG + any version-referencing docs in the same change.
- **Commit style:** conventional commits; frequent, one deliverable each; end messages with the Co-Authored-By trailer.

---

## Wave 0 — P0: unlock value already built (ship this month)

These are leaks, not features. The value already exists; users aren't receiving it. Fastest payoff on the board.

### Task 1: Test harness for `create-bridge`

The installer has **zero** tests today (no `test/` dir, no `test` script). Every later installer task is TDD, so this scaffolding comes first and folds into Task 2's first real deliverable.

**Files:**
- Modify: `packages/create-bridge/package.json` (add `vitest` devDep + `test` script)
- Create: `packages/create-bridge/vitest.config.js`
- Create: `packages/create-bridge/test/smoke.test.js` (one trivial passing test proving the harness runs)

**Interfaces:**
- Produces: a working `npm test` in `packages/create-bridge` that runs vitest over `test/**/*.test.js`.

- [ ] **Step 1: Add vitest devDependency and test script**

In `packages/create-bridge/package.json`, add a `scripts` block (there is none today) and a devDependency:

```json
  "scripts": {
    "test": "vitest run",
    "test:watch": "vitest"
  },
  "devDependencies": {
    "vitest": "^4.1.0"
  },
```

Place `"scripts"` right after the `"bin"` block and `"devDependencies"` right after `"dependencies"`.

- [ ] **Step 2: Create vitest config**

Create `packages/create-bridge/vitest.config.js`:

```js
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    include: ['test/**/*.test.js'],
    environment: 'node',
  },
});
```

- [ ] **Step 3: Write a smoke test that proves the harness runs**

Create `packages/create-bridge/test/smoke.test.js`:

```js
import { describe, it, expect } from 'vitest';
import pkg from '../package.json' with { type: 'json' };

describe('create-bridge test harness', () => {
  it('loads package.json and sees the published version', () => {
    expect(pkg.name).toBe('@mosaicxr-ai/create-bridge');
    expect(pkg.version).toMatch(/^\d+\.\d+\.\d+/);
  });
});
```

- [ ] **Step 4: Install and run**

Run: `cd packages/create-bridge && npm install && npm test`
Expected: 1 passing test.

- [ ] **Step 5: Commit**

```bash
git add packages/create-bridge/package.json packages/create-bridge/package-lock.json packages/create-bridge/vitest.config.js packages/create-bridge/test/smoke.test.js
git commit -m "test(create-bridge): add vitest harness"
```

---

### Task 2: Fix version reporting

`create-bridge/src/cli.js:6` hardcodes `1.0.0-beta.4` (package is `beta.7`); `mcp-server/src/index.ts:88` prints `"mosaic-mcp (see package.json for version)"`. Both make every beta bug report carry wrong version info. Fix: read the real version from `package.json` at runtime.

**Files:**
- Modify: `packages/create-bridge/src/cli.js:1-13`
- Test: `packages/create-bridge/test/version.test.js` (Create)
- Modify: `packages/mcp-server/src/index.ts:79-90`
- Test: `packages/mcp-server/test/version.test.ts` (Create)

**Interfaces:**
- Produces (create-bridge): `cli.js` exports/uses `VERSION` sourced from `package.json`, so `--version` matches the published number.
- Produces (mcp-server): `index.ts` exports `function getVersion(): string` reading `package.json`, used by the `--version` branch.

- [ ] **Step 1: Write the failing create-bridge version test**

Create `packages/create-bridge/test/version.test.js`:

```js
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
```

- [ ] **Step 2: Run it, verify it fails**

Run: `cd packages/create-bridge && npm test -- version`
Expected: FAIL — `VERSION` is `'1.0.0-beta.4'`, pkg.version is `'1.0.0-beta.7'` (or the import isn't exported).

- [ ] **Step 3: Read the real version in cli.js**

Replace the top of `packages/create-bridge/src/cli.js` (currently `const VERSION = '1.0.0-beta.4';`) with:

```js
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const pkgPath = path.resolve(fileURLToPath(import.meta.url), '../../package.json');
export const VERSION = JSON.parse(readFileSync(pkgPath, 'utf8')).version;
```

Keep the existing `.version(VERSION, '-v, --version', 'Show version')` line unchanged.

- [ ] **Step 4: Run it, verify it passes**

Run: `cd packages/create-bridge && npm test -- version`
Expected: PASS.

- [ ] **Step 5: Write the failing mcp-server version test**

Create `packages/mcp-server/test/version.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { getVersion } from '../src/index.js';

const pkg = JSON.parse(
  readFileSync(path.resolve(fileURLToPath(import.meta.url), '../../package.json'), 'utf8')
);

describe('mosaic-mcp getVersion()', () => {
  it('returns the package.json version', () => {
    expect(getVersion()).toBe(pkg.version);
  });
});
```

- [ ] **Step 6: Run it, verify it fails**

Run: `cd packages/mcp-server && npx vitest run version`
Expected: FAIL — `getVersion` is not exported.

- [ ] **Step 7: Add getVersion() and use it in the --version branch**

In `packages/mcp-server/src/index.ts`, add after the imports:

```ts
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

export function getVersion(): string {
  const pkgPath = path.resolve(fileURLToPath(import.meta.url), '../../package.json');
  return JSON.parse(readFileSync(pkgPath, 'utf8')).version;
}
```

Then replace the `--version` branch body (lines ~86-90):

```ts
  if (opts.version) {
    process.stdout.write(`mosaic-mcp ${getVersion()}\n`);
    process.exit(0);
  }
```

Note: `../../package.json` resolves correctly from both `src/` (dev/test) and `dist/` (published) because both are one level under the package root.

- [ ] **Step 8: Run it, verify it passes**

Run: `cd packages/mcp-server && npx vitest run version`
Expected: PASS.

- [ ] **Step 9: Also stamp the Server() version**

In `packages/mcp-server/src/server.ts:56`, the `new Server({ name: 'mosaic-bridge', version: '1.0.0' }, …)` version is hardcoded. Pass it through `CreateServerOptions` instead. Add `version: string` to `CreateServerOptions` (line ~41-45), use `version` in the `Server` ctor, and in `index.ts` pass `version: getVersion()` into `createMosaicServer({ … })`. Update `packages/mcp-server/test/*` server-construction tests if any assert the version.

- [ ] **Step 10: Build, test, commit**

Run: `cd packages/mcp-server && npm run build && npm test`
Expected: build clean, all tests pass.

```bash
git add packages/create-bridge/src/cli.js packages/create-bridge/test/version.test.js \
        packages/mcp-server/src/index.ts packages/mcp-server/src/server.ts packages/mcp-server/test/version.test.ts
git commit -m "fix: report real package version in create-bridge and mcp-server --version"
```

---

### Task 3: Ship the complete plugin in the npm tarball + install workflows

Two defects: (a) `create-bridge/package.json` `files` ships only `plugin/skills`, `plugin/workflows`, `plugin/config.yaml` — the `agents/`, `commands/`, and `.claude-plugin/` dirs (Zara/Ray/Max agents + slash commands + the Claude Code plugin manifest) are excluded from the tarball entirely; (b) `flow.js` copies only `plugin/skills` into projects — `workflows/` is shipped-but-never-installed. Fix both: complete the tarball, and copy workflows alongside skills so the installed project actually has them.

**Files:**
- Modify: `packages/create-bridge/package.json:16-24` (`files` array)
- Modify: `packages/create-bridge/src/flow.js:11,101-115` (add workflows copy)
- Test: `packages/create-bridge/test/tarball-contents.test.js` (Create)
- Test: `packages/create-bridge/test/install-workflows.test.js` (Create)

**Interfaces:**
- Consumes: `copyDirSync(src, dst)` from `./utils.js` (already used for skills).
- Produces: after `runInteractive`, the project contains `.claude/skills/*`, `.agents/skills/*`, **and** `.claude/workflows/*`. The published tarball includes every `plugin/**` path.

- [ ] **Step 1: Write the failing tarball-contents test**

Create `packages/create-bridge/test/tarball-contents.test.js`. `npm pack --dry-run --json` lists exactly what would ship:

```js
import { describe, it, expect } from 'vitest';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const pkgDir = path.resolve(fileURLToPath(import.meta.url), '../..');

function packedFiles() {
  const out = execFileSync('npm', ['pack', '--dry-run', '--json'], { cwd: pkgDir, encoding: 'utf8' });
  return JSON.parse(out)[0].files.map(f => f.path);
}

describe('published tarball', () => {
  const files = packedFiles();
  it('includes the full plugin: agents, commands, and .claude-plugin manifest', () => {
    expect(files.some(f => f.startsWith('plugin/agents/'))).toBe(true);
    expect(files.some(f => f.startsWith('plugin/commands/'))).toBe(true);
    expect(files.some(f => f === 'plugin/.claude-plugin/plugin.json')).toBe(true);
  });
  it('still includes skills and workflows', () => {
    expect(files.some(f => f.startsWith('plugin/skills/'))).toBe(true);
    expect(files.some(f => f.startsWith('plugin/workflows/'))).toBe(true);
  });
});
```

- [ ] **Step 2: Run it, verify it fails**

Run: `cd packages/create-bridge && npm test -- tarball`
Expected: FAIL — agents/commands/.claude-plugin not in the packed file list.

- [ ] **Step 3: Complete the `files` array**

In `packages/create-bridge/package.json`, replace the plugin entries in `files` with a single recursive include plus the dotfile manifest (npm excludes dot-directories unless named):

```json
  "files": [
    "bin/**/*.js",
    "src/**/*.js",
    "plugin/**",
    "plugin/.claude-plugin/**",
    "README.md",
    "LICENSE"
  ],
```

- [ ] **Step 4: Run it, verify it passes**

Run: `cd packages/create-bridge && npm test -- tarball`
Expected: PASS (all plugin paths present).

- [ ] **Step 5: Write the failing install-workflows test**

Create `packages/create-bridge/test/install-workflows.test.js`. This drives the real copy helpers against a temp project:

```js
import { describe, it, expect, afterEach } from 'vitest';
import { mkdtempSync, rmSync, existsSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { copyDirSync } from '../src/utils.js';

const PLUGIN = path.resolve(fileURLToPath(import.meta.url), '../../plugin');
let tmp;
afterEach(() => { if (tmp) rmSync(tmp, { recursive: true, force: true }); });

describe('workflow installation', () => {
  it('copies workflows into the project .claude/workflows', () => {
    tmp = mkdtempSync(path.join(tmpdir(), 'mosaic-'));
    const dst = path.join(tmp, '.claude', 'workflows');
    copyDirSync(path.join(PLUGIN, 'workflows'), dst);
    expect(existsSync(dst)).toBe(true);
    // preflight/scene-plan/shader-guide/session-handoff each ship a workflow.md
    const dirs = readdirSync(dst);
    expect(dirs).toEqual(expect.arrayContaining(['preflight', 'scene-plan', 'shader-guide', 'session-handoff']));
  });
});
```

- [ ] **Step 6: Run it, verify it fails or passes-trivially, then wire flow.js**

Run: `cd packages/create-bridge && npm test -- install-workflows`
(This tests the helper directly, so it may pass; the real wiring is in `flow.js`.) Now add a `WORKFLOWS_SRC` const and copy call to `flow.js`.

In `packages/create-bridge/src/flow.js`, after line 11:

```js
const WORKFLOWS_SRC = path.resolve(fileURLToPath(import.meta.url), '../../plugin/workflows');
```

In the skills-install `try` block (lines 105-115), after the two `copyDirSync` skills calls, add:

```js
    const workflowsDst = path.join(projectInfo.projectPath, '.claude', 'workflows');
    copyDirSync(WORKFLOWS_SRC, workflowsDst);
```

and update the success message to `'✓ Mosaic Bridge skills + workflows installed (.claude + .agents)'`.

- [ ] **Step 7: Full installer smoke via --skip-unity --skip-clients on a fake project**

Add to `install-workflows.test.js` a test that runs the CLI end-to-end against a temp Unity-looking dir (create `Assets/` + `ProjectSettings/ProjectVersion.txt`), invoking `bin/create-bridge.js --project-path <tmp> --yes --skip-unity --skip-clients --skip-claude`, then asserts `.claude/workflows/preflight` exists. Use `execFileSync(process.execPath, [binPath, …])`.

- [ ] **Step 8: Run the suite**

Run: `cd packages/create-bridge && npm test`
Expected: all pass.

- [ ] **Step 9: Commit**

```bash
git add packages/create-bridge/package.json packages/create-bridge/src/flow.js packages/create-bridge/test/tarball-contents.test.js packages/create-bridge/test/install-workflows.test.js
git commit -m "fix(create-bridge): ship full plugin in tarball and install workflows into project"
```

---

### Task 4: MCP prompts capability

The server declares only `tools` and `resources`. The scene-building interview, session-start preflight, shader guide, and session handoff are workflow rules that today reach users **only** if the installer wrote a client instruction file — every other client silently misses them. Exposing them as native MCP prompts (`prompts/list` + `prompts/get`) delivers them over the protocol to any client.

**Files:**
- Modify: `packages/mcp-server/src/server.ts:55-63` (add `prompts: {}` capability), and register `ListPromptsRequestSchema` + `GetPromptRequestSchema` handlers
- Create: `packages/mcp-server/src/prompts.ts` (the four prompt definitions)
- Test: `packages/mcp-server/test/prompts.test.ts` (Create)

**Interfaces:**
- Produces: `export const MOSAIC_PROMPTS: McpPromptDef[]` where `McpPromptDef = { name: string; description: string; arguments?: {name,description,required}[]; build(args): string }`. Prompt names: `scene-interview`, `preflight`, `shader-guide`, `session-handoff`.
- Consumes: prompt *content* is derived from the same source text as `templates.js` — extract the shared blocks so installer instruction files and MCP prompts don't drift. (Create `packages/create-bridge/src/protocol-blocks.js`? No — keep MCP self-contained: copy the canonical block text into `prompts.ts` and add a CI check `check-stale-docs`-style that diffs the two. Simpler: `prompts.ts` owns the text; a follow-up unifies.)

- [ ] **Step 1: Write the failing prompts test**

Create `packages/mcp-server/test/prompts.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { MOSAIC_PROMPTS } from '../src/prompts.js';

describe('MCP prompts', () => {
  it('exposes the four workflow prompts', () => {
    const names = MOSAIC_PROMPTS.map(p => p.name).sort();
    expect(names).toEqual(['preflight', 'scene-interview', 'session-handoff', 'shader-guide']);
  });
  it('scene-interview builds non-empty guidance text', () => {
    const p = MOSAIC_PROMPTS.find(p => p.name === 'scene-interview')!;
    const text = p.build({});
    expect(text).toMatch(/interview/i);
    expect(text.length).toBeGreaterThan(200);
  });
});
```

- [ ] **Step 2: Run it, verify it fails**

Run: `cd packages/mcp-server && npx vitest run prompts`
Expected: FAIL — `../src/prompts.js` does not exist.

- [ ] **Step 3: Create prompts.ts**

Create `packages/mcp-server/src/prompts.ts` with the four definitions. Content is the canonical protocol text (Scene-Building Interview Protocol, Session Start / preflight, ShaderGraph Serialization Rules, Session Handoff) lifted from `create-bridge/src/templates.js`'s `BASE_INSTRUCTIONS`. Shape:

```ts
export interface McpPromptDef {
  name: string;
  description: string;
  arguments?: { name: string; description: string; required: boolean }[];
  build(args: Record<string, string>): string;
}

const SCENE_INTERVIEW = `On any vague "build a scene" request, STOP. Call no tools yet. Ask these Tier-1 questions:
1. Scene type (interior/exterior/stylized/realistic)?
2. Geographic or art reference?
3. Scale (room / building / district / open world)?
4. Player perspective (first-person / top-down / orbit)?
Then produce a ScenePlan summary and WAIT for confirmation before building.
Spatial coherence: every placed object's Y = terrain/sample-height(x,z) + offset; never Y=0 outdoors.`;

// … PREFLIGHT, SHADER_GUIDE, SESSION_HANDOFF blocks …

export const MOSAIC_PROMPTS: McpPromptDef[] = [
  { name: 'scene-interview', description: 'Interview protocol before building any scene', build: () => SCENE_INTERVIEW },
  { name: 'preflight', description: 'Run project/preflight and interpret pipeline/packages/errors at session start', build: () => PREFLIGHT },
  { name: 'shader-guide', description: 'ShaderGraph serialization + node-wiring rules (Unity 14.x+)', build: () => SHADER_GUIDE },
  { name: 'session-handoff', description: 'Write docs/Sessions/{user}/SESSION_NOTES.md before ending', build: () => SESSION_HANDOFF },
];
```

Copy the full block text verbatim from `templates.js` — do not paraphrase (keeps prompts and installer files in lockstep).

- [ ] **Step 4: Run it, verify it passes**

Run: `cd packages/mcp-server && npx vitest run prompts`
Expected: PASS.

- [ ] **Step 5: Register the handlers in server.ts**

Import `ListPromptsRequestSchema`, `GetPromptRequestSchema` from the SDK types. Add `prompts: {}` to the `capabilities` object (line ~58-62). After the resources handlers, register:

```ts
  server.setRequestHandler(ListPromptsRequestSchema, async () => ({
    prompts: MOSAIC_PROMPTS.map(p => ({
      name: p.name,
      description: p.description,
      arguments: p.arguments ?? [],
    })),
  }));

  server.setRequestHandler(GetPromptRequestSchema, async (req) => {
    const def = MOSAIC_PROMPTS.find(p => p.name === req.params.name);
    if (!def) throw new McpError(ErrorCode.InvalidRequest, `Unknown prompt: ${req.params.name}`);
    return {
      messages: [{
        role: 'user' as const,
        content: { type: 'text' as const, text: def.build(req.params.arguments ?? {}) },
      }],
    };
  });
```

- [ ] **Step 6: Add an integration test through the real Server**

Extend `prompts.test.ts` to build a server via `createMosaicServer(...)` with a mock client and assert `prompts/list` returns 4 and `prompts/get {name:'preflight'}` returns a text message. Follow the in-memory-transport pattern already used in the mcp-server test suite (mirror `server.test.ts` setup).

- [ ] **Step 7: Build, test, commit**

Run: `cd packages/mcp-server && npm run build && npm test`
Expected: clean.

```bash
git add packages/mcp-server/src/prompts.ts packages/mcp-server/src/server.ts packages/mcp-server/test/prompts.test.ts
git commit -m "feat(mcp-server): expose scene/preflight/shader/handoff workflow prompts via MCP prompts capability"
```

---

### Task 5: `bridge doctor` diagnostic command

Connection failures surface as an opaque "Connection closed" (the Windows path-hash bug proved how costly that is). A `doctor` subcommand that checks the discovery file, live editor PIDs, port reachability, HMAC handshake, and clock skew turns hour-long support threads into a copy-pasteable report.

**Files:**
- Create: `packages/mcp-server/src/doctor.ts` (checks + report formatter)
- Modify: `packages/mcp-server/src/index.ts:15-44,79-90` (parse `doctor` / `--doctor`, dispatch)
- Test: `packages/mcp-server/test/doctor.test.ts` (Create)

**Interfaces:**
- Consumes: `readDiscovery(opts)` (`discovery.ts`), `BridgeClient` (`bridge-client.ts`) `.health()`, the instance-registry reader used by discovery.
- Produces: `export async function runDoctor(opts: DiscoveryOptions): Promise<DoctorReport>` where `DoctorReport = { checks: {name, status: 'pass'|'warn'|'fail', detail: string}[]; ok: boolean }`, and `export function formatDoctorReport(r: DoctorReport): string`.

- [ ] **Step 1: Write failing tests for the check runner**

Create `packages/mcp-server/test/doctor.test.ts` — assert `runDoctor` with a bogus `runtimeDir` yields a `fail` on the "discovery file" check and `ok === false`; assert `formatDoctorReport` renders one line per check with a `✓/⚠/✗` glyph. Inject a fake discovery dir via `opts.runtimeDir` pointing at a temp folder.

- [ ] **Step 2: Run, verify fail** — `npx vitest run doctor` → FAIL (module missing).

- [ ] **Step 3: Implement `doctor.ts`** with checks, in order:
  1. **Discovery file** — exists + parses + HMAC signature valid (reuse discovery integrity check; `warn` if unsigned, `fail` if missing/corrupt).
  2. **Live editor** — instance-registry lists a live PID for this project (`fail` if zero, `warn` if multiple with guidance to pass `--project-path`).
  3. **Port reachable** — TCP connect to `127.0.0.1:<port>` (`fail` on ECONNREFUSED).
  4. **Health + HMAC** — `client.health()` returns `status:'ok'` with a valid signature (`fail` on 401 → "secret rotated, restart client").
  5. **Clock skew** — compare local time to the discovery file mtime / a health-response timestamp; `warn` if >30s (HMAC nonce windows fail on skew).
  Each check is try/caught into a `{name,status,detail}` record; never throw out of `runDoctor`.

- [ ] **Step 4: Run, verify pass** — `npx vitest run doctor` → PASS.

- [ ] **Step 5: Wire the subcommand** — in `index.ts`, treat `argv[0] === 'doctor'` (or `--doctor`) as a mode: run `runDoctor`, `process.stdout.write(formatDoctorReport(r))`, exit `r.ok ? 0 : 1`. Add `doctor` to `printUsage()`.

- [ ] **Step 6: Manual smoke** — with a bridge NOT running: `node dist/index.js doctor --project-path <path>` prints a readable report ending in a fail. Document expected output in the test as a snapshot.

- [ ] **Step 7: Build, test, commit**

```bash
git add packages/mcp-server/src/doctor.ts packages/mcp-server/src/index.ts packages/mcp-server/test/doctor.test.ts
git commit -m "feat(mcp-server): add 'doctor' command for connection diagnostics"
```

---

### Wave 0 wrap-up

- [ ] Update `packages/mcp-server/CHANGELOG.md` and `packages/create-bridge/CHANGELOG.md` with the four fixes; bump both to the next `-beta` (mcp-server → beta.7, create-bridge → beta.8). Update the root `README.md` "What's inside" if it enumerates version numbers or the `doctor`/prompts capabilities.
- [ ] Run both package test suites green.
- [ ] BMad code review, then a release commit.

---

## Wave 1 — P1: the promised runtime layer (this quarter)

Each item below is spec-level. **At kickoff, each gets its own detailed TDD sub-plan** via the writing-plans skill (they're multi-day-to-multi-week and writing full speculative code now would be a plan failure). Sequence within the wave: 1.4 (play-mode) and 1.1 (KB tools) first — they de-risk everything else — then 1.2 (execute-plan) which depends on both, then 1.3/1.6 (new tool categories) in parallel, docs (1.5) continuous.

### Task 1.1: Runtime KB tools — `kb/query`, `kb/fetch`, `kb/watch`

**Subsystem:** Unity package (new `Editor/Tools/Kb/` category) + MCP server passthrough.
**Files (Unity):** `Editor/Tools/Kb/{Query,Fetch,Watch}{Params,Result,Tool}.cs` + `Tests/Unit/Tools/Kb/KbToolTests.cs` + 3 regression fixtures. Reuse `Editor/Core/KnowledgeProvider/KnowledgeBase.cs`.
**Files (MCP):** none new — these register through the dynamic registry automatically; add a passthrough integration test.
**Acceptance:** `kb/query {q:"URP color property"}` returns ranked KB entries with snippets; `kb/fetch {category,key}` returns full entry JSON; `kb/watch {sessionId}` returns entries whose `sceneState` drifted from a recorded ScenePlan. All three appear in `tools/list` over MCP. Answers the fluid-session pain (3–4 iterations on `_Color` vs `_BaseColor`).
**Effort:** ~weeks. **Roadmap:** named v1.0-stable item.

### Task 1.2: `scene/execute-plan`

**Subsystem:** Unity package (`Editor/Tools/Scene/`).
**Files:** `ExecutePlanParams.cs` (takes a validated ScenePlan JSON), `ExecutePlanResult.cs`, `ExecutePlanTool.cs` + unit test + fixture. Drives the existing 8-phase pipeline (terrain → water → textures → sky/lighting → structures → vegetation → post → camera) and enforces the spatial-coherence contract via `terrain/sample-height`.
**Interfaces:** consumes the ScenePlan schema from KB entry `scene/scene-composition-guide`; calls existing tools (`terrain/*`, `gameobject/*`, `material/*`) through the in-process dispatcher.
**Acceptance:** given a ScenePlan, one call builds the scene in deterministic phase order with all Y-positions resolved to terrain height; returns per-phase results + screenshots. Marked "optional/not built" in the current spec — this ships it.
**Effort:** ~weeks. **Depends on:** 1.1 (KB-driven validation), 1.4 (play-mode for sims).

### Task 1.3: Performance & scale tools — `optimize/*` (epic E42)

**Subsystem:** Unity package (new `Editor/Tools/Optimize/`).
**Tools:** `optimize/mesh-combine`, `optimize/gpu-instancing`, `optimize/batching`, `optimize/occlusion-setup`, `optimize/suggest` (reads the existing `profiler/*` tools and recommends fixes). Each = 3 files + unit test + fixture.
**Acceptance:** `optimize/suggest` returns actionable items keyed off profiler stats; `optimize/mesh-combine` reduces draw calls on a test scene (assert combined mesh count). Broadest-audience free epic.
**Effort:** ~weeks.

### Task 1.4: Play-mode awareness on tools

**Subsystem:** Unity package (Contracts + pipeline).
**Files:** add `RequiresPlayMode` to `MosaicToolAttribute` (`Editor/Contracts/Attributes/`); a pipeline stage warning (`Editor/Core/Pipeline/Stages/`) that flags/optionally auto-enters play mode; tag the sim tools (fluid, smoke, cloth, etc.). Tests under `Tests/Unit/`.
**Acceptance:** calling a `RequiresPlayMode` tool in edit mode returns a pipeline warning ("enter Play mode to see results") instead of silent nothing; opens the door to PlayMode test coverage. Fixes the fluid-session "nothing shows" dead-end.
**Effort:** ~days. **Do first in wave** (de-risks 1.2/1.3).

### Task 1.5: Bridge docs site + `TESTING.md`

**Subsystem:** docs (`mosaic-docs` + repo root).
**Files:** `TESTING.md` at repo root (README links it, it doesn't exist); real bridge content in `mosaic-docs/docs` (currently 100% BMAD, zero bridge). Tool reference generated from the registry, quickstart, the Scene Intelligence guide.
**Acceptance:** README's `TESTING.md` link resolves; docs site has a bridge quickstart + tool reference. Both are v1.0-stable / Asset Store prerequisites.
**Effort:** ~weeks (continuous through the wave).

### Task 1.6: Audio & signal tools — `audio/*` (epic E27)

**Subsystem:** Unity package (extend `Editor/Tools/Audio/` — thinnest category, 3 tools today).
**Tools:** `audio/procedural-sfx` (synthesized clips), `audio/mixer-create` + `audio/mixer-route`, `audio/spatial-profile` (occlusion/reverb zones). Each = 3 files + test + fixture. Ship the planned `audio-attenuation` KB entry alongside.
**Acceptance:** new tools appear in `tools/list`; procedural SFX generates a playable AudioClip asset; KB entry present. Knowledge + tools land together.
**Effort:** ~weeks.

---

## Wave 2 — P2: bigger bets & polish (next horizon)

Spec-level; each gets a sub-plan at kickoff. Ordered by leverage.

### Task 2.1: Terrain at scale + tree-import fix
**Unity.** Tiling guidance/tools for large scenes (resolution insufficient today) + fix trees whose nested-child `MeshRenderer`s are ignored by Unity's terrain system (flatten renderers on import). Both documented in gap analysis. Add `terrain/tile`-style tool + a fix in the tree-placement path + regression fixtures. **Effort:** ~weeks.

### Task 2.2: KB expansion pack
**Unity KB.** Add the README-named missing domains: animation timing, spatial metrics, color temperature, lighting presets. JSON entries + `.meta`, wire into `INDEX`, keep 100% tool coverage. Compounds with `kb/query` (1.1). **Effort:** ~days.

### Task 2.3: Deprecated-API migration tool
**Unity.** `script/migrate-api` (or `reflection/`-backed) scans project scripts for obsolete Unity APIs and auto-fixes, built on existing `script/*` + `reflection/*`. v1.1 roadmap item. 3 files + test + fixture. **Effort:** ~weeks.

### Task 2.4: Procgen visual regression goldens
**Unity tests.** No real session has ever exercised the 17 procgen tools — least-validated flagship. Add screenshot-based golden fixtures through the existing regression harness (`Tests/Regression/`) for each procgen algo. **Effort:** ~weeks.

### Task 2.5: Networking & multiplayer — `network/*` (epic E32)
**Unity.** Netcode-for-GameObjects scaffolding, `NetworkObject` setup, sync validation. Big surface + heavy test burden — schedule **after** E42 (1.3). Full sub-plan required. **Effort:** ~quarter.

### Task 2.6: In-engine ML — `ml/*` (epic E29)
**Unity.** Unity Sentis model import + inference tools. Differentiating (no competing bridge has it) but niche today — a v1.2 headline. Full sub-plan required. **Effort:** ~quarter.

### Task 2.7: HTTP transport + TLS for the MCP server
**MCP server.** Streamable-HTTP transport for remote/containerized clients; the discovery contract already reserves `tls_enabled`. **Defer** until a real remote-client request exists — stdio covers every currently supported client. **Effort:** ~quarter.

---

## Blocked — do not build here
- **PBD fluids (E24.6):** patent-blocked until **2027-08-14**.
- **All Pro categories (E33–E44 + measure/annotation/analysis/sensor/timeseries/etc.):** belong in the private `mosaic-pro` repo. Committing here makes them Apache-2.0, unrecoverably.

---

## Findings from live-bridge testing (2026-07-12)

Discovered while building the procgen demo scene against a live Unity 6000.3 bridge. Both are real
product defects worth their own fixes:

- **`editor/run-block` fixed ~22s job timeout is too short.** On any project where compile +
  domain-reload exceeds ~22s, the submitted block compiles (reaches `pending`) but its post-reload
  execution never completes inside the server-side job budget, so it always returns `timed out`. There
  is no way to extend it. This makes scripted editor automation (e.g. creating a post-processing
  volume, bulk asset ops) unusable on non-trivial projects. **Fix:** make the timeout configurable /
  much larger, or poll indefinitely with a heartbeat rather than a hard job deadline. *(P1-class.)*
- **Atmospheric skybox shaders clip without tonemapping.** `rendering/atmosphere-create` emits HDR sky
  values that clip to mustard (Preetham) or white (Bruneton) unless a URP tonemapping volume is present
  — and there is no tool to create a post-processing/tonemapping volume, so an AI agent cannot produce a
  good-looking atmospheric sky unaided. **Fix:** add a `graphics/post-process-create` (or
  `rendering/tonemapping`) tool that builds a global Volume + VolumeProfile with ACES tonemapping and
  enables `renderPostProcessing` on the target camera. *(P2-class; also unblocks polished demo imagery.)*

## Self-Review

**Spec coverage:** all 17 roadmap items mapped — Wave 0: version fix (Task 2), plugin/tarball (Task 3), MCP prompts (Task 4), doctor (Task 5); Wave 1: KB tools (1.1), execute-plan (1.2), optimize (1.3), play-mode (1.4), docs (1.5), audio (1.6); Wave 2: terrain (2.1), KB expansion (2.2), API migration (2.3), procgen goldens (2.4), network (2.5), ml (2.6), HTTP/TLS (2.7). Task 1 (test harness) is enabling scaffolding folded into Wave 0. ✅
**Placeholder scan:** Wave 0 tasks carry real code and exact commands. Waves 1–2 are intentionally spec-level with explicit "own sub-plan at kickoff" — not placeholders but scoped deferrals, since writing speculative multi-week C# now would be the plan failure the skill warns against. ✅
**Type consistency:** `getVersion()`, `VERSION`, `copyDirSync`, `MOSAIC_PROMPTS`/`McpPromptDef`, `runDoctor`/`DoctorReport`/`formatDoctorReport` are named identically wherever referenced. ✅
