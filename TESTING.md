# Testing Mosaic Bridge

Mosaic Bridge has three test suites, one per package. The two npm packages run
headless in CI; the Unity package runs in the Unity Editor's Test Runner.

| Package | Location | Runner | Command |
|---------|----------|--------|---------|
| MCP server | `packages/mcp-server` | vitest | `npm test` |
| Installer CLI | `packages/create-bridge` | vitest | `npm test` |
| Unity package | `packages/com.mosaic.bridge` | Unity Test Runner (EditMode) | see below |

---

## MCP server — `packages/mcp-server`

TypeScript, tested with [vitest](https://vitest.dev). No Unity required — the
suite uses in-memory MCP transports and a mock bridge client.

```bash
cd packages/mcp-server
npm install
npm run build      # tsc → dist/
npm test           # vitest run
```

What's covered:

- **`conformance.test.ts`** — MCP protocol handshake, tools/resources/prompts
  capabilities, and tool-call error mapping, driven through a real
  `createMosaicServer` over an in-memory transport (`tests/helpers.ts`).
- **`discovery.test.ts`** — discovery-file path resolution, the project-hash
  derivation, and the cross-platform (Windows backslash) canonicalization.
- **`prompts.test.ts`** — the four workflow prompts (`preflight`,
  `scene-interview`, `session-handoff`, `shader-guide`) and their handlers.
- **`doctor.test.ts`** — the `doctor` diagnostic checks and report formatting.
- **`version.test.ts`** — `--version` reports the real `package.json` version.

Coverage thresholds are enforced by `vitest.config.ts` (statements 60,
branches 50, functions 60, lines 60), excluding `src/index.ts` (the CLI entry
point, exercised via the built binary).

```bash
npm run coverage   # writes coverage/ with an lcov report
```

Run the connection diagnostics against a live bridge:

```bash
node dist/index.js doctor --project-path /path/to/UnityProject
```

## Installer CLI — `packages/create-bridge`

JavaScript, tested with vitest. No Unity required — tests drive the copy
helpers and the CLI against temporary directories.

```bash
cd packages/create-bridge
npm install
npm test
```

What's covered:

- **`tarball-contents.test.js`** — `npm pack --dry-run` ships the full plugin
  (skills, workflows, agents, commands, and the `.claude-plugin` manifest).
- **`install-workflows.test.js`** — an end-to-end run against a temp Unity-shaped
  project writes skills into `.claude/skills` + `.agents/skills` and workflows
  into `.claude/workflows`.
- **`version.test.js`** — the CLI `--version` matches `package.json`.

## Unity package — `packages/com.mosaic.bridge`

The bridge is an **Editor-only** plugin, so all tests run in **EditMode**. There
is a single test assembly, `Mosaic.Bridge.Tests`.

To run:

1. Open a Unity project that includes the `com.mosaic.bridge` package.
2. `Window → General → Test Runner`.
3. Select the **EditMode** tab.
4. Run all, run by category, or run individual tests.

Test categories (see `packages/com.mosaic.bridge/Tests/README.md`):

| Folder | Type | Purpose |
|--------|------|---------|
| `Unit/` | NUnit `[Test]` | Framework-level unit tests, Unity APIs mocked where possible |
| `Integration/` | `[UnityTest]` EditMode | Tests requiring real Unity Editor APIs |
| `Regression/` | `[Test]` + `[Category("Regression")]` | End-to-end fixtures against the live HTTP bridge |
| `Fixtures/` | JSON | Input/expected-output data for regression tests |

### Regression suite requires a running bridge

`RegressionTestRunner` loads every `*_smoke.json` fixture in
`Tests/Regression/Fixtures/` and executes it against the live bridge HTTP
endpoint (HMAC-signed), producing one NUnit test per fixture. It only passes
when `BridgeBootstrap` is in the **Running** state — start Unity and let the
bridge come up before filtering the Test Runner to the `Regression` category.

`CategoryCoverageTests` enforces the coverage invariant: **every registered
tool category must have at least one fixture, and every fixture must reference a
real registered tool.** Adding a new tool category therefore requires adding a
matching `*_smoke.json` fixture, or this test fails.

### Conventions

- Test method naming: `MethodUnderTest_Condition_ExpectedResult`
  (e.g. `Create_NameIsNullOrEmpty_ReturnsFailWithInvalidParam`).
- Test class naming: `<ClassUnderTest>Tests`.
- EditMode only — PlayMode tests are not applicable to an Editor plugin.
- HttpListener tests use `[Test]` (not `[UnityTest]`) with sync HTTP clients on
  background threads.

Target: every tool method has a happy-path and an error-path unit test;
bridge infrastructure maintains ≥ 80% line coverage.

---

## Before committing a change

Per `CLAUDE.md`, after any Unity-side story:

1. Unity console is clean — no errors, no warnings.
2. The full EditMode suite passes (plus the Regression category against a
   running bridge for tool changes).
3. BMad code review before commit.

For npm-package changes, `npm test` must pass in the changed package.
