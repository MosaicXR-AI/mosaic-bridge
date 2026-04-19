# Mosaic Bridge

A Unity Editor plugin that lets AI agents drive the Unity Editor through the
Model Context Protocol (MCP).

**Status:** Pre-launch. 1.0 beta is in preparation. Public launch materials
(full README, documentation, landing page) will follow.

## Repository layout

```
packages/
├── com.mosaic.bridge/   Unity UPM package (Editor + Runtime + Tests)
└── mcp-server/          Node.js MCP server (TypeScript)
```

## Multi-project support

Mosaic Bridge supports running multiple Unity Editors simultaneously —
each Editor gets its own runtime directory so their state files never
conflict. See the multi-project configuration section below.

### Single-project (one Unity Editor)

No configuration needed. Launch Unity → bridge starts → MCP server
auto-detects the running Editor.

### Multi-project (two or more Unity Editors)

Configure one MCP server entry per project. Each launches with
`--project-path` pointing at the Unity project's root directory.

Example `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "mosaic-bridge-MyGame": {
      "command": "mosaic-mcp",
      "args": ["--project-path", "/Users/me/Projects/MyGame"]
    },
    "mosaic-bridge-Factory": {
      "command": "mosaic-mcp",
      "args": ["--project-path", "/Users/me/Projects/Factory"]
    }
  }
}
```

Claude sees two tool namespaces (`mcp__mosaic-bridge-MyGame__*` and
`mcp__mosaic-bridge-Factory__*`) and routes by the project you mention
in your request (*"in my game project, create a cube"*).

### CLI flags

```
mosaic-mcp [options]

--project-path <path>   Path to Unity project root (recommended)
--project-hash <hash>   16-hex-char project hash (advanced; prefer --project-path)
--runtime-dir <dir>     Explicit per-project runtime directory (bypass hashing)
--discovery-file <file> Direct path to bridge-discovery.json (bypass all path resolution)
-h, --help              Show help
-v, --version           Show version
```

### Auto-detect behavior

When launched without any of the above flags, the MCP server inspects
`instance-registry.json` in the shared base directory:

| Live Editors found | Behavior |
|---|---|
| Zero | Prints "no Unity Editor with Mosaic Bridge detected" with suggested fix. |
| One | Auto-selects it. |
| Two or more | Prints a structured error listing every live project + the flag to specify which one. |

## License

[Apache License 2.0](LICENSE).

## Author

[Mousa Soutari](https://github.com/MousaSoutari) / [MosaicXR](https://mosaicxr.ai)

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for the
Developer Certificate of Origin (DCO) sign-off requirement and workflow.

## Security

See [SECURITY.md](SECURITY.md) for vulnerability disclosure policy and
contact.
