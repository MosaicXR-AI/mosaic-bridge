# Engine Port Analysis — Bridge on Unreal / Godot

> **Status: exploratory analysis, no commitment.** Written 2026-08-21 from a full read of this
> repo. Companion doc for the Pro layer: `mosaic-pro/docs/ENGINE-PORT-UNREAL-GODOT.md`.
> There is no engine abstraction layer anywhere in this codebase today; this doc maps what a
> port would actually reuse, rewrite, and redesign.

## TL;DR

The bridge splits ~15% / ~85%. The **protocol layer** (Node MCP server, HMAC auth, discovery,
routing, schema generation) is engine-neutral in design and mostly in code. The **tool surface**
(`Editor/Tools/`, ~57,400 LOC, 292 tools, 82% of the plugin) is direct Unity Editor API calls
with no abstraction — every tool body is a rewrite per engine. A port is therefore "reuse the
architecture, re-author the engine surface," starting from a curated 40–60 tool subset, not 292.

## 1. What the bridge is (porting view)

```
MCP client ⇄ stdio JSON-RPC ⇄ mcp-server (Node, ~1.5k LOC TS)
           ⇄ HTTP on 127.0.0.1, HMAC-SHA256 signed
           ⇄ BridgeServer (C# HttpListener inside the Editor)
           ⇄ MainThreadDispatcher → ExecutionPipeline → ToolRegistry
           ⇄ static [MosaicTool] method (one Params POCO in, ToolResult<T> out)
```

| Component | Size | Port verdict |
|---|---|---|
| `packages/mcp-server/src/` (Node) | ~1.5k LOC | **Reuse as-is** (~60 lines of Unity-shaped resource URIs to re-target) |
| `Editor/Contracts/` | 785 LOC | **Reuse the design, transliterate the code** (only `Compat/UnityIds.cs` is engine-bound) |
| `Editor/Core/` (server, auth, dispatch, discovery, pipeline) | ~9.3k LOC | **Rewrite per engine, same design** — couplings are concentrated in TypeCache, `EditorApplication.update`, `Undo.*`, `SessionState`/`EditorPrefs`, `[InitializeOnLoad]`, `AssemblyReloadEvents` |
| `Editor/Tools/` | ~57.4k LOC, 292 tools | **Rewrite per engine.** DTOs (~2/3 of files) port trivially; tool bodies do not |
| `Editor/UI/` dashboard, onboarding | ~2.8k LOC | Rewrite (IMGUI/UI Toolkit → Slate / Godot Control) |
| `Editor/Knowledge/` KB (18 categories) | JSON | Data is neutral; tool-name cross-references inside entries are not |
| Runtime bridge (`Runtime/`) | ~1.4k LOC | Same pattern, per-engine rewrite |

Tool categories with **portable cores** (algorithm is engine-free, only mesh/texture/terrain I/O
binds to Unity): `procgen/` (17), `ai/` (12), `simulation/` (8), `spatial/` (6), `nav/`,
`mesh/`, `chart/` — roughly 90 tools. These are the cheapest large block to carry over.

## 2. The five design decisions each engine must re-answer

These are where the current code answers a general question with a Unity primitive. Decide them
**before** writing tool #11 — several leak into every DTO.

| Concern | Unity today | Unreal | Godot |
|---|---|---|---|
| Tool discovery | `TypeCache.GetMethodsWithAttribute<MosaicToolAttribute>()` | `UFUNCTION`/`UCLASS` reflection, a generated registry, or a Python decorator registry | C# assembly scan you write yourself; GDScript has no attributes → manifest/convention |
| Main-thread dispatch | `EditorApplication.update` pump, budget 1 write + 5 reads/tick, stall detection, 202 backpressure | `AsyncTask(ENamedThreads::GameThread)` / ticker; re-tune the budgets | `call_deferred` / main-loop hook; re-tune the budgets |
| Undo as transaction | `Undo.IncrementCurrentGroup()` wrapper — one AI action = one Ctrl+Z; 116 files also call `Undo.*` | `FScopedTransaction` — maps cleanly | `EditorUndoRedoManager` — explicit do/undo **pairs per operation**; a real per-tool authoring tax |
| Editor lifecycle | Full domain-reload survival machinery (see §3.1) | No domain reload; Live Coding / Hot Reload have their own semantics | Much simpler; plugin reload on script change |
| Object identity | `EntityId` shim (`Compat/UnityIds.cs`), 32-bit int on the wire | `FSoftObjectPath` / `FGuid` | `ObjectID` / `NodePath` |

