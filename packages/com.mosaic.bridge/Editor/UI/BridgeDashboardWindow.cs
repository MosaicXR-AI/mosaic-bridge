using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Core.Bootstrap;
using Mosaic.Bridge.Core.Licensing;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Dockable Editor window showing live Mosaic Bridge status, connection info,
    /// tool registry count, license/trial state, and pipeline configuration.
    /// Accessible via Window > Mosaic > Bridge Dashboard.
    /// </summary>
    public class BridgeDashboardWindow : EditorWindow
    {
        [MenuItem("Window/Mosaic/Bridge Dashboard")]
        public static void ShowWindow()
        {
            GetWindow<BridgeDashboardWindow>("Mosaic Bridge");
        }

        private Vector2 _scrollPos;
        private int _frameCounter;

        private void OnEnable()
        {
            EditorApplication.update += ThrottledRepaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= ThrottledRepaint;
        }

        /// <summary>Repaint every 30th editor update frame to avoid perf overhead.</summary>
        private void ThrottledRepaint()
        {
            if (++_frameCounter >= 30)
            {
                _frameCounter = 0;
                Repaint();
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawConnectionSection();
            EditorGUILayout.Space(10);

            DrawToolsSection();
            EditorGUILayout.Space(10);

            DrawLicenseSection();
            EditorGUILayout.Space(10);

            DrawPipelineSection();
            EditorGUILayout.Space(10);

            DrawReportSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            var state = BridgeBootstrap.State;
            var color = state == BridgeState.Running ? Color.green :
                        state == BridgeState.Error   ? Color.red   : Color.yellow;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var oldColor = GUI.color;
                GUI.color = color;
                GUILayout.Label("\u25cf", GUILayout.Width(20)); // filled circle
                GUI.color = oldColor;

                EditorGUILayout.LabelField($"Mosaic Bridge \u2014 {state}", EditorStyles.boldLabel);
            }
        }

        private void DrawConnectionSection()
        {
            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                var port = BridgeBootstrap.Server?.Port ?? 0;
                EditorGUILayout.LabelField("Port", port > 0 ? port.ToString() : "Not running");
                EditorGUILayout.LabelField("State", BridgeBootstrap.State.ToString());

                var mcpPid = BridgeBootstrap.McpProcess?.CurrentPid ?? 0;
                EditorGUILayout.LabelField("MCP Server PID", mcpPid > 0 ? mcpPid.ToString() : "Not spawned");
            }
        }

        private void DrawToolsSection()
        {
            EditorGUILayout.LabelField("Tool Registry", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                var count = BridgeBootstrap.ToolRegistry?.Count ?? 0;
                EditorGUILayout.LabelField("Registered Tools", count.ToString());
            }
        }

        private void DrawLicenseSection()
        {
            EditorGUILayout.LabelField("License", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                var tier = EditorPrefs.GetString("MosaicBridge.LicenseTier", "trial");
                EditorGUILayout.LabelField("Tier", tier);

                if (tier == "trial")
                {
                    var manager = new TrialManager();
                    EditorGUILayout.LabelField("Days Remaining", manager.TrialDaysRemaining.ToString());
                    EditorGUILayout.LabelField("Daily Quota", $"{manager.DailyQuotaUsed} / {manager.DailyQuota}");

                    if (manager.IsBlocked)
                    {
                        var reason = manager.GetBlockReason();
                        EditorGUILayout.HelpBox(
                            reason == BlockReason.TrialExpired
                                ? "Trial expired. Activate a license to continue."
                                : "Daily quota exhausted. Resets at midnight.",
                            MessageType.Warning);
                    }
                }
            }
        }

        private void DrawPipelineSection()
        {
            EditorGUILayout.LabelField("Pipeline", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                var mode = EditorPrefs.GetString("MosaicBridge.DefaultExecutionMode", "direct");
                EditorGUILayout.LabelField("Default Mode", mode);
                EditorGUILayout.LabelField("Code Review",
                    EditorPrefs.GetBool("MosaicBridge.CodeReviewEnabled", true) ? "Enabled" : "Disabled");
                EditorGUILayout.LabelField("Auto Tests",
                    EditorPrefs.GetBool("MosaicBridge.CodeReviewRunTests", false) ? "Enabled" : "Disabled");
                EditorGUILayout.LabelField("Capture Resolution",
                    EditorPrefs.GetInt("MosaicBridge.CaptureResolution", 512) + "px");
            }
        }

        private void DrawReportSection()
        {
            if (GUILayout.Button("Report Issue", GUILayout.Height(28)))
            {
                var path = ReportIssueHelper.CreateReport();
                if (!string.IsNullOrEmpty(path))
                {
                    EditorUtility.DisplayDialog("Issue Report Saved",
                        $"Report saved to:\n{path}\n\nThis file will be included in support bundles.",
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Report Failed",
                        "Could not save issue report. Check the console for details.",
                        "OK");
                }
            }
        }
    }
}
