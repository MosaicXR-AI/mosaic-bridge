# Changelog — @mosaicxr-ai/mcp-server

All notable changes to this package will be documented in this file.

## [1.0.0-beta.8] — 2026-07-28

### Security

- **Cleared every advisory reaching users.** `npm audit --omit=dev` reported 5
  vulnerabilities in the published dependency tree (2 high, 3 moderate), all arriving
  transitively through the package's single production dependency,
  `@modelcontextprotocol/sdk`:
  - `ip-address` — three separate SSRF / trust-boundary bypasses (leading-zero octets
    decoded as decimal, CIDR suffixes suppressing special-use classification, and
    misclassified IPv4-mapped/NAT64 addresses).
  - `fast-uri` — host confusion via literal backslash authority and failed IDN
    canonicalization.
  - `@hono/node-server` — path traversal in `serve-static` on Windows via an encoded
    backslash.

  Shipping vulnerabilities are now **0**. The bridge listener binds loopback only and is
  HMAC-authenticated, which limited real exposure, but these sat in the tree users
  install. Build is clean and all 60 tests pass on the updated tree.

### Fixed

- **A 401 no longer always reports "domain reload".** Authentication failures were
  labelled as an in-progress Unity domain reload regardless of cause, so a clock-skew or
  bad-secret failure sent users chasing the wrong problem. The real reason is now
  surfaced.

## [1.0.0-beta.7] — 2026-07-12

### Added

- **`doctor` command** — `mosaic-mcp doctor [--project-path <path>]` runs connection
  diagnostics and prints a copy-pasteable report: discovery file (found/parsed/signed),
  live Unity Editor detection, TCP port reachability, a signed health + HMAC handshake,
  and clock-skew detection. Exits non-zero when any check fails. Replaces the opaque
  "Connection closed" failure that gave users nothing to act on.
- **MCP prompts capability** — the Mosaic Bridge workflow rules are now exposed over the
  protocol via `prompts/list` and `prompts/get`: `preflight`, `scene-interview`,
  `session-handoff`, and `shader-guide`. Every MCP client receives them, not just clients
  whose installer wrote a `CLAUDE.md`/`GEMINI.md`/`AGENTS.md`.

### Fixed

- **Version reporting** — `--version` now prints the real package version (was a
  placeholder string), and the MCP handshake advertises the real version instead of a
  hardcoded `1.0.0`. Both read from `package.json` at runtime.

---

## [1.0.0-beta.6] — 2026-04-29

### Added

- **Unity asset MCP resources** — six new resource categories exposed at
  `mosaic://unity/assets/{prefabs,materials,textures,scenes,scripts,shadergraphs}`.
  In Claude Code, use `@Unity Prefabs`, `@Unity Materials`, etc. to browse your project
  assets directly in the prompt. Each resource calls `asset/list` on the bridge and
  returns asset paths ready for use in tool calls.

---

## [1.0.0-beta.5] — 2026-04-22

### Fixed

- **Tool dispatcher** — resolved schema-refresh race that caused `meta/advanced_tool`
  to return "Tool not found" for valid tool names after a registry reload.

- **Opaque tool errors in `mesh/*`, `simulation/*`, `procgen/*`** — tools that create
  assets now use `AssetDatabaseHelper.EnsureFolder` before `AssetDatabase.CreateAsset`,
  fixing silent `{"suggestedFix":null}` failures caused by unregistered output directories.

- **`gameobject/set_active`** — uses `Resources.FindObjectsOfTypeAll` so inactive
  GameObjects can be found and activated (previous `GameObject.Find` skipped them).

- **`shadergraph/list`** — switched to filesystem `.shadergraph` search; the previous
  `t:Shader` type-indexed search returned zero results when files existed but weren't
  fully imported.

- **`prefab/info`** — wrapped `PrefabUtility` override APIs in try-catch to handle
  both prefab asset roots and scene instances correctly.

---

## [1.0.0-beta.4] — 2026-04-20

### Fixed

- **Windows path hashing** — normalized Win32 backslash paths to forward slashes before
  hashing so project IDs are stable across Windows path formats.

- **Windows MCP launch** — wrapped `npx mosaic-mcp` invocation in `cmd /c` so MCP
  clients can spawn the stdio server on Windows without "requires 'cmd /c' wrapper" errors.

---

## [1.0.0-beta.3] — 2026-04-19

### Added

- **OpenAI Codex CLI** added as a supported MCP client in the auto-configurator.

---

## [1.0.0-beta.1] — 2026-04-19

Initial release. MCP stdio server that bridges AI clients to the Unity Editor via
Mosaic Bridge. Supports Claude Code, Claude Desktop, Cursor, Gemini CLI, and Codex CLI.
