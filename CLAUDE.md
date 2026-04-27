# Mosaic Bridge — Claude Code Instructions

## ⚠️ HARD RULE: Free Bridge Only

This repo (`mosaic-bridge`, `github.com/MosaicXR-AI/mosaic-bridge`) is **Apache 2.0 open source**.
Anything committed here becomes public.

**Pro tools belong in `mosaic-pro` (`/Users/mousasoutari/Projects/mosaic-pro`, private).
Never implement Pro tools here.**

## ⚠️ OLD REPO WARNING

`/Users/mousasoutari/Projects/mosaic` (`github.com/MousaSoutari/mosaic`) is the **deprecated** dev sandbox.
Do NOT make changes there. Always work in THIS repo (`mosaic-bridge`) or `mosaic-pro`.

---

## Pro tool categories — DO NOT BUILD HERE:
- `measure/` — distance, angle, area, volume, bounds, section plane
- `annotation/` — 3D annotations
- `analysis/sightline`, `analysis/solar`
- `data/heatmap`, `process/flow`, `process/state`
- `view/explode`, `sensor/`, `timeseries/`
- Anything from epics E33–E44 (industrial, education, USD pipeline)

If asked to implement any of the above, **stop and tell the user to switch to `/Users/mousasoutari/Projects/mosaic-pro` first.**

## Free bridge tool categories (build here):
procgen, ai, pathfinding, physics, mesh, geometry, rendering, shader, spatial,
terrain, animation, audio, ml, networking, optimize, dataviz/chart, dataviz/graph

---

## Repo layout

```
packages/
├── com.mosaic.bridge/       # Unity package
│   ├── Editor/
│   │   ├── Contracts/       # Interfaces, attributes, envelopes
│   │   ├── Core/            # Bootstrap, dispatcher, server, registry
│   │   ├── Knowledge/       # JSON knowledge base files
│   │   └── Tools/           # [MosaicTool] implementations (3 files per tool)
│   └── Tests/
│       ├── Unit/            # NUnit EditMode unit tests
│       └── Regression/      # Live HTTP regression fixtures + runner
├── mcp-server/              # Node.js MCP server (TypeScript)
└── create-bridge/           # npm installer CLI
```

## Tool implementation pattern (3 files + test)

```
Editor/Tools/{Category}/
├── {Action}Params.cs     # [Required] on mandatory fields
├── {Action}Result.cs     # Return data
└── {Action}Tool.cs       # static class, [MosaicTool("category/action", ...)]
Tests/Unit/Tools/{Category}/
└── {Category}ToolTests.cs
```

## Workflow after each story

1. Check Unity console: no errors, no warnings
2. Run full test suite — all should pass
3. BMad code review before commit
