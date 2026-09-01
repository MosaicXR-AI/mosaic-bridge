# Changelog — com.mosaic.bridge

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0-beta.12] — 2026-09-01

### Fixed

- **Mosaic Pro tools are allowed by default.** AssemblyGuard refused all 29 of them on
  first import, so a licensed product greeted its buyer with 29 warnings naming the
  tools they had just bought and telling them to edit a settings page. The Pro
  assemblies have the same authorship and trust basis as the bridge tool assemblies,
  and exist only when the licensed packages are installed.
- **No `.mcp.json` is written unless it can work.** The Editor wrote one unasked into
  the project root pointing at a binary that was not installed, which then shadowed a
  working HTTP server for anyone starting Claude Code there.
- **No bare `mosaic-mcp` in generated configuration.** An unrelated package on npm
  publishes a binary of that name, so a bare command ran whatever was first on PATH.
  Generated configuration now invokes `npx -y @mosaicxr-ai/mcp-server`, and the
  package also installs as `mosaic-bridge-mcp`.
- **The npm hint is no longer a warning.** Most installations reach the Editor through
  the Mosaic connector, which needs no npm package at all; the line was the only
  warning in an otherwise clean console.

### Added

- **`@mosaicxr-ai/connector`** — links a local Unity Editor to a Mosaic service over an
  outbound connection, with installers for Windows and macOS.

## [1.0.0-beta.11] — 2026-08-28

### Fixed

- **Five tools that misreported what happened** (B1-B5): success/failure results now match
  the actual Editor outcome, so an agent never continues on a silently failed step.
- **`script/*` content handling** — content arriving as JSON, not only as a string, is
  accepted instead of erroring.
- **asmdef writes** — a file path is no longer mistaken for a folder of that name.
- **Text writes no longer emit a UTF-8 BOM** — generated C# and asset files are
  byte-clean for Unity's importers and diff tools.
- **`editor/run_block`** — compile errors are returned to the caller instead of pointing
  at a deleted temp file.

### Added

- **CLI wordmark** — the `create-bridge` installer shows the MOSAIC BRIDGE wordmark on a
  TTY (NO_COLOR respected; piped output unchanged).

## [1.0.0-beta.10] — 2026-08-14

### Added

- **`gameobject/rename`** — there was no way to rename a GameObject at all. `create`, `delete`,
  `duplicate`, `reparent`, `set_active` and `set_transform` existed; rename did not, and it could
  not be reached through `component/set_property` either, because `GameObject` does not derive from
  `Component` and `Transform` has no `name` property.

  The gap surfaced walking a real Unity course through the Editor: *"Rename it to Camera"* was the
  first instruction that could not be performed at all.

  Renames inactive objects too, and **refuses when the new name is already taken** — every other
  tool here addresses objects by name, so two sharing one makes the next lookup pick an arbitrary
  winner. Renaming to its own name succeeds, so re-running a step is not an error.

- **`gameobject/get_info` returns the transform** — `Position`, `LocalPosition`, `Rotation` and
  `LocalScale` as `float[]`, so a caller can sample, act, sample again and subtract. Their absence
  made "did this object actually move?" unanswerable from outside the Editor, which let a recording
  of moving scenery pass as gameplay.

### Fixed

- **`gameobject/get_info` finds inactive objects.** `GameObject.Find` skips them, so an inactive
  object answered "not found" — a lie rather than an answer.

## [1.0.0-beta.9] — 2026-08-14

Tool-reliability release. Three of the four fixes share one shape: **a tool reporting success for
work it did not do**, which a caller cannot detect without independently verifying the world.

### Fixed

- **Unknown parameters are rejected instead of silently ignored.** The generated schema already
  advertised `additionalProperties: false`; the binder did not enforce it, so a misspelt or invented
  parameter was dropped and the call succeeded with a result computed from defaults. The error now
  names the offending parameter *and* the accepted ones.