## 3. The hard problems (accidental-looking complexity that is actually load-bearing)

1. **Domain reload survival** — the single largest piece to re-derive rather than copy.
   `[InitializeOnLoad]` re-bootstrap, `AssemblyReloadEvents` drain-and-stop, HMAC secret parked
   in `SessionState`, discovery file deliberately kept across reload, `reloading: true /
   retryable: true` responses, Node-side 30s poll-and-retry re-reading discovery for a changed
   port/secret (`bridge-client.ts:waitAndRetry`). UE/Godot have analogous-but-different
   reload semantics; the *retryable-during-reload contract* is the part to keep.
2. **AssetDatabase semantics** — 150 tool files touch it (GUID-stable paths, `Refresh()` and
   its implicit recompile, `t:Type` filters exposed straight through to MCP resources, `.meta`
   files). UE's content model (`UPackage`, AssetRegistry, `/Game/` paths, no sidecars) and
   Godot's (`res://` UIDs, `.import` sidecars) are different enough that the asset tool surface
   is a **redesign, not a translation**.
3. **Compile-and-run-arbitrary-code** — `editor/run-block` writes a temp `[InitializeOnLoad]`
   class, rides the recompile, polls a JobId. UE: Python editor scripting is the sane substrate
   (C++ hot reload is not a reliable automation target). Godot: temp `EditorScript`/tool script —
   quite tractable.
4. **Multi-editor concurrency** — per-project runtime dirs keyed by project-path hash,
   PID-liveness instance registry, and helper-subprocess suppression (Unity's
   `AssetImportWorker` would otherwise fight for the port and delete the discovery file —
   `BridgeBootstrap.IsHelperSubprocess`). Every engine spawns its own helper processes;
   this bug class will be rediscovered.
5. **Pipeline stages** — the pre/post stage chain in `Editor/Core/Pipeline/` ports; four stages
   don't: `VisualVerificationStage`/`SceneCaptureService` (multi-angle screenshots as MCP image
   blocks), `CodeReviewStage` (post-write compile check), `TestRunnerStage`,
   `SemanticValidatorStage` rules.

## 4. Per-engine notes

### Unreal
- **Remote Control API plugin** (HTTP/WebSocket into the editor) may replace much of the
  in-editor HTTP server; evaluate before building `BridgeServer` from scratch.
- **Python editor scripting** is the fastest tool-authoring substrate; C++ for depth where
  Python bindings run out. The language choice shapes discovery, packaging, everything.
- Transactions map well; asset model does not; tutorial framework (`UEditorTutorial`) is
  essentially abandoned (matters for the Pro layer, see companion doc).
- Bigger market, bigger lift.

### Godot
- Cheaper port overall: simple lifecycle, open editor internals (the editor **is** a Godot
  scene — panel capture is easier than Unity's `GUIView.GrabPixels` reflection), first-class
  headless CLI (`godot --headless`).
- Costs: per-op undo authoring tax, no attribute reflection in GDScript (C#/.NET build keeps
  the most code shape), no tutorial framework at all.

## 5. Suggested sequencing (if/when this is ever picked up)

1. Port the skeleton: MCP server re-target + transport + discovery + dispatcher + registry,
   with ~10 proof tools (scene graph create/read, console, screenshot, script create+compile).
2. Decide object identity and the asset-path story **before** scaling the tool count.
3. Curate the v1 tool subset (40–60): scene graph, assets, scripts, play/simulate, console,
   capture, undo, search. Grow from telemetry, not from the 292 list.
4. Carry over the ~90 algorithm tools (procgen/simulation/spatial) by swapping their I/O layer.
5. Re-implement the four engine-bound pipeline stages last — the stage framework runs without
   them.

Pre-port reading: `docs/TOOL-RELIABILITY.md` (catalogs "tool reported success for work it
didn't do" failure modes — every one will recur on a new engine).
