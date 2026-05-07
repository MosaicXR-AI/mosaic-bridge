Build a complete, polished Flappy Bird clone as a Unity project. Use the MosaicXR MCP bridge (`mcp-mosaic-bridge`) to scaffold the project, drive the editor, and verify the build visually via `mosaic_camera_screenshot` after each major milestone. Deliver a runnable Unity project that opens cleanly in the Unity Editor and builds to WebGL/Standalone without errors.

# Project setup (via Mosaic MCP)
- Unity version: 2022.3 LTS or newer (use whatever the Mosaic bridge currently exposes — confirm via MCP before scaffolding).
- Render pipeline: Universal Render Pipeline (URP) 2D — required for 2D lights, post-processing, and shader graph.
- Project template: 2D (URP). Aspect ratio locked to 9:16 portrait (mobile-friendly), with a Pixel Perfect Camera component for crisp scaling.
- Use Mosaic MCP to: create the project, add packages, create scenes, create prefabs, attach scripts/components, and capture screenshots after each step for verification.

# Required Unity packages (all free, official)
Install via Package Manager through the Mosaic bridge:
- **Input System** (com.unity.inputsystem) — REQUIRED. Disable the legacy Input Manager in Project Settings → Player → Active Input Handling = "Input System Package (New)".
- **Cinemachine** — for smooth camera follow / screen shake via Impulse Source.
- **TextMeshPro** — all UI text.
- **Universal RP** + **2D Renderer** — lighting and post-FX.
- **Post Processing** (via URP Volume) — bloom, vignette, chromatic aberration, color grading.
- **Shader Graph** — for the parallax sky gradient and pipe highlight shader.
- **2D Animation** + **2D Sprite** — bird wing-flap rig.
- **2D Sprite Shape** — for organic ground/cloud silhouettes.
- **DOTween (HOTween v2)** — free from Asset Store; use for score pulse, fade transitions, game-over UI animations.
- **Burst** + **Mathematics** — for cheap particle math if needed.

# Project structure
```
Assets/
  Art/            (sprites generated procedurally or simple placeholders)
  Audio/          (procedurally generated AudioClips at runtime via AudioSource + AudioClip.Create)
  Prefabs/        (Bird, Pipe, GroundTile, ParticleBurst, Cloud)
  Scenes/         (MainMenu, Game)
  Scripts/
    Core/         (GameManager, GameState enum, ScoreManager, AudioManager)
    Gameplay/     (BirdController, PipeSpawner, Pipe, ParallaxLayer, GroundScroller)
    Input/        (PlayerInputActions.inputactions + generated C# wrapper)
    UI/           (HUDController, MainMenuController, GameOverPanel)
    FX/           (CameraShake, FeatherBurst, ScorePopup, DayNightCycle)
  Settings/       (URP asset, Input Actions asset, Volume profile)
```

# Input System (NEW — not legacy)
- Create `PlayerInputActions.inputactions` with one Action Map "Gameplay":
  - `Jump` (Button): bindings for `<Keyboard>/space`, `<Keyboard>/upArrow`, `<Mouse>/leftButton`, `<Touchscreen>/primaryTouch/tap`, `<Gamepad>/buttonSouth`.
  - `Pause` (Button): `<Keyboard>/escape`, `<Gamepad>/start`.
- Generate the C# wrapper class (Auto-Generate C# Class checkbox).
- `BirdController` consumes input via the generated class (`_actions.Gameplay.Jump.performed += OnJump`) — never via `Input.GetKey`.
- Enable/disable action maps based on game state.

# Game mechanics (deterministic, bug-free)
- Physics in `FixedUpdate`. Gravity ~-30 (Unity units/s²), jump impulse sets velocity.y = +9. Tune until feel matches original.
- Bird uses Rigidbody2D (Dynamic, gravity scale ~3) + CircleCollider2D (radius slightly smaller than sprite for forgiving hits).
- Pipes: pooled (object pool, no Instantiate/Destroy during play). Spawn every ~1.4s. Gap Y randomized within safe bounds; gap size ~3 units.
- Score: trigger collider on pipe center; OnTriggerEnter2D with "ScoreZone" tag. Each pipe scores once (boolean flag).
- Bird tilt: lerp Z rotation based on Rigidbody2D.velocity.y, clamped between -90° and +30°.
- Game states: `Menu`, `Ready`, `Playing`, `Dying`, `GameOver`. State machine in GameManager.
- High score persisted via PlayerPrefs.

# High-end graphics
- **URP 2D Lights**: global light + a soft point light following the bird (subtle glow).
- **Post-processing Volume** (Global): Bloom (intensity ~0.6), Vignette (mild), Chromatic Aberration (kicks up briefly on collision via DOTween), Color Adjustments for day/night tint.
- **Parallax**: 3 layers (far sky gradient via Shader Graph, mid clouds via Sprite Shape, near ground tiles). `ParallaxLayer` script multiplies camera delta by layer factor.
- **Day/night cycle**: DayNightCycle script lerps the global light color + Volume color-grading temperature over a ~30s loop.
- **Bird animation**: 3-frame flap via 2D Animation package, time-driven (not event-driven).
- **Particles**: feather burst (Particle System) on death; sparkle on score.
- **Camera shake**: Cinemachine Impulse Source fires on collision; CinemachineImpulseListener on the vcam.
- **Score UI**: TextMeshPro with outlined font asset; DOTween scale-punch on increment.
- **Pipe shader**: Shader Graph adds a vertical highlight gradient + soft drop shadow (not flat green).
- **Transitions**: DOTween fade for state changes.

# Audio (procedural via AudioClip.Create)
- AudioManager generates clips at runtime: jump (rising sine blip), score (two-note ding), hit (noise burst + low thud), die (descending tone).
- Mute toggle button in HUD; state in PlayerPrefs.
- AudioMixer with Master/SFX groups so mute is one line.

# Polish & QA
- No errors or warnings in the Console after Play.
- Use Mosaic's `mosaic_camera_screenshot` to capture: (1) Main Menu, (2) mid-gameplay with pipes + particles, (3) Game Over screen — attach all three to the final summary.
- Verify Play Mode launches cleanly and a full play-die-restart loop works.
- WebGL build completes without errors (run the build via MCP and capture the build log).
- Tab focus / app pause: pause game on `Application.focusChanged == false` (Time.timeScale = 0), resume on focus.
- Touch (mobile), mouse, keyboard, and gamepad all jump correctly through the same Input Action.
- All tunable constants exposed as `[SerializeField]` on a `GameTuning` ScriptableObject so designers can rebalance without code changes.

# Deliverable
1. Full Unity project at the path Mosaic creates.
2. `README.md` at project root: how to open, how to play, package list with versions, tuning guide.
3. Three Mosaic screenshots embedded in the README.
4. WebGL build output in `Builds/WebGL/`.
5. A 5–8 bullet architecture summary + the list of `GameTuning` constants and their default values.

Work iteratively: scaffold → confirm scene loads via screenshot → add bird + input → screenshot → add pipes + scoring → screenshot → add FX/audio/UI → final screenshots + WebGL build. Do not batch everything into one step — verify visually at each milestone using the Mosaic camera tool.