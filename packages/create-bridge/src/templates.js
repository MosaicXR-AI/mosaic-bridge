/**
 * Shared instruction content for LLM-specific context files written into
 * the Unity project root during setup. Each LLM reads a different file name
 * but the core rules are the same.
 *
 * CLAUDE.md  → Claude Code + Claude Desktop
 * GEMINI.md  → Gemini CLI
 * AGENTS.md  → OpenAI Codex
 * .cursor/rules/mosaic-bridge.mdc → Cursor
 */

// ── Core rules (all LLMs) ─────────────────────────────────────────────────

export const BASE_INSTRUCTIONS = `
## Scene Building — Interview Protocol

When asked to "build a scene", "create an environment", or describe any vague place/mood:

**STOP. Do not call any Mosaic tools. Run the Scene Interview first.**

Ask all four Tier 1 questions in a single message:

1. **Scene type?** — game level / playable · cinematic · archviz · prototype
2. **Geographic or thematic reference?** — be specific: "Wadi Rum Jordan", "Pacific Northwest forest", "dystopian 2080 Tokyo". Generic = generic output.
3. **Scale?** — < 100m · 100m–1km · 1–10km · 10km+
4. **Player perspective?** — first person · third person · drone / flight · top-down · no player (cinematic only)

After interview, generate a **ScenePlan summary** and wait for confirmation before executing any tools.

---

## Spatial Coherence Contract

**Every placed object Y must = terrain.SampleHeight(x, z) + small_offset.**

Never use Y=0 as a placement coordinate unless the scene is a flat indoor space.

Use \`terrain/sample-height\` before every \`gameobject/create\` or \`prefab/instantiate\` call.

---

## Execution Pipeline Order

Always build in this order (skipping creates visual artifacts):

1. **Terrain** — create, sculpt major features, secondary detail
2. **Water** — if applicable; sets the shoreline Y reference
3. **Terrain textures** — layer setup + splatmap painting
4. **Sky + Lighting** — directional light, skybox, ambient
5. **Large structures** — buildings, rock formations (use terrain/sample-height for Y)
6. **Vegetation** — trees (terrain system) then grass then small details
7. **Post-processing** — fog, bloom, color grade (last pass)
8. **Camera / player controller** — calibrated to final scene scale

---

## Session Start Protocol

Always call \`project/preflight\` at the start of each session:
\`\`\`json
{ "tool": "project/preflight" }
\`\`\`
This returns the **RenderPipeline** (URP/HDRP/BuiltIn) and **ColorProperty** (_BaseColor or _Color).
Never assume the pipeline — always verify it first.

## Render Pipeline Quick Reference

| Pipeline | Default Shader | Color Property |
|----------|---------------|---------------|
| URP | Universal Render Pipeline/Lit | _BaseColor |
| HDRP | HDRP/Lit | _BaseColor |
| BuiltIn | Standard | _Color |

Magenta material = wrong shader for pipeline. Use \`material/create\` without ShaderName to auto-detect.

## Tool Usage Rules

- **Render pipeline:** Always call \`project/preflight\` before material or shader work.
- **Terrain trees:** Prefab root must have \`MeshRenderer\`, \`LODGroup\`, or \`BillboardRenderer\`.
- **Material keywords:** Use \`keyword\` ValueType on \`material/set-property\` for \`_EMISSION\`, \`_NORMALMAP\`, \`_ALPHATEST_ON\`.
- **ShaderGraph nodes:** Use \`shadergraph/add-node\` + \`shadergraph/connect\` — do not fall back to raw HLSL .shader files.
- **HDRI skybox:** Use \`texture/set-import-settings\` with \`TextureShape=Cube\` to convert equirectangular HDRI to cubemap.

## ShaderGraph Serialization Rules (Unity 14.x+)

These rules are now enforced automatically by the tools — no manual workarounds needed:

| Rule | Detail |
|------|--------|
| UUID GUIDs | m_ObjectId format: \`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx\` |
| VoronoiNode | Requires SGVersion=1 + m_HashType=0 |
| Texture slots | StageCapability=2 (fragment-only) |
| CustomFunction | SGVersion=1 + m_SourceType=1 for inline HLSL |
| Block-based contexts | Unity 14.x+ uses m_VertexContext / m_FragmentContext — no PBRMasterNode |

Workflow: \`shadergraph/create\` → \`shadergraph/add-node\` → \`shadergraph/connect\`

## Session Handoff

At the end of every session (or when context is running low), write a handoff:
- Path: \`docs/Sessions/{username}/SESSION_NOTES.md\`
- Include: pipeline, color property, assets created, errors encountered, remaining work
- Recovery prompt: "Load session notes from docs/Sessions/{username}/SESSION_NOTES.md and continue where we left off."

## When in Doubt

Ask a clarifying question rather than guessing. A 2-minute interview prevents a 20-minute rebuild.
`;

