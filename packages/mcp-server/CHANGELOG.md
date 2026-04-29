# Changelog — @mosaicxr-ai/mcp-server

All notable changes to this package will be documented in this file.

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
