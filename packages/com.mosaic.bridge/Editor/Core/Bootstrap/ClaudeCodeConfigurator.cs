using System;
using System.IO;
using Mosaic.Bridge.Contracts.Interfaces;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Mosaic.Bridge.Core.Bootstrap
{
    /// <summary>
    /// Writes a project-local <c>.mcp.json</c> configuration so that Claude Code (and
    /// other MCP clients that read project-level config) can launch the Mosaic Bridge
    /// MCP server automatically when the user opens the project's directory.
    ///
    /// Behavior:
    ///   * On first run (file does not exist): writes a complete <c>.mcp.json</c> with a
    ///     single <c>mosaic-bridge</c> server pointing at the best available mcp-server path.
    ///   * On subsequent runs (file exists): never overwrites. If the user has customized
    ///     their <c>.mcp.json</c> or removed the <c>mosaic-bridge</c> entry, we respect that.
    ///   * The generated config uses <c>--project-path .</c> so the MCP server targets this
    ///     Unity project specifically, which makes multi-project setups work without extra wiring.
    ///
    /// The user can re-run this via <c>Tools &gt; Mosaic Bridge &gt; Configure Claude Code</c>
    /// to force an overwrite.
    /// </summary>
    public static class ClaudeCodeConfigurator
    {
        private const string McpConfigFileName = ".mcp.json";
        private const string ServerKey = "mosaic-bridge";
        private const string EditorPrefsKey = "Mosaic.Bridge.McpServerPath";

        /// <summary>
        /// Returns the Unity project root (parent of Assets/).
        /// </summary>
        public static string GetProjectRoot()
        {
            return Path.GetDirectoryName(UnityEngine.Application.dataPath);
        }

        /// <summary>
        /// Returns the absolute path to <c>.mcp.json</c> at the project root.
        /// </summary>
        public static string GetMcpConfigPath()
        {
            return Path.Combine(GetProjectRoot(), McpConfigFileName);
        }

        /// <summary>
        /// Writes .mcp.json on first run only. Does nothing if the file already exists.
        /// Returns true if the file was written.
        /// </summary>
        public static bool EnsureConfigOnFirstRun(IMosaicLogger logger)
        {
            try
            {
                var path = GetMcpConfigPath();
                if (File.Exists(path))
                {
                    // File already exists — respect whatever the user or their team has set up.
                    // The user can force regeneration via the menu item.
                    return false;
                }
                return WriteConfig(logger, overwrite: false);
            }
            catch (Exception ex)
            {
                logger?.Warn("Failed to write .mcp.json",
                    ("exception", (object)ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Forces .mcp.json to be (re)written with the current detected mcp-server path.
        /// Adds or replaces the <c>mosaic-bridge</c> entry; preserves any other entries
        /// already present in the file.
        /// </summary>
        [MenuItem("Tools/Mosaic Bridge/Configure Claude Code")]
        public static void WriteConfigMenu()
        {
            // Can't access BridgeBootstrap.Logger directly in all contexts — write with null
            // logger; all issues surface via Unity's console through the static constructor.
            var wrote = WriteConfig(null, overwrite: true);
            var path = GetMcpConfigPath();
            if (wrote)
            {
                EditorUtility.DisplayDialog(
                    "Mosaic Bridge — Claude Code configured",
                    $"Wrote {McpConfigFileName} at:\n{path}\n\nRestart Claude Code to pick up the Mosaic Bridge MCP server for this project.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Mosaic Bridge — Claude Code NOT configured",
                    $"Failed to write {McpConfigFileName} at:\n{path}\n\nCheck the Unity Console for details.",
                    "OK");
            }
        }

        private static bool WriteConfig(IMosaicLogger logger, bool overwrite)
        {
            var path = GetMcpConfigPath();
            var mcpServerPath = DetectMcpServerPath(logger);
            var projectPath = GetProjectRoot();

            JObject root;
            if (File.Exists(path) && !overwrite)
            {
                return false;
            }

            if (File.Exists(path))
            {
                try
                {
                    var existing = File.ReadAllText(path);
                    root = JObject.Parse(existing);
                }
                catch
                {
                    // Malformed existing file — start fresh.
                    root = new JObject();
                }
            }
            else
            {
                root = new JObject();
            }

            // Ensure mcpServers object exists.
            var mcpServers = root["mcpServers"] as JObject;
            if (mcpServers == null)
            {
                mcpServers = new JObject();
                root["mcpServers"] = mcpServers;
            }

            // Build the mosaic-bridge entry. Use "mosaic-mcp" binary name when path detection
            // fails so users with a global npm install still get a working config.
            JObject serverEntry;
            if (!string.IsNullOrEmpty(mcpServerPath))
            {
                serverEntry = new JObject(
                    new JProperty("type", "stdio"),
                    new JProperty("command", "node"),
                    new JProperty("args", new JArray(
                        mcpServerPath,
                        "--project-path",
                        projectPath
                    ))
                );
            }
            else
            {
                serverEntry = new JObject(
                    new JProperty("type", "stdio"),
                    new JProperty("command", "mosaic-mcp"),
                    new JProperty("args", new JArray(
                        "--project-path",
                        projectPath
                    ))
                );
                logger?.Warn(
                    "Could not detect mosaic-mcp dist/index.js; falling back to global 'mosaic-mcp' binary. " +
                    "Install with: npm install -g @mosaicxr-ai/mcp-server");
            }

            mcpServers[ServerKey] = serverEntry;

            try
            {
                File.WriteAllText(path, root.ToString(Newtonsoft.Json.Formatting.Indented));
                logger?.Info("Wrote Claude Code .mcp.json for this project",
                    ("path", (object)path),
                    ("command", (object)(serverEntry["command"]?.ToString() ?? "")),
                    ("projectPath", (object)projectPath));
                return true;
            }
            catch (Exception ex)
            {
                logger?.Warn("Failed to write .mcp.json",
                    ("path", (object)path),
                    ("exception", (object)ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Detects the absolute path to the mcp-server's compiled dist/index.js. Tries,
        /// in order:
        ///   1. EditorPrefs <c>Mosaic.Bridge.McpServerPath</c> (user override)
        ///   2. Sibling directory of this Unity package: <c>../mcp-server/dist/index.js</c>
        ///      (monorepo layout used during development and when installed via file:)
        ///   3. Unity project's local node_modules: <c>./node_modules/@mosaicxr-ai/mcp-server/dist/index.js</c>
        ///   4. Legacy namespace for backward compat: <c>./node_modules/@mosaic/mcp-server/dist/index.js</c>
        ///
        /// Returns <c>null</c> if nothing is found; the caller falls back to the global
        /// <c>mosaic-mcp</c> binary.
        /// </summary>
        private static string DetectMcpServerPath(IMosaicLogger logger)
        {
            // 1. Explicit override.
            var customPath = EditorPrefs.GetString(EditorPrefsKey, "");
            if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
                return customPath;

            // 2. Sibling directory of the Unity package (monorepo layout).
            //    The Unity package lives at <root>/packages/com.mosaic.bridge/
            //    The mcp-server lives at   <root>/packages/mcp-server/
            try
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(ClaudeCodeConfigurator).Assembly);
                if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
                {
                    // resolvedPath points at .../com.mosaic.bridge; go up one, then to mcp-server/dist.
                    var parent = Path.GetDirectoryName(packageInfo.resolvedPath);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        var sibling = Path.Combine(parent, "mcp-server", "dist", "index.js");
                        if (File.Exists(sibling)) return sibling;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Debug("PackageInfo lookup failed — skipping sibling path detection",
                    ("exception", (object)ex.Message));
            }

            // 3. Local npm install under Unity project root (new scope).
            var localNew = Path.Combine(
                GetProjectRoot(), "node_modules", "@mosaicxr-ai", "mcp-server", "dist", "index.js");
            if (File.Exists(localNew)) return localNew;

            // 4. Legacy namespace for backward compat with earlier pre-release installs.
            var localOld = Path.Combine(
                GetProjectRoot(), "node_modules", "@mosaic", "mcp-server", "dist", "index.js");
            if (File.Exists(localOld)) return localOld;

            return null;
        }
    }
}
