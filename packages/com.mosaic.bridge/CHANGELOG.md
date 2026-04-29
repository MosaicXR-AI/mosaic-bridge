# Changelog — com.mosaic.bridge

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0-beta.4] — 2026-04-29

### Added

- **Full knowledge base** — 35 authored KB entry files synced into `Editor/Knowledge/`
  across 18 categories: `core`, `physics`, `rendering`, `animation`, `terrain`, `procgen`,
  `shadergraph`, `navigation`, `ui`, `particle`, `input`, `editor`, `scene`, `spline`,
  `probuilder`, `addressables`, `dataviz`, `visualscripting`.
  Every entry includes `summary`, `mosaicTools`, `llmGuidance`, `commonMistakes`, and
  `examples` fields for rich LLM context injection.

- **Generic KB list/read** — `HandleKbList` now enumerates all entry-schema KB files
  (not just physics constants and PBR materials). `HandleKbRead` falls back to authored
  entry files for any `category/key` not found in reference data.
  Full KB is now visible as MCP resources and accessible via `mosaic://knowledge/{cat}/{key}`.

- **`KnowledgeBase.LoadEntry(category, key)`** — loads any KB entry file by category
  and filename key.

- **`KnowledgeBase.ListEntries(category?)`** — scans `Editor/Knowledge/` and returns
  all entry-schema JSON files.

---

## [1.0.0-beta.3] — 2026-04-29

### Added

- **`project/preflight`** — returns the active render pipeline (URP / HDRP / BuiltIn),
  the correct color property (`_BaseColor` or `_Color`), Unity version, and platform.
  Call once at session start; avoids magenta-material errors from wrong shader/pipeline combos.

- **`material/create-batch`** — creates multiple material assets in a single call.
  Returns separate `Created`, `Skipped`, and `Failed` lists for partial-failure handling.
  Supports per-entry `ShaderName` with the same auto-detect fallback as `material/create`.

- **Knowledge base: rendering** — three new KB files bundled with the package:
  `rendering/render-pipeline-compat.json` (shader ↔ pipeline compatibility matrix),
  `rendering/shadergraph-nodes.json` (38 node aliases with slot descriptions),
  `rendering/unity-api-quirks.json` (documented API pitfalls with workarounds).

### Fixed

- **`component/set_reference`** — `FindPropertyFuzzy` now parses array index expressions
  (e.g. `Spline.Knots[0].Position`): detects `[n]` in each dot-separated segment, strips
  the index, finds the array property with the usual `m_` prefix fallback, then calls
  `GetArrayElementAtIndex(n)` before continuing traversal. Fixes GitHub issue #6.

- **`component/set_property`** — fixed `CS8121` pattern-match error on `ObjectReference`
  values: replaced `value is string refPath` with `value?.Value<string>()` for correct
  `JToken` handling.

- Various tool refinements across ShaderGraph, Physics, Audio, Materials, and Particles.

---

## [1.0.0-beta.2] — 2026-04-22

### Added

- **`scene/create-object`** — Mandatory entry point for all complex object creation.
  Runs the full decision tree and returns an `Action` telling the AI exactly what to do:
  `"primitive"` → use `probuilder/create` directly;
  `"instantiate"` → use `asset/instantiate_prefab`;
  `"choose"` → multiple project matches found, show list to user (`AutoApprove` skips);
  `"store"` → show Asset Store URL to user;
  `"build"` → execute the returned `Parts` list with `probuilder/create`.
  Each `BuildPart` includes `Shape`, `Dimensions`, `Position`, `Rotation` (euler angles),
  and `MaterialHex` (CSS hex color). Built-in plans for ship (19 parts), house, castle, tree.