- **`editor/refresh` reports whether assemblies were actually rebuilt.** It previously returned
  `CompilationFailed: false` when no compile had run at all, which reads as "compiled and current".
  New fields: `AssembliesRebuilt`, `AssembliesUpdatedUtc`, `AutoRefreshEnabled`, `Pending`. When
  nothing rebuilt, nothing is compiling and Auto Refresh is disabled, it fails with
  `STALE_ASSEMBLIES` rather than reporting success.
- **`editor/execute-code` accepts enums as callers write them** — qualified
  (`UnityEditor.ImportAssetOptions.ForceUpdate`) and combined (`A | B`). This was never an arity
  limit; multiple arguments already worked.
- **`/tools` and `/execute` agree about availability during a domain reload.** `/tools` gains
  `count` and `reloading`; a 404 from `/execute` carries `reloading` and `retryable`. The 404
  **status is unchanged by design** — switching to 503 breaks a public contract asserted by
  `ToolRegistryTests`, and belongs in a deliberate version bump.

## [1.0.0-beta.8] — 2026-08-08

### Fixed

- **Hand-authored `.meta` GUIDs collided with other Unity packages and broke consuming
  projects entirely.** 19 GUIDs had been generated by walking hex nibbles arithmetically
  rather than randomly — e.g. `d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3`, whose low nibbles step
  `+1 mod 10` and whose high nibbles run a 6-cycle. That occupies a minuscule share of the
  128-bit space, making collisions near-certain rather than unlucky.

  `com.unity.purchasing` hit two of them. Unity resolves such a clash by dropping the file
  from whichever package loses, and because a package folder is immutable it cannot
  reassign a GUID — so `CatalogCsvLoader.cs` was ignored and the *victim* package then
  failed to compile with `CS0103: The name 'AppleDeviceInfoBuilder' does not exist`. The
  whole project stopped building, and the error named Unity's package rather than the
  bridge, so nobody would suspect this one.

  All 19 regenerated with `uuid.uuid4().hex`. Safe by construction: every affected `.cs`
  is a plain static class, none deriving from `MonoBehaviour` or `ScriptableObject`, so no
  scene, prefab or asset can hold a serialized reference to them — the only thing a script
  GUID change can break. Audited: 1430 `.meta` files, **0 structured and 0 duplicate GUIDs**.

- **`build/build` had never shipped.** The root `.gitignore` carries stock Unity-project
  globs, and line 5's `[Bb]uild/` matched `Editor/Tools/Build/` — so `BuildParams.cs`,
  `BuildPlayerResult.cs` and `BuildTool.cs` were never committed despite existing since
  April. Consuming projects resolved a package containing `Editor/Tools/Build.meta` beside
  an empty directory, and no `build`-family tool appeared among the 250+ the bridge
  exposes. Added a negation for the package path; the sources are now tracked and compile
  clean. The negation has to target the directory — git cannot re-include a file whose
  parent directory is excluded.

### Added

- **CI guards for both of the above.** A check that fails when any package source is
  gitignored — the one that would have caught the missing build tool four months ago — and
  a `.meta` audit rejecting arithmetically-structured or duplicated GUIDs.

## [1.0.0-beta.7] — 2026-08-08

### Fixed

- **HDRP tools now compile.** `HdrpLightTool` used `LightType.Area` with
  `HDAdditionalLightData.SetAreaLightShape`, a pair Unity 2023.2 folded into `LightType`
  itself — the old enum member is obsolete-as-error on 6.5 and `SetAreaLightShape` no
  longer exists. Now sets `LightType.Rectangle` / `Disc` / `Tube` directly, verified
  non-deprecated on 6000.3, 6000.5 and 6000.6.0b5, so no version guard is needed.
  `SetIntensity` and `HDAdditionalLightData.intensity` are likewise deprecated; intensity
  now goes through `LightUnitUtils.ConvertIntensity(light, value, light.lightUnit,
  LightUnitUtils.GetNativeLightUnit(light.type))` — reproducing exactly what `SetIntensity`
  did internally, read out of its IL, so the value is still interpreted in the light's
  configured unit and behaviour is unchanged. With HDRP 17.5 installed the assembly builds
  10 → 6 types with **zero errors and zero warnings**; previously it did not build at all
  for any HDRP user, cascading to every dependent assembly.

