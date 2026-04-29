# Changelog — @mosaicxr-ai/create-bridge

All notable changes to this package will be documented in this file.

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
