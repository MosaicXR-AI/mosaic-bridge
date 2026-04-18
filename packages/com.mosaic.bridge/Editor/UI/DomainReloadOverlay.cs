using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Shows a brief notification during domain reloads so the user knows
    /// why tool calls may be temporarily unavailable.
    /// </summary>
    [InitializeOnLoad]
    public static class DomainReloadOverlay
    {
        private static double _reloadStartTime;
        private static bool _isReloading;

        static DomainReloadOverlay()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
        }

        private static void OnBeforeReload()
        {
            _isReloading = true;
            _reloadStartTime = EditorApplication.timeSinceStartup;
        }

        private static void OnAfterReload()
        {
            if (_isReloading)
            {
                _isReloading = false;
                var duration = EditorApplication.timeSinceStartup - _reloadStartTime;
                LastReloadDuration = duration;

                // Show a brief notification with reload duration
                var windows = Resources.FindObjectsOfTypeAll<SceneView>();
                if (windows.Length > 0)
                {
                    windows[0].ShowNotification(
                        new GUIContent($"Mosaic Bridge reloaded ({duration:F1}s)"), 2.0);
                }

                Debug.Log($"[Mosaic.Bridge] Domain reload complete ({duration:F1}s)");
            }
        }

        /// <summary>Whether a domain reload is currently in progress.</summary>
        public static bool IsReloading => _isReloading;

        /// <summary>Duration of the last reload in seconds.</summary>
        public static double LastReloadDuration { get; private set; }
    }
}
