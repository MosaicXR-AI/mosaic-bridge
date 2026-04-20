# Changelog

All notable changes to Mosaic Bridge will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Pre-launch development. Detailed change history will begin with the 1.0.0-beta.1
tag.

### mcp-server 1.0.0-beta.3

- Fix: MCP server on Windows failed to locate the bridge discovery file because
  Node's `path.resolve/join` on win32 produce backslashes while Unity's
  `Application.dataPath` always uses forward slashes. Both sides now hash the
  forward-slash form, so the C# and TS project hashes agree on Windows. This
  was surfacing to users as "Connection closed" in Gemini CLI / Claude Desktop
  with no other diagnostic.
