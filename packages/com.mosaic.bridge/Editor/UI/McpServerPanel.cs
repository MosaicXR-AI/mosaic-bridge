using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Core.Bootstrap;
using Mosaic.Bridge.Core.Runtime;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// MCP Server management panel showing process status,
    /// PID, restart controls, and connection info.
    /// Can be embedded in BridgeDashboardWindow or used standalone.
    /// </summary>
    public static class McpServerPanel
    {
        public static void Draw()
        {
            EditorGUILayout.LabelField("MCP Server", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                var mcpProcess = BridgeBootstrap.McpProcess;
                var isRunning = mcpProcess?.IsRunning ?? false;
                var pid = mcpProcess?.CurrentPid ?? 0;

                // Status indicator
                var statusColor = isRunning ? Color.green : Color.gray;
                var statusText = isRunning ? "Running" : "Not Running";

                using (new EditorGUILayout.HorizontalScope())
                {
                    var old = GUI.color;
                    GUI.color = statusColor;
                    GUILayout.Label("\u25cf", GUILayout.Width(15));
                    GUI.color = old;
                    EditorGUILayout.LabelField("Status", statusText);
                }

                if (isRunning)
                {
                    EditorGUILayout.LabelField("PID", pid.ToString());
                }

                // MCP server path config
                var customPath = EditorPrefs.GetString("Mosaic.Bridge.McpServerPath", "");
                var newPath = EditorGUILayout.TextField("Custom Server Path", customPath);
                if (newPath != customPath)
                    EditorPrefs.SetString("Mosaic.Bridge.McpServerPath", newPath);

                if (string.IsNullOrEmpty(customPath))
                {
                    EditorGUILayout.HelpBox(
                        "No custom path set. MCP server will spawn via npx when @mosaic/mcp-server is installed.",
                        MessageType.Info);
                }

                // Discovery file info
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Discovery File", EditorStyles.miniBoldLabel);
                var discoveryPath = RuntimeDirectoryResolver.GetDiscoveryFilePath();
                using (new EditorGUI.DisabledGroupScope(true))
                {
                    EditorGUILayout.TextField("Path", discoveryPath);
                }

                if (GUILayout.Button("Open Discovery File Location"))
                {
                    var dir = System.IO.Path.GetDirectoryName(discoveryPath);
                    if (System.IO.Directory.Exists(dir))
                        EditorUtility.RevealInFinder(dir);
                }
            }
        }
    }
}
