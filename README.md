# Mosaic Bridge

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![Unity 2022 LTS+](https://img.shields.io/badge/Unity-2022%20LTS%2B-000?logo=unity)](https://unity.com)
[![MCP](https://img.shields.io/badge/MCP-2024--11--05-4A90E2.svg)](https://modelcontextprotocol.io/)
[![Release](https://img.shields.io/github/v/release/MosaicXR-AI/mosaic-bridge?include_prereleases&label=release)](https://github.com/MosaicXR-AI/mosaic-bridge/releases)
[![GitHub stars](https://img.shields.io/github/stars/MosaicXR-AI/mosaic-bridge?style=social)](https://github.com/MosaicXR-AI/mosaic-bridge/stargazers)

**AI agents that drive the Unity Editor.**

Mosaic Bridge connects MCP-compliant AI clients — Claude Code, Claude Desktop,
Cursor, Gemini CLI, and any other Model Context Protocol client — to a running
Unity Editor. It exposes ~197 tools covering GameObject and scene operations,
procedural generation, physics simulation, pathfinding, rendering, animation,
and more.

Free and open source under Apache 2.0. No telemetry. Your project never leaves
your machine.

```text
  You:     "Create a 3D tic-tac-toe board with warm wood styling and
            glowing cyan X pieces"
  Claude:  calls mosaic_gameobject_create, mosaic_material_create (uses
            KB for oak albedo), mosaic_physics_add-rigidbody, …
  Unity:   board appears in your scene, ready to play
```

---

## What makes it different

- **Research-paper implementations.** ~90 of the tools are original
  implementations of published CS, graphics, and AI papers — Bridson's Poisson
  Disk (2007), Müller's SPH fluid (2003), Stam's Stable Fluids (1999), Orkin's
  GOAP (F.E.A.R. 2003), Lorensen-Cline Marching Cubes (1987), Reynolds' Boids
  (1987), Harabor-Grastien Jump Point Search (2011), Garland-Heckbert quadric
  decimation (1997). Not wrappers over Unity APIs — actual algorithms, callable
  by AI.

- **Curated knowledge base.** Tools that return physical quantities (density,
  friction, IOR, color temperature) draw from a bundled reference library — NIST
  physics constants, Unity's PhysicallyBased material data. Responses include
  citations. No hallucinated numbers.

- **Multi-Unity-version.** Works on Unity 2022 LTS and Unity 6. Each Editor
  runs in its own per-project runtime directory, so multiple Editors on
  different projects can run concurrently without fighting for ports or files.

- **Privacy-first.** Loopback-only HTTP listener bound to 127.0.0.1,
  HMAC-SHA256 authenticated, telemetry off by default. Ephemeral ports,
  per-project discovery files (mode 0600 on Unix).

- **Vendor-neutral.** Apache 2.0 with explicit patent grant. Not a funnel for a
  paid product. Your tools, your keys, your data.

---

## Requirements

- Unity 2022 LTS or newer (Unity 6 fully supported)
- Node.js 18+ (for the MCP server)
- macOS, Windows, or Linux

---

## Install

Add the Unity package via git URL in your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.mosaic.bridge": "https://github.com/MosaicXR-AI/mosaic-bridge.git?path=/packages/com.mosaic.bridge"
  }
}
```

Clone the repo and build the MCP server:

```bash
git clone https://github.com/MosaicXR-AI/mosaic-bridge.git
cd mosaic-bridge/packages/mcp-server
npm install && npm run build
```

Open your Unity project. The bridge auto-starts, writes a `.mcp.json` to your
project root, and — for Claude Code — auto-detects the config. Restart Claude
Code and you're done.

> **A polished `npx @mosaicxr-ai/create-bridge` installer is in progress.**
> It will configure Claude Code, Claude Desktop, Cursor, and Gemini in one
> interactive wizard. See the [roadmap](#roadmap).

---

## What's inside

~197 tools across 59 categories. A sample:

| Category | Tools | Notable |
|---|---|---|
| Procedural Generation | 17 | Poisson Disk, Marching Cubes, Hydraulic Erosion, Wave Function Collapse, L-Systems, Voronoi, Blue Noise, Perlin / Simplex noise, 3D noise textures |
| Simulation | 8 | SPH fluid, Boids, Stable Fluids / smoke, agent-based (ant colony, slime mold), cloth, orbital mechanics |
| AI Behaviors | 11 | GOAP, Utility AI, Context Steering, Flow Fields, Jump Point Search, Behavior Trees, Steering |
| Advanced Mesh | 10 | Quadric decimation, QuickHull, BSP/CSG boolean, Dual Contouring, icosphere subdivision |
| Advanced Rendering | 12 | Volumetric clouds, atmospheric scattering, ray marching, SDF text, portals |
| Physics | 10 | Rigidbody, collider, raycast, overlap, gravity, physics material, joints |
| Spatial Data Structures | 6 | Spatial hash, k-d tree, octree |
| GameObjects & Scenes | 14 | Create, delete, duplicate, reparent, hierarchy, stats |
| Prefabs & Assets | 12 | Create, instantiate, overrides, variants, import, list |
| Components & Scripts | 9 | Add, remove, set property / reference, create/read/update scripts |
| Animation | 7 | Controllers, states, blend trees, clips, IK setup, transitions |
| Lighting & Graphics | 13 | Lights, baking, shaders, post-processing, shadergraph |
| Cameras & ScreenShots | 6 | Camera info, scene/game/camera screenshots |
| UI | 5 | Canvas, elements, layouts, rect transforms |
| Navigation | 10 | NavMesh, agents, obstacles, pathfinding, FOV visualization, FABRIK IK |
| Terrain | 8 | Height, paint, trees, detail, settings, grid, erosion |
| Package Integrations | ~28 | Cinemachine, ProBuilder, Addressables, TextMeshPro, URP, HDRP, Splines, VisualScripting |

Plus: editor control (play mode, execute code, menu items), profiler, timeline,
input system, constraints, LOD, assembly definitions, reflection, undo/redo,
selection, tags & layers, settings, build, console, measurement, data
visualization.

Full tool list: `Packages/com.mosaic.bridge/Editor/Tools/` after install, or
browse [here](packages/com.mosaic.bridge/Editor/Tools/).

---

## Knowledge base

Tools that return physical values consult a bundled, versioned reference library:

- **`physics/constants.json`** — NIST CODATA fundamental constants
- **`rendering/pbr-materials.json`** — PhysicallyBased API material values (CC0)
- Planned: animation timing, audio attenuation, spatial metrics, color
  temperature, lighting presets

When a tool consults the KB, it attaches a citation to its response envelope so
the AI knows which source a value came from. If a requested value isn't in the
KB, the tool surfaces an explicit "no data available" warning rather than
fabricating a number.

---

## Supported MCP clients

Mosaic Bridge is protocol-native — it speaks [Model Context Protocol
2024-11-05](https://modelcontextprotocol.io/). Tested with:

| Client | Config file | Auto-configured? |
|---|---|---|
| **Claude Code** | `<project>/.mcp.json` | Yes — written on first bridge startup |
| **Claude Desktop** | `~/Library/Application Support/Claude/claude_desktop_config.json` | Coming via installer |
| **Cursor** | `~/.cursor/mcp.json` or project `.cursor/mcp.json` | Coming via installer |
| **Gemini CLI** | `~/.gemini/settings.json` | Coming via installer |
| **Other MCP clients** | See client docs | Manual |

Any MCP 2024-11-05 compliant client should work — these are just the ones with
dedicated auto-config paths planned.

---

## Multi-project: run several Unity Editors at once

Each Editor gets its own runtime directory under
`~/Library/Application Support/Mosaic/Bridge/{projectHash}/` (macOS;
analogous paths on Windows and Linux). State files never collide.

To have one MCP client talking to multiple Unity projects, register one MCP
server per project:

```json
{
  "mcpServers": {
    "mosaic-bridge-game": {
      "command": "node",
      "args": ["/path/to/mcp-server/dist/index.js", "--project-path", "/path/to/MyGame"]
    },
    "mosaic-bridge-factory": {
      "command": "node",
      "args": ["/path/to/mcp-server/dist/index.js", "--project-path", "/path/to/Factory"]
    }
  }
}
```

Each server connects to exactly the specified Editor. When you say *"in my game
project, create a cube"*, the client routes to the correct server by namespace.

Launched without `--project-path`, the server inspects the shared instance
registry:
- Zero live Editors: clear error with setup guidance
- One live Editor: auto-selects it
- Two+ live Editors: structured error listing projects and the flags to
  disambiguate

### MCP server flags

```
mosaic-mcp [options]

  --project-path <path>     Unity project root (recommended)
  --project-hash <hash>     16-hex-char project hash
  --runtime-dir <dir>       Explicit per-project runtime directory
  --discovery-file <file>   Direct path to bridge-discovery.json
  -h, --help                Show help
  -v, --version             Show version
```

---

## Architecture

```
                 Unity Editor (main thread)
                         │
                         │  [MosaicTool] attribute discovery via TypeCache
                         │
                ┌────────▼────────┐
                │  Mosaic Bridge  │   Unity C# package
                │   Core + Tools  │   In-process, main-thread dispatched
                └────────┬────────┘
                         │
                         │  HTTP on loopback (127.0.0.1, ephemeral port)
                         │  HMAC-SHA256 challenge-response authentication
                         │  Discovery file at {project-hash} runtime dir
                         │
                ┌────────▼────────┐
                │  MCP Server     │   Node.js / TypeScript
                │  stdio ↔ MCP    │
                └────────┬────────┘
                         │
                         │  Model Context Protocol over stdio
                         │
      ┌──────────────────┼──────────────────┬──────────────┐
      │                  │                  │              │
  Claude Code      Claude Desktop        Cursor        Gemini CLI
```

**Key properties:**
- In-process bridge — Unity APIs are main-thread-only, cross-process calls
  throw `UnityException`
- Main-thread dispatch via `EditorApplication.update` — guaranteed safe
- Survives Unity domain reloads via `SessionState` persistence
- `TypeCache` discovery — add a static method with `[MosaicTool(...)]`,
  it's automatically callable
- Per-project isolation — multiple Editors never share mutable state files
- Telemetry off by default

---

## Development

```bash
git clone https://github.com/MosaicXR-AI/mosaic-bridge.git
cd mosaic-bridge

# MCP server
cd packages/mcp-server
npm install
npm run build
npm test

# Unity package — install via file: reference in a test project's manifest.json
# See TESTING.md (coming soon) for setup details.
```

Monorepo layout:

```
packages/
├── com.mosaic.bridge/       Unity UPM package (Editor + Runtime + Tests)
│   ├── Editor/              ~197 tools + core infrastructure
│   ├── Runtime/             Runtime-compatible tool subset
│   ├── Tests/               NUnit + Unity Test Runner
│   └── Samples~/            Custom-tool authoring sample
└── mcp-server/              Node.js MCP server (TypeScript + Vitest)
```

Running Unity tests requires opting in via `testables` in the consuming
project's `manifest.json`:

```json
{
  "testables": ["com.mosaic.bridge"]
}
```

---

## Roadmap

### v1.0 beta (current)
- Core bridge, MCP server, ~197 tools
- Per-project runtime isolation
- Auto `.mcp.json` for Claude Code
- Apache 2.0 license with patent grant

### v1.0 stable
- `npx @mosaicxr-ai/create-bridge` — interactive installer for Claude Code,
  Claude Desktop, Cursor, and Gemini
- Unity Asset Store listing
- OpenUPM registry publication
- First docs site

### v1.1+
- Migrate deprecated Unity API usage to modern equivalents
- Expand knowledge base (animation, audio, level design, lighting presets)
- Runtime (compiled build) tool support for more categories
- Performance profiler integration

See [open issues](https://github.com/MosaicXR-AI/mosaic-bridge/issues) for the
live backlog, and [Discussions](https://github.com/MosaicXR-AI/mosaic-bridge/discussions)
for design conversations.

---

## Contributing

Contributions welcome — pull requests, issues, test coverage, docs. Every
commit must be signed off per the [Developer Certificate of Origin](https://developercertificate.org/):

```bash
git commit -s -m "your message"
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full workflow, and
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for community standards.

**Security vulnerabilities:** please disclose privately — see
[SECURITY.md](SECURITY.md).

---

## License

[Apache License 2.0](LICENSE). Includes an explicit patent grant.

Individual research-backed tools cite the papers they implement. Algorithm
citations are embedded in the source code and returned in tool response
envelopes where relevant.

---

## Credits

Built and maintained by [Mousa Soutari](https://github.com/MousaSoutari) /
[MosaicXR](https://mosaicxr.ai).

Research-paper implementations respectfully credit their authors. Knowledge
base values are sourced from public authoritative data (NIST, Unity
PhysicallyBased, etc.) under their respective licenses.

If you build something interesting with Mosaic Bridge, open a
[Discussion](https://github.com/MosaicXR-AI/mosaic-bridge/discussions) — we'd
love to see it.
