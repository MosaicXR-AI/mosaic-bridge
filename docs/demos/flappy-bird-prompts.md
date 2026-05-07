# Flappy Bird in Unity — Built by Claude in 48 Minutes

> **Watch the build:** [youtube.com/watch?v=EIXiSQ0z1ZY](https://www.youtube.com/watch?v=EIXiSQ0z1ZY)
> **Repo:** [MosaicXR-AI/mosaic-bridge](https://github.com/MosaicXR-AI/mosaic-bridge)
> **Reproduce locally:** `npx @mosaicxr-ai/create-bridge`

This page documents how Claude (Sonnet 4.6) built a working Flappy Bird clone in Unity through Mosaic Bridge — start to finish, no manual code, no manual editor clicks. Every action is a Mosaic Bridge tool call.

## What this demo proves

- **AI can drive Unity end-to-end.** Not a code-completion assist — Claude composed the prompt, sequenced the calls, and adapted when something didn't work.
- **48 minutes is the actual elapsed time**, not edited footage. The on-screen clock in the video is real-time.
- **Reproducible.** Run the same starter prompt against your own Unity project and you'll get a working game. The exact moment-to-moment sequence will vary because Claude makes its own decisions, but the result converges.

## Environment

| | |
|---|---|
| **Unity version** | 6000.0 (Unity 6) |
| **Render pipeline** | URP |
| **Color property** | `_BaseColor` |
| **Input system** | New Input System (`com.unity.inputsystem`) |
| **Active scene** | Empty SampleScene |
| **Mosaic Bridge** | `1.0.0-beta.5` |
| **MCP server** | `@mosaicxr-ai/mcp-server@1.0.0-beta.6` |
| **Client** | Claude Code |

These were resolved automatically by `project/preflight` at the start of the session — Claude never assumes pipeline or input system, it always reads them first.

## The opening prompt

The whole build started from a single user message:

> *"Build me a Flappy Bird clone in Unity. Working game, with score, game over, and restart."*

Everything below is what Claude did with that prompt. No follow-up clarifications were given until the player reported runtime errors — Claude resolved those itself in the same session.

## Build phases

| # | Phase | Approx. video time | Primary tools |
|---|-------|-------------------|---------------|
| 1 | Preflight & scope | 00:00–00:30 | `project/preflight` |
| 2 | The bird (player) | 00:30–04:00 | `gameobject/create`, `material/create`, `physics/add-rigidbody`, `physics/add-collider`, `script/create` |
| 3 | Jump input (new Input System) | 04:00–08:00 | `script/create`, `script/edit` |
| 4 | Pipe prefab | 08:00–14:00 | `gameobject/create`, `physics/add-collider`, `material/create`, `prefab/create` |
| 5 | Pipe spawner | 14:00–22:00 | `script/create`, `gameobject/create`, `component/add` |
| 6 | Scrolling ground & background | 22:00–28:00 | `gameobject/create`, `material/create`, `script/create` |
| 7 | Score & UI | 28:00–35:00 | `ui/create-canvas`, `ui/create-text`, `script/create`, `physics/add-trigger` |
| 8 | Game over + restart | 35:00–42:00 | `script/edit`, `scene/load`, `ui/create-button` |
| 9 | Polish (camera, colors, scaling) | 42:00–48:00 | `material/set-property`, `gameobject/set-transform`, `camera/set-projection` |

## Notable moments

### Phase 2 — Why the bird is a sphere
Claude chose a primitive sphere with a colored URP material rather than searching for a bird model in the Asset Store. Reason logged in the session: "Faster iteration; matches the original Flappy Bird's abstract aesthetic." Materials use `_BaseColor` (URP) — not `_Color` — because `project/preflight` returned `RenderPipeline: URP` first.

### Phase 3 — New Input System
Per the workflow rules baked into `CLAUDE.md`, Claude generated input handling using `PlayerInput` + an `InputAction` asset, not `Input.GetKeyDown`. The script attaches an `OnJump(InputValue)` callback, not a polled `Update()` check.

### Phase 4 — Prefab-first pipe creation
The pipe was built **once** at the origin, saved via `prefab/create`, and every spawned pipe in Phase 5 is an `asset/instantiate-prefab` call. Zero geometry duplication. This is the "Prefab-First Object Creation" rule from the instruction template — followed automatically.

### Phase 5 — Pipe spawner script
The spawner C# class was generated, written to `Assets/Scripts/PipeSpawner.cs`, and attached to a new empty GameObject. The script uses `Random.Range` for vertical pipe gap position and a coroutine for spawn timing. No manual editing of the script in Unity.

### Phase 7 — Score detection
A trigger collider sits in the gap between top and bottom pipes. When the bird passes through, `OnTriggerEnter2D` increments a static score counter, which the UI Text component reads each frame.

### Phase 8 — One real bug, one real fix
The first runtime test crashed because `SceneManager.LoadScene` was called on a scene name that wasn't in the build settings. Claude added the scene to build settings via `build/add-scene-to-settings` and re-tested. This is in the video at ~38:00 — a real fix, not edited out.

## Full prompt transcript

A full prompt-by-prompt + tool-call log (extracted from the Claude Code session JSONL) will be appended to this page. For now, the YouTube video is the source of truth for the exact call sequence — every tool name appears as a lower-third overlay when it fires.

If you want the raw conversation export, [open an issue](https://github.com/MosaicXR-AI/mosaic-bridge/issues/new/choose) — happy to share the JSONL on request while I clean it up for general publication.

## Reproduce it yourself

```bash
# 1. Install
npx @mosaicxr-ai/create-bridge

# 2. Open your Unity project (any empty Unity 6 / Unity 2022 LTS project works)

# 3. In Claude Code (or any MCP client):
"Build me a Flappy Bird clone in Unity. Working game, with score, game over, and restart."
```

The exact sequence Claude takes will differ run-to-run — that's the whole point. The result converges.

## See also

- [README — See it work](../../README.md#see-it-work)
- [Mosaic Bridge tools list](../../README.md#tool-categories) — every tool used here is in the catalogue
- [Knowledge base](../../packages/com.mosaic.bridge/Editor/Knowledge/) — the data Claude consulted for material albedo, physics constants, etc.
