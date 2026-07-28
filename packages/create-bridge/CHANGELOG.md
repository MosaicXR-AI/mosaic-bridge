# Changelog — @mosaicxr-ai/create-bridge

All notable changes to this package will be documented in this file.

## [1.0.0-beta.9] — 2026-07-26

### Added

- **`--update` / `-u` — upgrade an already-installed bridge.** Unity resolves a UPM
  git dependency exactly once and records the commit in
  `Packages/packages-lock.json`, then reuses it indefinitely. Combined with the
  installer previously skipping any dependency that was already present, this meant
  **there was no supported way to move an existing project to a newer bridge** — users
  stayed pinned to whatever commit they first resolved, with nothing in the docs to
  explain why. `--update` now rewrites the manifest entry and clears that lock entry
  (leaving every other locked package untouched), so Unity re-resolves on next open.
- **`--ref <commit|tag|branch>`** — pins the dependency by appending `#ref`, for when
  you need to know exactly which commit a machine is running rather than "whatever
  `main` was at resolve time". Implies `--update`, and replaces any existing `#ref`
  rather than stacking fragments.

- **Requirements are now enforced at setup, not discovered in Unity.** The installer
  read the project's Unity version only to print it, so it would report success against
  an editor the bridge cannot compile on — the user found out from a wall of CS0619
  errors after Unity reimported the package. `create-bridge` now classifies the version
  against the documented support matrix and refuses `error`-level editors (below
  6000.3, and 6.6 betas where the EntityId layout changed), warns on 6000.4 and
  prereleases, and explains what to do in each case. `--ignore-unity-version` overrides.
  Node is checked against the `engines` floor before the project is touched at all.

### Changed

- Documented commands are now **single-line**, so they work unchanged in `cmd`,
  PowerShell, and bash. The previous examples used `\` continuations, which fail in
  `cmd` and forced Windows users into Git Bash for no reason.
- Without `--update`, the "already present" message now names the flag to use, instead
  of leaving users to guess why nothing happened.

## [1.0.0-beta.8] — 2026-07-12

### Fixed

- **Incomplete published tarball** — the `files` allowlist excluded `plugin/agents`,
  `plugin/commands`, and the `.claude-plugin/plugin.json` manifest, so the Zara/Ray/Max
  specialist agents and slash commands never shipped to users. The full `plugin/**` tree
  is now published.
- **Workflows never installed** — `plugin/workflows` shipped in the package but the
  installer only copied `plugin/skills`. Setup now also copies workflows (preflight,
  scene-plan, shader-guide, session-handoff) into the project's `.claude/workflows`.
- **`--version` reported the wrong number** — the CLI hardcoded `1.0.0-beta.4`; it now
  reads the real version from `package.json` at runtime.

### Added

- **Test harness** — `vitest` suite for the installer (tarball contents, version, and an
  end-to-end skills+workflows install run).

---

## [1.0.0-beta.7] — 2026-05-05

### Changed

- **Multi-Angle Screenshots section** in the LLM instruction templates now lists
  the exact tool sequence and standard rotation values:
  - 7-step tool table: \`selection/set\` → \`selection/focus-scene-view\` →
    \`sceneview/info\` → (optional \`gameobject/set-active\` to hide blockers) →
    \`sceneview/set-camera\` → \`camera/screenshot-scene\` → restore.
  - Standard 5-angle rotation table reusing the \`Pivot\` + \`Size\` from
    \`sceneview/info\`: front \`[20,0,0]\`, right \`[20,90,0]\`, back \`[20,180,0]\`,
    left \`[20,270,0]\`, 3/4 elevated \`[35,45,0]\`, top-down \`[89,0,0]\`.
  - Worked example showing the full call sequence for a hero object with one
    obstructing wall hidden + restored.
  - Notes on \`selection/focus-scene-view\` taking no params (operates on the
    current selection — always \`selection/set\` first), and the difference
    between \`camera/screenshot-scene\` (SceneView) vs \`camera/screenshot-camera\`
    (in-scene Camera component) vs \`camera/screenshot-game\` (GameView).

---

## [1.0.0-beta.6] — 2026-05-05

### Added

- **Three new behavioral rule sections** in the LLM instruction templates
  (\`CLAUDE.md\` / \`GEMINI.md\` / \`AGENTS.md\` / \`.cursor/rules/mosaic-bridge.mdc\`):

  - **Prefab-First Object Creation** — never create the same visual object twice;
    build once → \`prefab/create\` → \`asset/instantiate-prefab\` for every other
    placement. Use \`prefab/create-variant\` for variations.

  - **Visual Verification — Multi-Angle Screenshots** — after creating any
    visual object, frame it with \`selection/focus-scene-view\`, loop through 4–5
    viewpoints with \`sceneview/set-camera\`, and save \`camera/screenshot-scene\`
    output to \`Assets/_screenshots/{ObjectName}/{angle}.png\`. Hide blockers
    via \`gameobject/set-active\` before capture, re-enable after. Tool-success
    is not visual correctness.

  - **Input System Selection** — always use \`com.unity.inputsystem\` (PlayerInput,
    InputAction assets, \`OnMove(InputValue)\` callbacks) unless the user
    explicitly asks for legacy \`UnityEngine.Input\`. Cross-references the new
    \`InputSystem\` field from \`project/preflight\`.

### Changed

- **Session Start Protocol** updated to document the new \`project/preflight\`
  fields: \`GraphicsPipelineAsset\` (\`m_CustomRenderPipeline\`),
  \`QualityPipelineAsset\` (Quality → Rendering override), \`PipelineMismatch\`,
  \`InputSystem\`, and \`InputSystemPackageInstalled\`. Mismatch handling rules
  added: when the Quality override differs from the Graphics default, the
  Quality asset wins for the active quality level.

---

## [1.0.0-beta.5] — 2026-04-29

### Added

- **Cross-LLM agent distribution** — following the bmad-method convention, the installer
  now writes three specialist skill agents (Zara — Project Guide, Ray — Shader Expert,
  Max — Scene Builder) to two locations in the Unity project:
  - `.claude/skills/` — Claude Code slash commands (`/mosaic-guide`, `/mosaic-shader`, `/mosaic-scene`)
  - `.agents/skills/` — universal format for Cursor (`@mosaic-guide`), Codex (`$mosaic-guide`),
    Gemini (natural language `@file`), Windsurf, OpenCode, and GitHub Copilot

- **`GEMINI.md`** — Gemini CLI-specific instruction file written to the Unity project root.
  Includes agent activation table, scene-building protocol, spatial coherence contract, and
  render pipeline quick reference.

- **`AGENTS.md`** — OpenAI Codex-specific instruction file written to the Unity project root.

- **`.cursor/rules/mosaic-bridge.mdc`** — Cursor-native rules file (`alwaysApply: true`)
  written to `.cursor/rules/` in the Unity project.

---

## [1.0.0-beta.4] — 2026-04-22

### Added

- **OpenAI Codex CLI** added as a supported MCP client — the installer now detects and
  configures Codex CLI alongside Claude Code, Claude Desktop, Cursor, and Gemini CLI.

### Fixed

- **Windows MCP launch** — `cmd /c` wrapper added to the `mosaic-mcp` invocation written
  into client config files on Windows, matching the fix applied to the server package.

---

## [1.0.0-beta.3] — 2026-04-19

### Fixed

- Minor installer reliability improvements.

---

## [1.0.0-beta.1] — 2026-04-19

Initial release. Interactive CLI installer (`npx @mosaicxr-ai/create-bridge`) that sets up
the Mosaic Bridge Unity package and auto-configures MCP for supported AI clients.
