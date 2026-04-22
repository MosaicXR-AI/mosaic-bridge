# Changelog — com.mosaic.bridge

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0-beta.10] — 2026-04-22

### Added

- **`AssetDatabaseHelper.EnsureFolder`** (internal shared utility) — Ensures a folder path
  exists in both the filesystem and AssetDatabase using `AssetDatabase.CreateFolder` for each
  level. Replaces unsafe `Directory.CreateDirectory` calls that left directories unregistered
  in AssetDatabase, causing opaque `{"suggestedFix":null}` errors in 30+ code-gen tools.

### Fixed

- **`gameobject/set_active`** — Now uses `Resources.FindObjectsOfTypeAll<GameObject>()`
  filtered by scene validity, so it can activate **inactive** GameObjects. Previous
  `GameObject.Find()` implementation skipped inactive objects entirely.

- **`shadergraph/list`** — Replaced `AssetDatabase.FindAssets("t:Shader")` with a
  filesystem search (`Directory.GetFiles("*.shadergraph")`). The type-indexed search
  returned 0 results when `.shadergraph` files existed but weren't yet fully imported.

- **`prefab/info`** — Wrapped `PrefabUtility.GetObjectOverrides / GetAddedComponents /
  GetRemovedComponents / GetAddedGameObjects / GetRemovedGameObjects` calls in try-catch.
  These APIs throw "Provided GameObject is not a Prefab instance" when called on prefab
  asset roots (as opposed to scene instances). Tool now returns correct metadata for
  both asset paths and scene instances.

- **`mesh/generate`, `mesh/decimate`, `mesh/boolean`, `mesh/convex-hull`** — Applied
  `AssetDatabaseHelper.EnsureFolder` before `AssetDatabase.CreateAsset`. Added try-catch
  around `CreateAsset` to surface the real error message instead of opaque failure.

- **`simulation/cloth`, `simulation/fluid`, `simulation/smoke`** — Applied
  `AssetDatabaseHelper.EnsureFolder` before `File.WriteAllText` / `AssetDatabase.ImportAsset`
  to ensure output directories are registered before use.

- **`procgen/terrain`** — Applied `AssetDatabaseHelper.EnsureFolder` for the same reason.

- All remaining procgen + simulation + physics + AI + animation + measure tools that used
  `Directory.CreateDirectory(fullDir)` now use `AssetDatabaseHelper.EnsureFolder` instead.

### Changed (descriptions / caveats)

- **`mesh/generate`** — Description now explicitly states Vertices is a flat `float[]`
  (`[x0,y0,z0,x1,y1,z1,...]`), NOT nested arrays. Includes a minimal triangle example.

- **`procgen/wfc`** — Description now includes a complete Tiles schema example with
  `Id`, `Weight`, `AllowedNeighbors` keys and direction names (right/left/up/down/forward/back).

- **`reflection/call-method`** — Description now states struct/vector args must be JSON
  objects (`{"x":1,"y":2,"z":3}`), NOT arrays (`[1,2,3]`).

---

## [1.0.0-beta.9] — 2026-04-22

### Added — Spatial coherence + scene intelligence

- **`terrain/sample-height`** — Returns world-space Y at any XZ position on the active terrain.
  Use before every `gameobject/create` or `prefab/instantiate` to resolve placement Y.
  Result: `WorldY`, `NormalizedHeight`, `SuggestedPlacementY [x, worldY+0.1, z]`, `TerrainSize`.
  Read-only.

- **`gameobject/snap-to-ground`** — Snaps an existing GameObject's Y to
  `terrain.SampleHeight(x,z) + YOffset`. Fixes bulk-placed objects after sculpting.
  Modes: `terrain` (edit-mode, fast) or `raycast` (physics-based, for non-terrain floors).
  Undo-safe.

- **`terrain/get-regions`** — Reads the splatmap and returns per-layer coverage stats:
  dominant coverage fraction (%), world-space bounding box for each layer's painted area,
  center world position. Use before `scene/plan-composition` to understand current
  texture layout.

- **`scene/plan-composition`** — Takes a structured scene intent (regions, landmarks,
  time of day, player type) and returns a validated execution plan: pre-resolved Y
  coordinates for every landmark (sampled from active terrain), ordered build phases,
  lighting parameters (angle, color, intensity keyed to time of day/weather), and camera
  start position. Run after terrain sculpting, before placing objects.

- **`shadergraph/add-node`** — Adds a processing node to an existing `.shadergraph` file.
  Supports 38 common node types via friendly aliases: math (add, subtract, multiply, divide,
  power, lerp, clamp, saturate, abs, negate, sqrt, floor, ceil, frac, step, smoothstep, remap),
  utility (split, combine, swizzle, fresnel), input (float, vector2/3/4, color, uv, time,
  position, normal, viewdir), texture (sampletexture2d, samplecubemap). Returns `NodeId`
  (GUID) and slot definitions for use with `shadergraph/connect`.

- **`shadergraph/connect`** — Creates an edge between two ShaderGraph nodes via
  `OutputNodeId + OutputSlotId → InputNodeId + InputSlotId`. Both IDs are returned by
  `shadergraph/add-node`. Validates both nodes exist before writing.

