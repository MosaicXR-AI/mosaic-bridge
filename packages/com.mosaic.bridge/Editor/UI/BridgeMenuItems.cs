using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Core.Bootstrap;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Mosaic Bridge menu items under Tools > Mosaic Bridge.
    /// </summary>
    public static class BridgeMenuItems
    {
        [MenuItem("Window/Mosaic/Status", priority = 1)]
        public static void ShowStatus()
        {
            var state    = BridgeBootstrap.State;
            var port     = BridgeBootstrap.Server?.Port ?? 0;
            var hasTools = BridgeBootstrap.ToolRegistry != null;

            string msg = $"Mosaic Bridge\n\n" +
                         $"State:    {state}\n" +
                         $"Port:     {(port > 0 ? port.ToString() : "—")}\n" +
                         $"Health:   {(port > 0 ? $"http://127.0.0.1:{port}/health" : "—")}\n" +
                         $"Registry: {(hasTools ? "loaded" : "not loaded")}";

            EditorUtility.DisplayDialog("Mosaic Bridge — Status", msg, "OK");
        }

        [MenuItem("Window/Mosaic/Copy Health URL", priority = 2)]
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

        [MenuItem("Window/Mosaic/Restart Bridge", priority = 20)]
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
