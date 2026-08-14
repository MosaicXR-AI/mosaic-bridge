# Changelog

All notable changes to Mosaic Bridge will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### com.mosaic.bridge 1.0.0-beta.10

- **`gameobject/get_info` returns the transform** — `Position`, `LocalPosition`, `Rotation`,
  `LocalScale` as `float[]`. Their absence made "did this object actually move?" unanswerable from
  outside the Editor, which let a recording of moving scenery pass as gameplay.
- **`gameobject/get_info` finds inactive objects.** `GameObject.Find` skips them, so an inactive
  object answered "not found" — a lie rather than an answer.

Additive only; existing fields are unchanged.

### com.mosaic.bridge 1.0.0-beta.9

Four tool-reliability fixes, all found while driving a live Editor through
`/execute` for several hours. Three share one shape: **a tool reporting success for work it did
not do**, which a caller cannot detect without independently verifying the world afterwards.

- **Unknown parameters are rejected instead of silently ignored.** The generated schema already
  advertised `additionalProperties: false`; the binder did not enforce it, so a misspelt or
  invented parameter was dropped and the call succeeded with a result computed from defaults. The
  error now names the offending parameter *and* the accepted ones.
- **`editor/refresh` reports whether assemblies were actually rebuilt.** It previously returned
  `CompilationFailed: false` when no compile had run at all, which reads as "compiled and current".
  New fields: `AssembliesRebuilt`, `AssembliesUpdatedUtc`, `AutoRefreshEnabled`, `Pending`. When
  nothing rebuilt, nothing is compiling and Auto Refresh is disabled, it fails with
  `STALE_ASSEMBLIES` rather than reporting success.
- **`editor/execute-code` accepts enums as callers actually write them** — qualified
  (`UnityEditor.ImportAssetOptions.ForceUpdate`) and combined (`A | B`). This was never an arity
  limit; multiple arguments already worked.
- **`/tools` and `/execute` agree about availability during a domain reload.** `/tools` gains
  `count` and `reloading`; a 404 from `/execute` now carries `reloading` and `retryable` and
  explains the retry. The 404 **status is unchanged by design** — switching to 503 breaks a
  public contract and belongs in a deliberate version bump.

No breaking changes. `tools` keeps its shape, `/execute` keeps its status codes.

See `docs/TOOL-RELIABILITY.md` for the evidence behind each.

Pre-launch development. Detailed change history will begin with the 1.0.0-beta.1
tag.

### mcp-server 1.0.0-beta.3

- Fix: MCP server on Windows failed to locate the bridge discovery file because
  Node's `path.resolve/join` on win32 produce backslashes while Unity's
  `Application.dataPath` always uses forward slashes. Both sides now hash the
  forward-slash form, so the C# and TS project hashes agree on Windows. This
  was surfacing to users as "Connection closed" in Gemini CLI / Claude Desktop
  with no other diagnostic.