- **TextMeshPro tools were permanently dormant.** `MOSAIC_HAS_TMP` was gated on
  `com.unity.textmeshpro >= 3.0.0`, but Unity 6 marks that package deprecated with
  `removeOnProjectUpgrade` — TMP now ships inside `com.unity.ugui`, so the define could
  never be satisfied on any supported editor and the whole category silently never
  registered. Re-gated on `com.unity.ugui >= 2.0.0`. Enabling it exposed a second bug:
  `TmpCreateTool` called `AddComponent<TextMeshPro>()` from inside
  `namespace Mosaic.Bridge.Tools.TextMeshPro`, where the namespace shadows
  `TMPro.TextMeshPro` (CS0118); now fully qualified. The assembly builds 10 types.

### Added

- **`ai/generate-asset` — Unity AI generation, prepared by the bridge and executed through
  Unity's own authorized channel.** Takes a plain-language description plus an asset kind
  (Texture, Material, Sprite, Image, Sound, Animation, Model3D) and returns a ready-to-run
  generation request: command, advisory model ids, save path, and dimensions.

  The bridge deliberately does **not** invoke generation itself. Unity's terms (updated
  2026-06-30) restrict automated callers — naming MCP servers explicitly — from invoking
  Unity offerings without Authorized Agentic Access, with a carve-out for integrations
  working only with the local Editor and project files. Every other bridge tool sits inside
  that carve-out; generation is a paid cloud service and would not. Unity ships its own
  authorized asset-generation MCP, so this tool builds the request and that channel runs it.
  Returns `Mode: "unity-mcp"` when Unity AI is detected, `"handoff"` when it is not.

  What the bridge adds is what Unity's generator cannot see: measured values from the
  bundled PBR knowledge base folded into the prompt, each recorded in `KnowledgeApplied`
  with its source. "wood oak floor planks" becomes a prompt carrying real albedo,
  roughness and metalness rather than the model's guess. Matching prefers the most specific
  entry, so "wood oak" never resolves to `wood_pine`.

  Detection is pure reflection with no compile-time dependency, no asmdef and no manifest
  entry — required, because Unity AI is entitlement-gated and adding it to `manifest.json`
  breaks resolution. A missing or renamed type degrades to `handoff` rather than failing.
  The tool is read-only: it writes nothing, creates no folders, and spends no credits.
  `ConsumesCredits` tells the caller when execution would.


## [1.0.0-beta.6] — 2026-07-26

### Fixed