### Fixed

- **`component/set_reference` (Issue #6)** — Now traverses nested struct property paths
  via `FindPropertyRelative()` with automatic `m_` prefix fallback. Both
  `"Target.TrackingTarget"` and `"m_Target.m_TrackingTarget"` resolve correctly. Also
  extended to support **value-type assignments**: `FloatValue`, `IntValue`, `BoolValue`,
  `StringValue` (including enum names), `ColorValue [r,g,b,a]`, and `VectorValue [x,y,z]`.
  Closes the Cinemachine CM3 Follow-target, Lens field, and Volume override `.value` blockers.

- **`texture/set-import-settings` (Issue #7)** — Added `TextureShape` parameter:
  `2D`, `Cube` (cubemap), `2DArray`, `3D`. Use `TextureShape=Cube` with
  `TextureType=Default` to convert an equirectangular HDRI into a Cubemap for skyboxes.
  Result now includes `TextureShape` field.

### Docs / planning

- KB entry T5.1 `scene/scene-composition-guide.json` — 5-tier scene interview protocol,
  8-phase build pipeline, spatial coherence contract, ScenePlan JSON template.
- `CLAUDE.md.unity-template` — now distributed by `create-bridge@beta.4` installer.

## [1.0.0-beta.8] — 2026-04-21

### Fixed

Round 3 of manual-test-driven fixes. Addresses the dispatcher and schema
issues surfaced by the beta.7 test run.

- **AssemblyGuard (issue #8)** — added the 8 optional-package tool
  wrapper assemblies (`Mosaic.Bridge.Tools.Cinemachine`,
  `Mosaic.Bridge.Tools.ProBuilder`, `Mosaic.Bridge.Tools.Addressables`,
  `Mosaic.Bridge.Tools.TextMeshPro`, `Mosaic.Bridge.Tools.URP`,
  `Mosaic.Bridge.Tools.HDRP`, `Mosaic.Bridge.Tools.Splines`,
  `Mosaic.Bridge.Tools.VisualScripting`) to `DefaultAllowed`. These are
  Mosaic-authored and ship as part of the same package, so they're
  trusted by construction. Before this fix the tools appeared in
  `meta/list_advanced_tools` (which bypasses the guard) but were NOT
  in the registry (which respects the guard), producing the dispatcher
  mismatch — `meta/advanced_tool` returned "Tool not found" for every
  name format tried. Each assembly only compiles when its associated
  Unity package is installed, so enabling them by default is safe.

## [1.0.0-beta.7] — 2026-04-21

### Fixed

Round 2 of manual-test-driven fixes. Caught by re-running previously-SKIPed
tests after the test-assets-manifest landed.

- **input/create (T-INPUT-01 FAIL-ERROR)** — seeded a default empty
  ActionMap before calling `InputActionAsset.ToJson()`. A freshly-created
  asset has null internal collections; `ToJson()` iterates them via LINQ
  which throws `ArgumentNullException: Value cannot be null. Parameter
  name: source`. Now returns a valid stub asset callers can populate via
  follow-up `input/map` tools.
- **terrain/trees (T-TERRAIN-SCULPTING-05 PASS-WITH-WARNINGS)** — reject
  prefabs whose root has no `MeshRenderer`, `LODGroup`, or
  `BillboardRenderer`. Unity's terrain tree system renders from the
  prototype root only — nested-child visuals silently don't draw. Clear
  error now surfaces the constraint + suggests `gameobject/create` as an
  alternative scattering path.
- **ui/create_canvas** — accept Unity-doc canonical enum names
  (`ScreenSpaceOverlay`, `ScreenSpaceCamera`, `WorldSpace`) in addition
  to the short aliases (`Overlay`, `Camera`, `WorldSpace`) that were the
  only accepted values in beta.1-6. Result `RenderMode` field now
  returns the canonical name. Non-breaking for existing callers.

### Docs

- **test-assets-manifest.md** — corrected Poly Haven slugs: 
  `rock_surface_04` → `rocks_ground_01`, `grass_medium_03` → `leafy_grass`
  (original URLs 404'd). Added inline note about the
  `texture/set-import-settings` textureShape gap with an
  `editor/execute-code` workaround.

### Issues filed (tracked separately)

- **#6** `component/set_reference` doesn't traverse nested struct paths —
  blocks CM3 Follow target, Spline knots, Volume override `.value`.
  Foundational capability gap.
- **#7** `texture/set-import-settings` has no `textureShape` option —
  blocks HDRI→Cubemap workflow.
- **#8** Optional-package tool wrappers (ProBuilder, Addressables, etc.)
  not discoverable even when packages are installed. May be resolved by
  the beta.4-6 compile-error fixes; verify after re-import.

## [1.0.0-beta.6] — 2026-04-21

### Fixed
Package-integration tests now compile cleanly when the corresponding
packages (Cinemachine, Splines, ProBuilder, Addressables, TextMeshPro)
are present. Previous tests referenced stale API shapes:

- **CinemachineToolTests.cs** — qualified `AddComponent<Camera>()` as
  `UnityEngine.Camera`. After adding `using Unity.Cinemachine;`
  the bare `Camera` identifier resolved to the `Unity.Cinemachine.Camera`
  namespace (not a type), causing CS0118.
- **SplinesToolTests.cs** — replaced non-existent nested types
  `SplineCreateParams.KnotData` / `SplineAddKnotParams.KnotDataEntry`
  with the actual top-level `SplineKnotData`. Replaced
  `result.Data.Splines.Count` (array) with `.Length`. Replaced
  `result.Data.NewKnotCount` with the actual `KnotCount` field.
- **ProBuilderToolTests.cs** — renamed `.Execute` → `.Create` / `.Info` /
  `.Modify` to match the actual public methods. Replaced
  `result.Data.Meshes.Count` with `.Length` (array).
- **AddressablesToolTests.cs** — renamed `.Execute` → `.Groups` / `.Info` /
  `.Mark`.
- **TextMeshProToolTests.cs** — proactive fix: `result.Data.Components.Count`
  → `.Length`.

## [1.0.0-beta.5] — 2026-04-21

### Fixed
- **Mosaic.Bridge.Tests.asmdef** — added `Unity.Cinemachine` to references.
  `CinemachineToolTests.cs` uses `using Unity.Cinemachine;` directly (for
  `CinemachineCamera`, `CinemachineBrain`, `CinemachineSplineDolly`
  assertions), and Unity asmdef references do not cascade through
  intermediate assemblies. Tests compile now when Cinemachine is installed.

## [1.0.0-beta.4] — 2026-04-21

### Fixed
- **AddressablesBuildTool.cs** — added missing `using System.Linq;` so
  `GetFilePaths().Count()` resolves. The tool previously failed to compile
  in any project with the Addressables package installed.
- **CinemachineCreateDollyTool.cs** — tightened `#if` guard from
  `MOSAIC_HAS_CINEMACHINE` to `MOSAIC_HAS_CINEMACHINE && MOSAIC_HAS_SPLINES`.
  Added `Unity.Splines` + `Unity.Mathematics` assembly references and the
  `MOSAIC_HAS_SPLINES` versionDefine to `Mosaic.Bridge.Tools.Cinemachine.asmdef`.
  Fixes compile when Cinemachine is installed without Splines (CM 2.x path).
- **ProBuilderModifyTool.cs** — replaced non-existent
  `MergeElements.MergeFaces` with the correct public API
  `MergeElements.Merge(mesh, faces)` (ProBuilder 6.x). Rewrote the
  `detach` case to use the stable `ProBuilderMesh.DeleteFaces` primitive
  instead of the non-public `DetachElements` class.

## [1.0.0-beta.3] — 2026-04-21

### Fixed
- **settings/get-render** (#1) — use `GraphicsSettings.currentRenderPipeline`
  so per-quality-level overrides are respected. Added canonical `Pipeline`
  field (`BuiltIn` / `URP` / `HDRP` / `Custom`) and stable
  `RenderPipelineAssetType` (asset rename-proof). `RenderPipelineAsset`
  preserved for back-compat.
- **graphics/set-post-processing** (#2) — honor `sharedProfile` + `priority`
  + `isGlobal` + `weight`. The previous reflection-only-GetProperty path
  silently no-op'd because Unity's `Volume` declares these as public
  FIELDS, not properties. Added field-fallback.
- **gameobject/duplicate** (#3) — new optional `NewName`, `Position`,
  `Parent` params. Auto-uniquifies name via
  `GameObjectUtility.GetUniqueNameForSibling` when `NewName` is null.
  Empty-string `Parent` explicitly unparents.
- **terrain/height** (#4) — new `array` action accepts a caller-provided
  `float[]` heightmap region + `Width`/`HeightCells` + `ArrayX`/`ArrayY`
  placement; applied in a single `SetHeights` call.
  Supports `BlendMode` (replace/add/max/min) and `DelayLod` for
  multi-patch sequences. Replaces per-cell atomic iteration patterns.
- **material/set-property** (#5) — new `bool` ValueType (for material
  flags like `enableInstancing`, `doubleSidedGI` — bypasses HasProperty)
  and `keyword` ValueType (toggles shader keywords like `_EMISSION`,
  `_NORMALMAP`, `_ALPHATEST_ON`).

## [1.0.0-beta.2] — 2026-04-21

### Fixed
- **Windows MCP launch** — `ClaudeCodeConfigurator.cs` now wraps the
  fallback `mosaic-mcp` global binary invocation in `cmd /c` when running
  on Windows, matching the `create-bridge` installer behavior. Previously
  Claude Code on Windows surfaced "Windows requires 'cmd /c' wrapper to
  execute npx" when falling back from the local `dist/index.js` detection.

## [1.0.0-beta.1] — 2026-04-19

Initial beta release.
- MCP server + Unity Editor bridge
- 288 tools across 67 categories
- Per-project runtime isolation
- Auto `.mcp.json` configuration for Claude Code
- Apache 2.0 license with patent grant
