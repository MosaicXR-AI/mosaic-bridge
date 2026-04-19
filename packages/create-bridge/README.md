# @mosaicxr-ai/create-bridge

Interactive installer for [Mosaic Bridge](https://github.com/MosaicXR-AI/mosaic-bridge) — sets up the Unity package and configures MCP for Claude Code, Claude Desktop, Cursor, and Gemini CLI.

## Usage

```bash
npx @mosaicxr-ai/create-bridge
```

That's it. The installer will:

1. Ask for the absolute path to your Unity project (create it first via Unity Hub if you haven't)
2. Validate it's a real Unity project (has `Assets/` + `ProjectSettings/`)
3. Ask which MCP clients to configure
4. Add `com.mosaic.bridge` to `Packages/manifest.json`
5. Write MCP server entries into each client's config
6. Print next steps

Your existing MCP configs are preserved — we merge our server entry in, we don't overwrite anything else.

## Non-interactive mode

Useful for CI or scripted setup:

```bash
npx @mosaicxr-ai/create-bridge \
  --project-path /path/to/UnityProject \
  --clients claude-code,cursor \
  --yes
```

## Flags

| Flag | Description |
|---|---|
| `-p, --project-path <path>` | Unity project root. Skips the interactive prompt. |
| `-c, --clients <list>` | Comma-separated: `claude-code`, `claude-desktop`, `cursor`, `gemini`. |
| `--skip-unity` | Don't modify `Packages/manifest.json` (only write MCP client configs). |
| `--skip-clients` | Don't write MCP client configs (only install the Unity package). |
| `--server-name <name>` | Override the MCP server name. Defaults to `mosaic-bridge-<project>`. |
| `--force` | Overwrite existing MCP client entries for this server (normally skipped). |
| `-y, --yes` | Non-interactive mode. Requires `--project-path`. |
| `-h, --help` | Show help. |
| `-v, --version` | Show version. |

## What gets written where

### Unity project

`<project>/Packages/manifest.json` — adds:

```json
{
  "dependencies": {
    "com.mosaic.bridge": "https://github.com/MosaicXR-AI/mosaic-bridge.git?path=/packages/com.mosaic.bridge"
  }
}
```

### Claude Code

`<project>/.mcp.json` (project-local, auto-loaded by Claude Code when opened in that directory).

### Claude Desktop

User-global file, OS-dependent:

| OS | Path |
|---|---|
| macOS | `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |
| Linux | `~/.config/Claude/claude_desktop_config.json` |

### Cursor

`~/.cursor/mcp.json` (user-global).

### Gemini CLI

`~/.gemini/settings.json` (user-global).

Note: Gemini rejects server names with underscores (tool FQN parser). The installer normalizes all server names to hyphens by default.

## The server entry

All four clients get the same server definition, only the file location differs:

```json
{
  "mcpServers": {
    "mosaic-bridge-<project>": {
      "type": "stdio",
      "command": "npx",
      "args": [
        "-y",
        "@mosaicxr-ai/mcp-server",
        "--project-path",
        "/abs/path/to/your/UnityProject"
      ]
    }
  }
}
```

The `npx -y @mosaicxr-ai/mcp-server` command downloads and caches the MCP server on first run, so users don't need a separate global install.

## After running

1. Open the Unity project in Unity Hub
2. Wait for Unity to import and compile (first time: a few minutes)
3. Restart the MCP clients you configured
4. Open the client, ask it to do something in Unity — the tools will appear in its namespace

## Troubleshooting

**"Directory not found"** — make sure you pass an absolute path and the Unity project exists at that location.

**"Not a Unity project"** — the folder must contain `Assets/` and `ProjectSettings/`. Create the project in Unity Hub first.

**Client shows "Connection closed"** — the Unity Editor must be running for the MCP server to connect. Open Unity, wait for the bridge to initialize, then retry in your client.

**Multiple Unity projects** — configure one server per project (rerun this installer in each project — they use unique server names by default).

## License

[Apache License 2.0](https://github.com/MosaicXR-AI/mosaic-bridge/blob/main/LICENSE)

## Author

[Mousa Soutari](https://github.com/MousaSoutari) / [MosaicXR](https://mosaicxr.ai)
