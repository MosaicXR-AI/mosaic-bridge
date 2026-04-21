# Changelog — com.mosaic.bridge

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