// ── Agent activation table (per LLM) ─────────────────────────────────────

const CLAUDE_AGENTS_SECTION = `
## Mosaic Bridge Skills (Claude Code)

Three specialist agents are installed as project skills:

| Agent | Invoke | Best for |
|-------|--------|----------|
| Zara — Project Guide | \`/mosaic-guide\` | Session start, preflight, pipeline issues, session handoff |
| Ray — Shader Expert | \`/mosaic-shader\` | ShaderGraph creation, node wiring, shader debugging |
| Max — Scene Builder | \`/mosaic-scene\` | Full scene construction, particles, physics, audio, UI |

---`;

const GEMINI_AGENTS_SECTION = `
## Mosaic Bridge Agents (Gemini CLI)

Three specialist agents are available in \`.agents/skills/\`:

| Agent | How to activate | Best for |
|-------|----------------|----------|
| Zara — Project Guide | Say: *"Load mosaic-guide"* or \`@.agents/skills/mosaic-guide/SKILL.md\` | Session start, preflight, pipeline issues |
| Ray — Shader Expert | Say: *"Load mosaic-shader"* or \`@.agents/skills/mosaic-shader/SKILL.md\` | ShaderGraph, node wiring, shader debugging |
| Max — Scene Builder | Say: *"Load mosaic-scene"* or \`@.agents/skills/mosaic-scene/SKILL.md\` | Full scene construction, particles, physics |

---`;

const CURSOR_AGENTS_SECTION = `
## Mosaic Bridge Agents (Cursor)

Three specialist agents are installed in \`.agents/skills/\`:

| Agent | Invoke | Best for |
|-------|--------|----------|
| Zara — Project Guide | \`@mosaic-guide\` | Session start, preflight, pipeline issues |
| Ray — Shader Expert | \`@mosaic-shader\` | ShaderGraph, node wiring, shader debugging |
| Max — Scene Builder | \`@mosaic-scene\` | Full scene construction, particles, physics |

---`;

const CODEX_AGENTS_SECTION = `
## Mosaic Bridge Skills (OpenAI Codex)

Three specialist agents are installed in \`.agents/skills/\`:

| Agent | Invoke | Best for |
|-------|--------|----------|
| Zara — Project Guide | \`$mosaic-guide\` | Session start, preflight, pipeline issues |
| Ray — Shader Expert | \`$mosaic-shader\` | ShaderGraph, node wiring, shader debugging |
| Max — Scene Builder | \`$mosaic-scene\` | Full scene construction, particles, physics |

---`;

// ── Claude Code — CLAUDE.md ───────────────────────────────────────────────

export const CLAUDE_MD_CONTENT = `# Unity Project — AI Assistant Instructions

This project uses **Mosaic Bridge MCP** to drive the Unity Editor via tool calls.
${CLAUDE_AGENTS_SECTION}
${BASE_INSTRUCTIONS}`;

// ── Gemini CLI — GEMINI.md ────────────────────────────────────────────────

export const GEMINI_MD_CONTENT = `# Unity Project — AI Assistant Instructions (Gemini CLI)

This project uses **Mosaic Bridge MCP** to drive the Unity Editor via tool calls.
${GEMINI_AGENTS_SECTION}
${BASE_INSTRUCTIONS}`;

// ── OpenAI Codex — AGENTS.md ─────────────────────────────────────────────

export const AGENTS_MD_CONTENT = `# Unity Project — AI Assistant Instructions

This project uses **Mosaic Bridge MCP** to drive the Unity Editor via tool calls.
${CODEX_AGENTS_SECTION}
${BASE_INSTRUCTIONS}`;

// ── Cursor — .cursor/rules/mosaic-bridge.mdc ─────────────────────────────

export const CURSOR_RULES_CONTENT = `---
description: Mosaic Bridge Unity MCP rules — scene building, shader creation, render pipeline
alwaysApply: true
---

# Mosaic Bridge — Unity MCP Rules

This project uses **Mosaic Bridge MCP** to drive the Unity Editor via tool calls.
${CURSOR_AGENTS_SECTION}
${BASE_INSTRUCTIONS}`;