- **`asset/find-3d`** — Searches `Assets/` for existing prefabs, FBX, OBJ, and model files
  before building anything from scratch. Expands natural-language queries via semantic aliases
  (`ship` → boat/vessel/galleon, `house` → building/cottage/cabin, etc.). Returns
  `IsPrimitive=true` for simple shapes (skip search), ranked project matches, and an Asset
  Store free-search URL when nothing is found locally.

- **`editor/run-block` + `editor/run-block-poll`** — Multi-statement C# execution with polling.
  Accepts a full method body, wraps it in a temp `[InitializeOnLoad]` Editor class, compiles it,
  and stores the result in `EditorPrefs` (survives domain reload). Poll with `JobId` to get
  `status` (`compiling` / `pending` / `done` / `error`) and `Output` (captured `Debug.Log` lines).

- **`terrain/sample-height`** — Returns world-space Y at any XZ position on the active terrain.
  Result includes `WorldY`, `NormalizedHeight`, and `SuggestedPlacementY` for correct object placement.

- **`gameobject/snap-to-ground`** — Snaps an existing GameObject's Y to terrain height + offset.
  Supports `terrain` (edit-mode) and `raycast` (physics-based) modes.

- **`terrain/get-regions`** — Reads the terrain splatmap and returns per-layer coverage stats
  (dominant fraction, world-space bounding box, center position).

- **`scene/plan-composition`** — Returns a validated scene build plan with pre-resolved Y
  coordinates (sampled from active terrain), ordered build phases, and lighting parameters.

- **`shadergraph/add-node`** — Adds a processing node to an existing `.shadergraph` file.
  Supports 38 node types via friendly aliases (math, utility, input, texture families).

- **`shadergraph/connect`** — Creates an edge between two ShaderGraph nodes.

- **`particle/set-renderer`** — Configures `ParticleSystemRenderer`: `RenderMode`,
  `VelocityScale`, `LengthScale`, `MaxParticleSize`, `MaterialPath`, `UseUrpParticlesMaterial`.

- **`AssetDatabaseHelper.EnsureFolder`** (internal) — Registers folder paths in AssetDatabase
  using `AssetDatabase.CreateFolder`. Replaces `Directory.CreateDirectory` across all code-gen
  tools to prevent opaque `{"suggestedFix":null}` errors on first use.

- **Project Settings → Mosaic Bridge → Particle Pack Source** — Choose which particle source
  `particle/create` uses: Unity Particle Pack, Starter Particle Pack, Legacy Particle Pack,
  or Built-in Presets Only. Shows detection status (In Project / Cached / Not Downloaded)
  with Import and Open Store Page buttons.

- **Project Settings → Mosaic Bridge → Asset Search Behavior** — Toggle to enable/disable
  Asset Store link suggestions when `scene/create-object` finds nothing in the project.

### Fixed

- **`console/get-errors`** — Returned `Count: 0` even when the console had visible messages.
  Two root causes fixed: (1) lazy initialization missed domain-reload messages — fixed with
  `[InitializeOnLoad]`; (2) console Clear wiped `LogEntries` — fixed with a persistent file log
  at `Library/MosaicBridge/console.log` that survives both Clear and domain reloads.

- **`probuilder/create`** — Was calling `ShapeGenerator.CreateShape()` + `transform.localScale`
  (geometrically incorrect). Now calls the specific `Generate*` method for each shape:
  `GenerateCube`, `GeneratePrism`, `GenerateCylinder`, `GenerateDoor`, `GenerateStair`,
  `GenerateArch`, `GeneratePipe`, `GenerateCone`, `GenerateIcosahedron`, `GenerateTorus`.

- **`particle/create`** — Rendered magenta in URP. Now auto-assigns
  `Universal Render Pipeline/Particles/Unlit` material. Rain preset fixed: `RenderMode=Stretch`,
  `VelocityScale=0.8`, `LengthScale=3`, `MaxParticleSize=0.5`.

- **`particle/create`** — OS Asset Store cache detection: if a Unity Technologies particle pack
  `.unitypackage` is found in the OS cache, it is imported silently and used automatically.

