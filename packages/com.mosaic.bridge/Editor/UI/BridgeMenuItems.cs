using System;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Core.Bootstrap;
using Mosaic.Bridge.Core.Discovery;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// The Mosaic menu. One top-level menu, one place to look.
    /// </summary>
    /// <remarks>
    /// Items used to be spread over three menus — Mosaic/, Tools/Mosaic Bridge, Tools/Mosaic Pro
    /// and Window/Mosaic — and the first thing a reviewer did was ask which one to open. The
    /// order and the separators are fixed by priority: status and licence, then the capture
    /// shortcuts (Pro adds them at 20–22), then the Bridge's own controls, then setup and
    /// feedback, then diagnostics last. Anything only a developer of Mosaic needs sits behind
    /// MOSAIC_DEV_TOOLS and does not exist in a customer's Editor.
    /// </remarks>
    public static class BridgeMenuItems
    {
        [MenuItem("Mosaic/Status", priority = 1)]
        public static void ShowStatus()
        {
            var state    = BridgeBootstrap.State;
            var port     = BridgeBootstrap.Server?.Port ?? 0;
            var registry = BridgeBootstrap.ToolRegistry;
            var tools    = registry != null ? registry.Count : 0;

            // Who is signed in: the fact that decides whether Pro will run at all, and it
            // was not visible anywhere. The Bridge itself is free and unlimited; Pro's own
            // licence is under Mosaic ▸ Pro Licence when Pro is installed.
            var user = string.IsNullOrEmpty(EditorIdentity.UserName) || EditorIdentity.UserName == "anonymous"
                ? "not signed in to Unity"
                : EditorIdentity.UserName;

            string msg = $"Bridge:    {state}{(port > 0 ? $" on port {port}" : "")}\n" +
                         $"Tools:     {(tools > 0 ? tools.ToString() : "not loaded")}\n" +
                         $"Unity ID:  {user}";

            EditorUtility.DisplayDialog("Mosaic", msg, "OK");
        }

        [MenuItem("Mosaic/Diagnostics/Copy Health URL", priority = 81)]
        public static void CopyHealthUrl()
        {
            var port = BridgeBootstrap.Server?.Port ?? 0;
            if (port == 0)
            {
                EditorUtility.DisplayDialog("Mosaic Bridge", "Bridge is not running.", "OK");
                return;
            }
            var url = $"http://127.0.0.1:{port}/health";
            GUIUtility.systemCopyBuffer = url;
            Debug.Log($"[Mosaic.Bridge] Health URL copied: {url}");
        }

        [MenuItem("Mosaic/Restart Bridge", priority = 42)]
        public static void RestartBridge()
        {
            if (!EditorUtility.DisplayDialog("Mosaic Bridge",
                    "This will stop and restart the Mosaic Bridge server.\nIn-flight requests will be dropped.",
                    "Restart", "Cancel"))
                return;

            BridgeBootstrap.ShutdownForReload();

            // Force a domain reload so the [InitializeOnLoad] static constructor re-runs.
            // AssetDatabase.Refresh() only reloads if scripts changed — RequestScriptReload()
            // always triggers it.
            EditorUtility.RequestScriptReload();
        }
    }
}