- **Unity 6.5 (6000.5) compatibility — the package no longer compiles.** Unity 6.5
  replaced the 32-bit instance ID with the 64-bit `UnityEngine.EntityId` and made the
  old entry points obsolete-**as-error** (CS0619), which broke `Mosaic.Bridge.Tools`
  with 315 errors across 93 files: `Object.GetInstanceID()` (312 sites) and
  `SerializedProperty.objectReferenceInstanceIDValue` (3 sites). Because every
  optional integration assembly (`…Tools.Addressables`, `.Splines`, `.URP`, `.HDRP`,
  `.ProBuilder`, `.Cinemachine`, `.TextMeshPro`, `.VisualScripting`) and
  `Mosaic.Bridge.Tests` reference `Mosaic.Bridge.Tools`, none of them were emitted
  either — which is what produced the secondary
  `Mono.Cecil.AssemblyResolutionException: Failed to resolve assembly
  'Mosaic.Bridge.Tools.Addressables'` from the Burst entry-point scanner. That error
  was a symptom, not a separate bug; it clears once compilation succeeds.

  All object-identity calls now route through the new
  `Mosaic.Bridge.Contracts.Compat.UnityIds` shim, which version-guards the engine
  API in one place and keeps `InstanceId` a 32-bit `int` on the MCP wire, so tool
  schemas and clients are unchanged. Also removed the now-deprecated
  `FindObjectsByType<T>(FindObjectsSortMode.None)` (27 sites) and
  `Object.FindObjectOfType<T>()` (5 sites), and version-guarded the
  `FindObjectsByType` call inside the MonoBehaviour that
  `simulation/spherical-gravity` generates.

  `UnityIds.Resolve` rebuilds the 64-bit `EntityId` from the 32-bit id rather than
  using the `int`→`EntityId` implicit operator (a warning on 6.5, a hard **error** on
  6.6), since Unity exposes no public replacement — `EntityId.Parse` and
  `EntityId.From(int)` are both `internal`. Every `EntityId` in a session shares the
  same high 32 bits, so those are read once off a throwaway `ScriptableObject` and
  recombined with the id via the supported `EntityId.ToULong` / `FromULong`. Measured
  on 6000.5.5f1 and 6000.6.0a2: 602 objects spanning scene GameObjects, Components
  and assets all shared one high word and every one round-tripped.
  `UnityIdsTests.EntityId_HighBitsAreSharedAcrossObjectKinds` pins that invariant so
  it fails loudly on the first Unity that changes the layout — the point at which the
  wire format genuinely has to widen (note the raw 64-bit id is ~63× above
  JavaScript's safe-integer limit, so it could not travel as a JSON number anyway).

- **Unity 6.6 `AssetDatabase.ImportPackage` deprecation.** `particle/create` routed
  its two `.unitypackage` imports through the new
  `AssetDatabaseHelper.ImportPackage`, which suppresses the 6.6 deprecation warning in
  one documented place. The suggested replacement, `UnityEditor.AssetPackage.Package`,
  does not exist in 6000.6.0a2 (it landed partway through the 6.6 cycle) and Unity
  emits no define granular enough to branch on, so suppressing a still-working API
  beats guessing a version boundary.

  Verified by full EditMode runs on Unity 6000.3.10f1, 6000.5.5f1 and 6000.6.0a2 —
  each with zero compile errors and zero warnings from this package, all 13 assemblies
  emitted, and no test regressions against the pre-fix baseline. The `Integration` and
  `Regression` categories were additionally run against a live bridge on 6000.3 and
  6000.5, exercising the ids over real HMAC-signed HTTP: all 6 `BridgeIntegrationTests`
  pass on both, along with the `*_smoke.json` fixtures (65 on 6000.5), including the
  id-heavy `scene_get_hierarchy`, `search_by_name`, `search_by_component`,
  `selection_smoke`, `taglayer_smoke` and `terrain_smoke` paths.

  **Not supported: Unity 6000.6.0b5 and later.** It compiles cleanly, but 65 tests fail
  because b5 changed the `EntityId` bit layout: the per-version constant in the high 32
  bits is gone, and those bits now vary per object (measured across 602 objects — 601 at
  `0x0000010000000000`, one at `0x0000070000000000`). A 32-bit id therefore no longer
  identifies an object, and Unity exposes no supported int-to-`EntityId` conversion
  (`EntityId.From(int)` / `Parse` are `internal`; `MarshalFromInstanceId` is a private
  marshalling helper). Every id-based lookup returns null on b5.
  `UnityIdsTests.EntityId_HighBitsAreSharedAcrossObjectKinds` is the test that caught
  this and it fails on b5 by design. Supporting b5+ requires widening the id to 64 bits
  across the MCP wire — and as a *string*, since the raw value is ~63x above
  JavaScript's safe-integer limit.

  Known limitation: Unity **6000.4** is not covered. It compiles and behaves correctly,
  but 6.4 already deprecates `GetInstanceID` (as a warning), and the guards here switch
  at `UNITY_6000_5_OR_NEWER`, so 6.4 builds emit CS0618 warnings. Lowering the guards to
  `UNITY_6000_4_OR_NEWER` looks right on paper — the 6000.4 docs list `EntityId.ToULong`
  / `FromULong` and `objectReferenceEntityIdValue` — but the parameterless
  `FindObjectsByType<T>()` is not documented there, and that was not verified against a
  real 6000.4 install, so the guards were left where they are rather than risk turning
  warnings into a compile failure.

- **`Mosaic.Bridge.Tools.HDRP` asmdef was missing `Unity.RenderPipelines.Core.Runtime`.**
  `HdrpVolumeTool` uses `VolumeComponent` and `VolumeProfile`, which live in that
  assembly; the URP asmdef already referenced it but HDRP did not, so the assembly
  failed with CS0246 for any user who had HDRP installed — cascading to every
  dependent assembly exactly like the 6.5 breakage above. Found only because
  verifying this release was the first time the HDRP tool code had ever been compiled.

### Known issues (all pre-existing, not introduced here)

- **HDRP tools still do not compile.** With the asmdef reference fixed,
  `HdrpLightTool` fails against HDRP 17.5 with 6 errors: `LightType.Area` is
  obsolete-as-error (use `LightType.Rectangle`) and
  `HDAdditionalLightData.SetAreaLightShape` no longer exists. This needs a real HDRP
  light-API migration, and one that stays valid for HDRP 17.0 — which the asmdef's
  `versionDefines` still admit. Left for a focused change.
- **TextMeshPro tools are dormant and do not compile.** `MOSAIC_HAS_TMP` is gated on
  `com.unity.textmeshpro >= 3.0.0`, but Unity 6 marks that package deprecated with
  `removeOnProjectUpgrade` — TMP now ships inside `com.unity.ugui`. The define can
  therefore never be satisfied on any supported editor. Forcing it on reveals a second
  bug: `TmpCreateTool.cs:143` calls `AddComponent<TextMeshPro>()` from inside
  `namespace Mosaic.Bridge.Tools.TextMeshPro`, so the namespace shadows
  `TMPro.TextMeshPro` (CS0118). Re-gating on `com.unity.ugui` requires fixing that too.
- **`SplineCreateTool` has no minimum-knot validation**, but
  `SplinesToolTests.Create_TooFewKnots_ReturnsFail` asserts that it does, so that test
  fails. Either the check is missing or the expectation is wrong.

### Changed

- **`package.json` minimum Unity raised from `6000.0` to `6000.3`** — the oldest editor
  actually verified. The previous claim could not have held: 6000.0–6000.2 have no
  `UnityEngine.EntityId` and no `Resources.EntityIdToObject`, both of which this package
  already depended on unconditionally.

- **MCP server auto-spawn used the wrong package name.** `McpServerProcess` and
  `McpServerPanel` referenced `@mosaic/mcp-server` (which is not the published
  package) for the local-install lookup, the npx spawn arguments, and the
  user-facing log/UI text. Corrected to `@mosaicxr-ai/mcp-server` so auto-spawn
  and local-install detection actually resolve. (`ClaudeCodeConfigurator` already
  checked the correct name first, with `@mosaic` retained only as a legacy fallback.)

## [1.0.0-beta.5] — 2026-05-05

### Added

- **`project/preflight` — dual render pipeline detection.** Now reports BOTH the
  GraphicsSettings default pipeline asset (\`m_CustomRenderPipeline\` —
  Edit → Project Settings → Graphics) AND the per-quality-level QualitySettings
  Render Pipeline Asset (Edit → Project Settings → Quality → Rendering).
  New result fields: \`GraphicsPipelineAsset\`, \`QualityPipelineAsset\`,
  \`ActiveQualityLevel\`, \`PipelineMismatch\` (true when the override differs from
  the default — Quality wins for the active quality level).

- **`project/preflight` — input system reporting.** New result fields:
  \`InputSystem\` (\`Legacy\` / \`New\` / \`Both\`) and \`InputSystemPackageInstalled\`
  (whether \`com.unity.inputsystem\` is in the manifest). Detected via
  \`PlayerSettings.activeInputHandler\` reflection.

### Changed

- **`MaterialCreateTool.DetectRenderPipeline`** now resolves the active pipeline
  using \`QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline\`,
  matching how Unity actually selects the pipeline at runtime. Previous behavior
  could miss quality-level overrides and pick the wrong shader.

- New helper: \`MaterialCreateTool.ClassifyPipeline(RenderPipelineAsset)\` —
  exposed so other tools can classify a specific asset.

---

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