- **`component/set_reference`** — Now traverses nested struct property paths via
  `FindPropertyRelative()`. Supports value-type assignments: `FloatValue`, `IntValue`,
  `BoolValue`, `StringValue`, `ColorValue`, `VectorValue`.

- **`texture/set-import-settings`** — Added `TextureShape` parameter (`2D`, `Cube`, `2DArray`, `3D`)
  to support HDRI → Cubemap conversion for skyboxes.

- **`gameobject/set_active`** — Now uses `Resources.FindObjectsOfTypeAll<GameObject>()` so
  inactive GameObjects can be found and activated.

- **`shadergraph/list`** — Replaced `AssetDatabase.FindAssets("t:Shader")` with a filesystem
  search to fix zero results when `.shadergraph` files existed but weren't fully imported.

- **`prefab/info`** — Wrapped PrefabUtility override APIs in try-catch to handle both
  prefab asset roots and scene instances without throwing.

- **`settings/get-render`** — Uses `GraphicsSettings.currentRenderPipeline` to respect
  per-quality-level overrides.

- **`input/create`** — Seeds a default empty ActionMap before `ToJson()` to prevent
  `ArgumentNullException` on freshly-created assets.

- **`terrain/trees`** — Rejects prefabs whose root has no `MeshRenderer`, `LODGroup`, or
  `BillboardRenderer` (Unity terrain tree system only renders from the prototype root).

- **`terrain/height`** — New `array` action applies a full heightmap region in one `SetHeights`
  call. Supports `BlendMode` (replace/add/max/min) and `DelayLod`.

- **`material/set-property`** — New `bool` ValueType (material flags) and `keyword` ValueType
  (shader keyword toggles like `_EMISSION`, `_NORMALMAP`).

- **`mesh/*`, `simulation/*`, `procgen/*`** — All use `AssetDatabaseHelper.EnsureFolder`
  before asset creation to prevent opaque folder-not-registered errors.

- **AssemblyGuard** — Optional-package tool assemblies (ProBuilder, Cinemachine, Addressables,
  TextMeshPro, URP, HDRP, Splines, VisualScripting) added to `DefaultAllowed` so they are
  visible to the MCP dispatcher when their packages are installed.

- **Windows** — `ClaudeCodeConfigurator.cs` wraps the fallback `mosaic-mcp` invocation in
  `cmd /c` on Windows, matching the `create-bridge` installer behavior.

### Changed

- **`probuilder/create`** — Added `Rotation: [x,y,z]` euler angles parameter. Added
  `ParentName` parameter for hierarchy grouping. Description rewritten: references
  `scene/create-object` as mandatory prerequisite; hard-rejects complex object names;
  blocks unsolicited complex assemblies via `MosaicBridge.BuildPlanActive` EditorPrefs flag.

- **`editor/run-block`** — Description now starts with an explicit `⛔ DO NOT use` list:
  GameObjects, ProBuilder meshes, materials, prefab instantiation, and any task that has a
  dedicated MCP tool. The previous description listed "ProBuilder calls" as a valid use case,
  which caused the AI to bypass `probuilder/create` entirely.

- **`editor/execute-code`** — Same prohibition added for scene-content creation.

- **`particle/create`** — Searches all installed particle packs (not just Unity's built-in
  presets). New `PrefabPath` and `UseExistingPrefab` parameters. New keyword aliases for
  fire, rain, smoke, sparks, explosion, snow, and more.

- **`ui/create_canvas`** — Accepts both canonical enum names (`ScreenSpaceOverlay`) and
  short aliases (`Overlay`).

---

## [1.0.0-beta.1] — 2026-04-19

Initial beta release.
- MCP server + Unity Editor bridge
- 288 tools across 67 categories
- Per-project runtime isolation
- Auto `.mcp.json` configuration for Claude Code
- Apache 2.0 license with patent grant
