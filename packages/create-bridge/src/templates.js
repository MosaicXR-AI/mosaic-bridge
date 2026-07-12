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
The result includes:
- **RenderPipeline** — the active pipeline (URP / HDRP / BuiltIn) materials actually resolve against
- **ColorProperty** — \`_BaseColor\` (URP/HDRP) or \`_Color\` (BuiltIn)
- **GraphicsPipelineAsset** — project default (\`Edit → Project Settings → Graphics\`, serialized as \`m_CustomRenderPipeline\`)
- **QualityPipelineAsset** — per-quality-level override (\`Edit → Project Settings → Quality → Rendering → Render Pipeline Asset\`)
- **PipelineMismatch** — \`true\` when the Quality override differs from the Graphics default. **If true, the Quality asset wins for the active quality level.** Always read both before creating materials, otherwise materials may render magenta on platforms with different quality presets.
- **InputSystem** — \`Legacy\` / \`New\` / \`Both\` (active input handling)
- **InputSystemPackageInstalled** — whether \`com.unity.inputsystem\` is in the manifest

Never assume the pipeline — always verify it first.

## Render Pipeline Quick Reference

| Pipeline | Default Shader | Color Property |
|----------|---------------|---------------|
| URP | Universal Render Pipeline/Lit | _BaseColor |
| HDRP | HDRP/Lit | _BaseColor |
| BuiltIn | Standard | _Color |

Magenta material = wrong shader for pipeline. Use \`material/create\` without ShaderName to auto-detect.

## Prefab-First Object Creation

**Never create the same visual object more than once. Build a prefab, then instantiate.**

When the scene needs more than one of the same thing (trees, rocks, enemies, props, modular tiles, particle effects), the workflow is:

1. Build the object **once** at the origin or off-scene.
2. Save it as a prefab via \`prefab/create\` to \`Assets/Prefabs/{Category}/{Name}.prefab\`.
3. For every additional placement, use \`asset/instantiate-prefab\` (or \`prefab/instantiate\`) — never re-build the geometry.
4. If you need a variation, use \`prefab/create-variant\` instead of duplicating-and-modifying.

The cost difference is real: 50 individually-built trees with the same materials/meshes will fragment your scene file, prevent batching, and make every later edit a 50-step operation. 50 instances of one prefab = one source of truth.

## Visual Verification — Multi-Angle Screenshots

**After creating any visual object (model, prefab, particle effect, ShaderGraph applied, lit scene), capture screenshots from at least 4 angles before declaring the task done.**

### Tools used

| Step | Tool | Purpose |
|------|------|---------|
| 1 | \`selection/set\` | Select the target by \`Name\` / \`Names\` / \`InstanceIds\` / \`AssetPaths\` |
| 2 | \`selection/focus-scene-view\` | Frame + center the SceneView orbit on the selection (no params) |
| 3 | \`sceneview/info\` | Read the resulting \`Pivot\` and \`Size\` to use as the orbit anchor |
| 4 | \`gameobject/set-active\` | (Optional) hide blockers — re-enable in step 7 |
| 5 | \`sceneview/set-camera\` | Move the SceneView camera. Reuse the same \`Pivot\` + \`Size\`; only change \`Rotation\` |
| 6 | \`camera/screenshot-scene\` | Capture: \`SavePath\`, \`Width\` (default 1920), \`Height\` (default 1080), \`Format\` (\`png\`/\`jpeg\`) |
| 7 | \`gameobject/set-active\` | Restore anything hidden in step 4 |

### Standard 5-angle set

Reuse the \`Pivot\` and \`Size\` from \`sceneview/info\`. Only \`Rotation\` (euler degrees) changes:

| Angle | Rotation \`[x, y, z]\` |
|-------|----------------------|
| Front (looking -Z) | \`[20, 0, 0]\` |
| Right | \`[20, 90, 0]\` |
| Back | \`[20, 180, 0]\` |
| Left | \`[20, 270, 0]\` |
| 3/4 elevated hero | \`[35, 45, 0]\` |
| (Tall objects) Top-down | \`[89, 0, 0]\` |

The \`x\` component is the elevation. \`20°\` keeps the floor visible without clipping; raise it for tall scenes. The \`y\` component is the orbit angle around the pivot.

### Example sequence

\`\`\`
selection/set            → { "Name": "MyHero" }
selection/focus-scene-view → {}
sceneview/info           → returns { Pivot, Size, ... }
                           // capture Pivot and Size — reuse below

// Optional: hide an obstructing wall
gameobject/set-active    → { "Name": "BlockingWall", "Active": false }

// Front
sceneview/set-camera     → { "Pivot": [px,py,pz], "Size": s, "Rotation": [20, 0, 0] }
camera/screenshot-scene  → { "SavePath": "Assets/_screenshots/MyHero/front.png" }

// Right
sceneview/set-camera     → { "Pivot": [px,py,pz], "Size": s, "Rotation": [20, 90, 0] }
camera/screenshot-scene  → { "SavePath": "Assets/_screenshots/MyHero/right.png" }

// Back, Left, 3/4 — same pattern with rotations [20,180,0], [20,270,0], [35,45,0]

// Restore
gameobject/set-active    → { "Name": "BlockingWall", "Active": true }
\`\`\`

### Notes

- \`selection/focus-scene-view\` takes **no parameters** — it operates on whatever \`selection/set\` selected. Always select first.
- For an in-scene \`Camera\` component (not the SceneView), use \`camera/screenshot-camera\`. \`camera/screenshot-game\` captures the GameView — only useful when a Camera is actively rendering it.
- Inspect the screenshots before reporting success. Magenta materials, missing meshes, scale errors, and z-fighting are obvious in renders and invisible in JSON.

Type-checking and tool-success ≠ visual correctness. If you can't see it, you haven't verified it.

## Input System Selection

**Always use the new Input System package (\`com.unity.inputsystem\`) when writing input-handling code, unless the user explicitly asks for the legacy \`UnityEngine.Input\` API.**

Reasoning:
- The new Input System has been the recommended API since Unity 2019.1 and ships as a verified package on Unity 2021 LTS+.
- New Unity templates default to it. Mixing legacy + new on the same project triggers warnings and breaks rebinding flows.
- Check \`InputSystem\` from \`project/preflight\`: if \`Legacy\` only, suggest enabling the new system in Player Settings → Active Input Handling and installing \`com.unity.inputsystem\` before writing input code.

Use \`PlayerInput\` components, \`InputAction\` assets, and the \`OnMove(InputValue)\` callback pattern — not \`Input.GetAxis\` / \`Input.GetKeyDown\`.

## Tool Usage Rules

- **Render pipeline:** Always call \`project/preflight\` before material or shader work; check both Graphics + Quality pipeline assets.
- **Repeated objects:** Build once → \`prefab/create\` → \`asset/instantiate-prefab\` for every other placement.
- **Visual verification:** \`selection/focus-scene-view\` + \`sceneview/set-camera\` + \`camera/screenshot-scene\` from 4–5 angles after any visual change.
- **Input code:** Use the new Input System package by default; only fall back to legacy \`UnityEngine.Input\` on explicit user request.
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

## Connection Troubleshooting

If tool calls fail or the bridge seems unreachable, run the doctor from a terminal:

\`\`\`bash
npx @mosaicxr-ai/mcp-server doctor --project-path <this Unity project>
\`\`\`

It checks the discovery file, live editor, port, HMAC handshake, and clock skew, and prints
exactly which link is broken. The usual cause: the Unity Editor isn't open, or it's still compiling.
With one Editor open you can omit \`--project-path\` — it auto-detects.

## Workflow Prompts (MCP)

This bridge also exposes its protocols as MCP prompts — \`preflight\`, \`scene-interview\`,
\`session-handoff\`, and \`shader-guide\`. Invoke them from your client's prompt menu (in Claude Code,
the prompt picker) when you want the full protocol text on demand.

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
