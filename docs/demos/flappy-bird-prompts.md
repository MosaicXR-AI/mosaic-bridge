# Flappy Bird in Unity — Built by Claude in 48 Minutes

> **Watch the build:** [youtube.com/watch?v=EIXiSQ0z1ZY](https://www.youtube.com/watch?v=EIXiSQ0z1ZY)
> **Repo:** [MosaicXR-AI/mosaic-bridge](https://github.com/MosaicXR-AI/mosaic-bridge)
> **Reproduce locally:** `npx @mosaicxr-ai/create-bridge`

This page is the prompt log for the Flappy Bird demo. Claude (Sonnet 4.6) built a complete, polished Flappy Bird clone in Unity through Mosaic Bridge — 48 minutes, start to finish, no manual code, no manual editor clicks. Every action is a Mosaic Bridge tool call.

## What this demo proves

- **AI can drive Unity end-to-end** — including package management, asset creation, ScriptableObjects, Input System action maps, URP 2D lighting, post-processing, Shader Graph, particle systems, procedural audio, and a WebGL build — from a single user prompt.
- **48 minutes is the actual elapsed time**, not edited footage. The clock in the video is real-time.
- **Reproducible.** The full prompt is below. Paste it into your own Claude Code session, and you'll get a working game. The exact sequence Claude takes will vary because it composes its own approach — the result converges.
- **The "Multi-Angle Screenshots" workflow rule from `CLAUDE.md` was followed automatically.** Claude captured `mosaic_camera_screenshot` after every milestone (main menu, mid-gameplay, game over) without being reminded — exactly as the instruction template specifies.

## Environment

| | |
|---|---|
| **Unity version** | 2022.3 LTS (per opening prompt — "or newer, confirm via MCP") |
| **Render pipeline** | Universal Render Pipeline (URP) — **2D Renderer** |
| **Color property** | `_BaseColor` |
| **Aspect ratio** | 9:16 portrait, Pixel Perfect Camera |
| **Input system** | New Input System (`com.unity.inputsystem`) — legacy disabled |
| **Mosaic Bridge (Unity package)** | `1.0.0-beta.5` |
| **MCP server** | `@mosaicxr-ai/mcp-server@1.0.0-beta.6` |
| **Client** | Claude Code (Sonnet 4.6) |

These were confirmed via `project/preflight` at the start of the session — Claude never assumes pipeline or input system, it always reads them first.

## The opening prompt (verbatim)

This is the entire user message that started the build. No follow-up clarifications were given until runtime issues appeared, and Claude resolved those in the same session.

````markdown
Build a complete, polished Flappy Bird clone as a Unity project. Use the
MosaicXR MCP bridge (`mcp-mosaic-bridge`) to scaffold the project, drive the
editor, and verify the build visually via `mosaic_camera_screenshot` after
each major milestone. Deliver a runnable Unity project that opens cleanly in
the Unity Editor and builds to WebGL/Standalone without errors.

# Project setup (via Mosaic MCP)
- Unity version: 2022.3 LTS or newer (use whatever the Mosaic bridge currently
  exposes — confirm via MCP before scaffolding).
- Render pipeline: Universal Render Pipeline (URP) 2D — required for 2D lights,
  post-processing, and shader graph.
- Project template: 2D (URP). Aspect ratio locked to 9:16 portrait
  (mobile-friendly), with a Pixel Perfect Camera component for crisp scaling.
- Use Mosaic MCP to: create the project, add packages, create scenes, create
  prefabs, attach scripts/components, and capture screenshots after each step
  for verification.

# Required Unity packages (all free, official)
- Input System (com.unity.inputsystem) — REQUIRED. Disable legacy Input Manager.
- Cinemachine — smooth camera follow / screen shake via Impulse Source.
- TextMeshPro — all UI text.
- Universal RP + 2D Renderer — lighting and post-FX.
- Post Processing (via URP Volume) — bloom, vignette, chromatic aberration,
  color grading.
- Shader Graph — for the parallax sky gradient and pipe highlight shader.
- 2D Animation + 2D Sprite — bird wing-flap rig.
- 2D Sprite Shape — for organic ground/cloud silhouettes.
- DOTween (HOTween v2) — score pulse, fade transitions, game-over UI animations.
- Burst + Mathematics — cheap particle math if needed.

# Project structure
Assets/
  Art/            (sprites generated procedurally or simple placeholders)
  Audio/          (procedurally generated AudioClips at runtime)
  Prefabs/        (Bird, Pipe, GroundTile, ParticleBurst, Cloud)
  Scenes/         (MainMenu, Game)
  Scripts/
    Core/         (GameManager, GameState enum, ScoreManager, AudioManager)
    Gameplay/     (BirdController, PipeSpawner, Pipe, ParallaxLayer, GroundScroller)
    Input/        (PlayerInputActions.inputactions + generated C# wrapper)
    UI/           (HUDController, MainMenuController, GameOverPanel)
    FX/           (CameraShake, FeatherBurst, ScorePopup, DayNightCycle)
  Settings/       (URP asset, Input Actions asset, Volume profile)

# Input System (NEW — not legacy)
- PlayerInputActions.inputactions with one Action Map "Gameplay":
  - Jump (Button): <Keyboard>/space, <Keyboard>/upArrow, <Mouse>/leftButton,
    <Touchscreen>/primaryTouch/tap, <Gamepad>/buttonSouth.
  - Pause (Button): <Keyboard>/escape, <Gamepad>/start.
- Generate C# wrapper class (Auto-Generate C# Class checkbox).
- BirdController consumes input via the generated class
  (_actions.Gameplay.Jump.performed += OnJump) — never via Input.GetKey.
- Enable/disable action maps based on game state.

# Game mechanics (deterministic, bug-free)
- Physics in FixedUpdate. Gravity ~-30, jump impulse sets velocity.y = +9.
- Bird: Rigidbody2D (Dynamic, gravity scale ~3) + CircleCollider2D
  (radius slightly smaller than sprite for forgiving hits).
- Pipes: pooled (object pool, no Instantiate/Destroy during play). Spawn every
  ~1.4s. Gap Y randomized within safe bounds; gap size ~3 units.
- Score: trigger collider on pipe center; OnTriggerEnter2D with "ScoreZone"
  tag. Each pipe scores once (boolean flag).
- Bird tilt: lerp Z rotation based on Rigidbody2D.velocity.y, clamped
  between -90° and +30°.
- Game states: Menu, Ready, Playing, Dying, GameOver. State machine in GameManager.
- High score persisted via PlayerPrefs.

# High-end graphics
- URP 2D Lights: global light + soft point light following the bird.
- Post-processing Volume (Global): Bloom (~0.6), Vignette (mild), Chromatic
  Aberration (kicks up on collision via DOTween), Color Adjustments for
  day/night tint.
- Parallax: 3 layers (far sky gradient via Shader Graph, mid clouds via
  Sprite Shape, near ground tiles).
- Day/night cycle: lerp global light color + Volume color-grading temperature
  over a ~30s loop.
- Bird animation: 3-frame flap via 2D Animation package, time-driven.
- Particles: feather burst (Particle System) on death; sparkle on score.
- Camera shake: Cinemachine Impulse Source fires on collision.
- Score UI: TextMeshPro with outlined font asset; DOTween scale-punch on increment.
- Pipe shader: Shader Graph adds a vertical highlight gradient + soft drop shadow.
- Transitions: DOTween fade for state changes.

# Audio (procedural via AudioClip.Create)
- AudioManager generates clips at runtime: jump (rising sine blip), score
  (two-note ding), hit (noise burst + low thud), die (descending tone).
- Mute toggle button in HUD; state in PlayerPrefs.
- AudioMixer with Master/SFX groups so mute is one line.

# Polish & QA
- No errors or warnings in the Console after Play.
- mosaic_camera_screenshot of: (1) Main Menu, (2) mid-gameplay with pipes +
  particles, (3) Game Over screen.
- Verify Play Mode launches cleanly and a full play-die-restart loop works.
- WebGL build completes without errors.
- Tab focus / app pause: pause on Application.focusChanged == false
  (Time.timeScale = 0).
- Touch, mouse, keyboard, gamepad all jump through the same Input Action.
- All tunable constants exposed as [SerializeField] on a GameTuning
  ScriptableObject so designers can rebalance without code changes.

# Deliverable
1. Full Unity project at the path Mosaic creates.
2. README.md at project root: how to open, how to play, package list with
   versions, tuning guide.
3. Three Mosaic screenshots embedded in the README.
4. WebGL build output in Builds/WebGL/.
5. A 5–8 bullet architecture summary + the list of GameTuning constants
   and their default values.

Work iteratively: scaffold → confirm scene loads via screenshot → add bird +
input → screenshot → add pipes + scoring → screenshot → add FX/audio/UI →
final screenshots + WebGL build. Do not batch everything into one step —
verify visually at each milestone using the Mosaic camera tool.
````

That's the entire input. Everything below is what Claude produced from it.

## What's impressive about this prompt → result

This isn't a "vibe-coding" demo. The prompt is **highly specific** — explicit physics constants (gravity -30, jump +9), explicit gap size (~3 units), explicit spawn rate (~1.4s), explicit tilt clamps (-90° to +30°), explicit package list, explicit folder structure. A specific brief like this is harder than a vague one because every constraint is verifiable — and Claude hit them all.

What Claude still had to figure out on its own:
- How to install each package via Mosaic Bridge package management tools
- How to create and serialize the `.inputactions` asset and trigger C# wrapper generation
- How to author and serialize the Shader Graph for the pipe highlight + parallax sky
- How to wire DOTween, Cinemachine Impulse, and the URP Volume system together
- How to procedurally generate AudioClips with `AudioClip.Create` and route them through an `AudioMixer`
- How to recover from any tool errors or Unity compile failures encountered along the way
- WebGL build configuration and post-build asset verification

## Build sequence (high-level)

The video shows the order Claude executed in. Approximate phases:

| Phase | What happens |
|-------|-------------|
| Preflight | `project/preflight` — confirms Unity version, URP 2D, no installed packages yet |
| Package install | Input System, Cinemachine, TMP, URP 2D Renderer, Shader Graph, 2D Animation, Sprite Shape, DOTween, Burst, Mathematics |
| Project settings | Disable legacy Input Manager, set 9:16 aspect, configure Pixel Perfect Camera, URP asset assigned |
| Folder + ScriptableObject scaffolding | Full `Assets/` tree from the prompt + `GameTuning` SO with default values |
| Input Actions asset | `PlayerInputActions.inputactions` with Gameplay map (Jump + Pause), C# wrapper generated |
| Bird (player) | Sprite, Rigidbody2D, CircleCollider2D, BirdController, OnJump callback, tilt lerp |
| Pipe + object pool | Pipe prefab with two sprites + ScoreZone trigger, PipeSpawner with pool (no runtime Instantiate/Destroy) |
| Scoring | Trigger detection, ScoreManager, TMP HUD with DOTween scale-punch on increment |
| Game state machine | GameManager state enum (Menu/Ready/Playing/Dying/GameOver), action map enable/disable per state |
| Parallax + day/night | 3-layer parallax, Shader Graph sky gradient, DayNightCycle lerping global light + color grading |
| FX | Feather burst particles, Cinemachine Impulse on death, chromatic aberration kick via DOTween |
| Audio | AudioManager creates jump/score/hit/die clips at runtime via `AudioClip.Create`, AudioMixer wiring, mute toggle |
| UI | MainMenu, HUD, GameOver panel, DOTween fade transitions, high score from PlayerPrefs |
| Verification | `mosaic_camera_screenshot` × 3 (menu, mid-game, game over) |
| WebGL build | Build to `Builds/WebGL/`, capture build log |

Each phase boundary in the video corresponds to a `mosaic_camera_screenshot` call — that's how you can find them on the timeline. The on-screen tool overlay names which tool fires.

## Reproduce it yourself

```bash
# 1. Install Mosaic Bridge
npx @mosaicxr-ai/create-bridge

# 2. Open your Unity project (Unity 2022.3 LTS or newer; empty 2D URP template)

# 3. In Claude Code (or any MCP client), paste the full prompt above.
```

Your run will not produce identical code — Claude makes its own decisions on naming, ordering, and small structural choices each time. But the deliverable will match: a working Flappy Bird clone with the constraints from the prompt enforced.

## Full prompt-by-prompt + tool-call transcript

The verbatim Claude Code session JSONL (every prompt, every tool call, every result) is being cleaned up for general publication and will be appended here. If you want the raw export sooner, [open an issue](https://github.com/MosaicXR-AI/mosaic-bridge/issues/new/choose) — happy to share it on request.

In the meantime, the YouTube video is the source of truth — every Mosaic Bridge tool call appears as a lower-third overlay when it fires.

## See also

- [README — See it work](../../README.md#see-it-work)
- [`CLAUDE.md` instruction template](../../packages/create-bridge/src/templates.js) — including the **Multi-Angle Screenshots** rule that Claude followed automatically here
- [Mosaic Bridge knowledge base](../../packages/com.mosaic.bridge/Editor/Knowledge/) — pipeline detection, shader properties, Unity API quirks
- [Mosaic Bridge tools](../../README.md#tool-categories) — every tool used in this build is in the catalogue
