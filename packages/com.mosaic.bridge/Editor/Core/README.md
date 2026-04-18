# Mosaic.Bridge.Core

Core infrastructure for Mosaic Bridge. Each subfolder corresponds to a
component group.

| Folder | Responsibility |
|---|---|
| `Bridge/` | HttpListener server, lifecycle, discovery file, domain reload handling, runtime directory paths |
| `Dispatcher/` | Main thread queue, `EditorApplication.update` pump, backpressure |
| `Discovery/` | `TypeCache`-based tool discovery, registry caching |
| `Validation/` | Parameter validation against typed parameter classes |
| `Authentication/` | HMAC-SHA256 challenge-response, nonce cache, secret generation |
| `KnowledgeProvider/` | Knowledge base loading and query API |
| `Licensing/` | License validation, OS-native keystore, JWT parsing, trial state |
| `Logging/` | Diagnostic logging with allowlist redaction |
| `Mcp/` | Node.js MCP server child process spawning and lifecycle |
| `Http/` | HTTP request parsing, response writing, rate limiting, routing |
| `ScriptSafety/` | Script tool human-in-the-loop approval and path allowlist |
| `Security/` | Security helpers (canonicalization, redaction policies) |
| `Diagnostics/` | Tool call logger, execution timing, diagnostic collection |
| `Runtime/` | Runtime directory paths, startup status writer, instance registry |
| `Server/` | Rate limiter, bridge server lifecycle |
| `Pipeline/` | Tool execution pipeline, semantic validation, knowledge advisor |
| `Bootstrap/` | Bridge bootstrap (`[InitializeOnLoad]` startup) |
| `Services/` | Service locator helpers |
