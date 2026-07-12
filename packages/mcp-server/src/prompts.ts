/**
 * MCP prompts — the Mosaic Bridge workflow rules, exposed over the protocol so
 * every client receives them, not only clients whose installer wrote a
 * CLAUDE.md/GEMINI.md/AGENTS.md instruction file.
 *
 * The text below is the canonical protocol content, kept in lockstep with the
 * shared `BASE_INSTRUCTIONS` block in `create-bridge/src/templates.js`. If you
 * edit one, edit the other.
 */

export interface McpPromptDef {
  name: string;
  description: string;
  arguments?: { name: string; description: string; required: boolean }[];
  build(args: Record<string, string>): string;
}

const SCENE_INTERVIEW = `## Scene Building — Interview Protocol

When asked to "build a scene", "create an environment", or describe any vague place/mood:

STOP. Do not call any Mosaic tools. Run the Scene Interview first.

Ask all four Tier 1 questions in a single message:

1. Scene type? — game level / playable · cinematic · archviz · prototype
2. Geographic or thematic reference? — be specific: "Wadi Rum Jordan", "Pacific Northwest forest", "dystopian 2080 Tokyo". Generic = generic output.
3. Scale? — < 100m · 100m–1km · 1–10km · 10km+
4. Player perspective? — first person · third person · drone / flight · top-down · no player (cinematic only)

After the interview, generate a ScenePlan summary and wait for confirmation before executing any tools.

## Spatial Coherence Contract

Every placed object Y must = terrain.SampleHeight(x, z) + small_offset. Never use Y=0 as a placement
coordinate unless the scene is a flat indoor space. Call terrain/sample-height before every
gameobject/create or prefab/instantiate.

## Execution Pipeline Order

Always build in this order (skipping creates visual artifacts):
1. Terrain — create, sculpt major features, secondary detail
2. Water — if applicable; sets the shoreline Y reference
3. Terrain textures — layer setup + splatmap painting
4. Sky + Lighting — directional light, skybox, ambient
5. Large structures — buildings, rock formations (use terrain/sample-height for Y)
6. Vegetation — trees (terrain system) then grass then small details
7. Post-processing — fog, bloom, color grade (last pass)
8. Camera / player controller — calibrated to final scene scale`;

const PREFLIGHT = `## Session Start Protocol

Always call project/preflight at the start of each session:
  { "tool": "project/preflight" }

The result includes:
- RenderPipeline — the active pipeline (URP / HDRP / BuiltIn) materials resolve against
- ColorProperty — _BaseColor (URP/HDRP) or _Color (BuiltIn)
- GraphicsPipelineAsset — project default (Project Settings → Graphics)
- QualityPipelineAsset — per-quality-level override (Project Settings → Quality → Rendering)
- PipelineMismatch — true when the Quality override differs from the Graphics default. If true, the
  Quality asset wins for the active quality level. Read both before creating materials, or materials
  may render magenta on platforms with different quality presets.
- InputSystem — Legacy / New / Both (active input handling)
- InputSystemPackageInstalled — whether com.unity.inputsystem is in the manifest

Never assume the pipeline — always verify it first.

## Render Pipeline Quick Reference

| Pipeline | Default Shader                   | Color Property |
|----------|----------------------------------|----------------|
| URP      | Universal Render Pipeline/Lit    | _BaseColor     |
| HDRP     | HDRP/Lit                         | _BaseColor     |
| BuiltIn  | Standard                         | _Color         |

Magenta material = wrong shader for pipeline. Use material/create without ShaderName to auto-detect.`;

const SHADER_GUIDE = `## ShaderGraph Serialization Rules (Unity 14.x+)

These rules are enforced automatically by the tools — no manual workarounds needed:

| Rule                 | Detail                                                             |
|----------------------|--------------------------------------------------------------------|
| UUID GUIDs           | m_ObjectId format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx             |
| VoronoiNode          | Requires SGVersion=1 + m_HashType=0                                |
| Texture slots        | StageCapability=2 (fragment-only)                                  |
| CustomFunction       | SGVersion=1 + m_SourceType=1 for inline HLSL                       |
| Block-based contexts | Unity 14.x+ uses m_VertexContext / m_FragmentContext — no PBRMaster |

Workflow: shadergraph/create → shadergraph/add-node → shadergraph/connect.
Do not fall back to raw HLSL .shader files when a ShaderGraph will do.`;

const SESSION_HANDOFF = `## Session Handoff

At the end of every session (or when context is running low), write a handoff:
- Path: docs/Sessions/{username}/SESSION_NOTES.md
- Include: pipeline, color property, assets created, errors encountered, remaining work
- Recovery prompt: "Load session notes from docs/Sessions/{username}/SESSION_NOTES.md and continue
  where we left off."`;

export const MOSAIC_PROMPTS: McpPromptDef[] = [
  {
    name: 'preflight',
    description: 'Run project/preflight and interpret the render pipeline, packages, and input system at session start.',
    build: () => PREFLIGHT,
  },
  {
    name: 'scene-interview',
    description: 'The interview protocol to run before building any scene — questions, ScenePlan, spatial-coherence contract, and build order.',
    build: () => SCENE_INTERVIEW,
  },
  {
    name: 'session-handoff',
    description: 'Write docs/Sessions/{username}/SESSION_NOTES.md before ending a session so the next one can resume.',
    build: () => SESSION_HANDOFF,
  },
  {
    name: 'shader-guide',
    description: 'ShaderGraph serialization + node-wiring rules for Unity 14.x+.',
    build: () => SHADER_GUIDE,
  },
];
