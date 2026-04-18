using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Draws the Pipeline Configuration section inside the Mosaic Bridge settings page.
    /// Reads from and writes to EditorPrefs using the same keys as
    /// <see cref="Core.Pipeline.PipelineConfiguration"/>.
    /// </summary>
    public static class PipelineSettingsSection
    {
        private const string Prefix = "MosaicBridge.";

        // EditorPrefs keys (must match PipelineConfiguration / ScriptApprovalStage)
        private const string KeyDefaultMode       = Prefix + "DefaultExecutionMode";
        private const string KeyCaptureResolution  = Prefix + "CaptureResolution";
        private const string KeyCaptureAngles      = Prefix + "CaptureAngles";
        private const string KeyCodeReviewEnabled  = Prefix + "CodeReviewEnabled";
        private const string KeyCodeReviewRunTests = Prefix + "CodeReviewRunTests";
        private const string KeyScriptApproval     = Prefix + "ScriptApprovalEnabled";

        // Defaults
        private const string DefaultMode          = "direct";
        private const int    DefaultResolution     = 512;
        private const string DefaultAngles         = "front,right,top,perspective";
        private const bool   DefaultCodeReview     = true;
        private const bool   DefaultRunTests       = false;
        private const bool   DefaultScriptApproval = false;

        // Mode dropdown
        private static readonly string[] ModeLabels = { "Direct", "Validated", "Verified", "Reviewed" };

        // Resolution dropdown
        private static readonly string[] ResolutionLabels = { "256", "512", "1024" };
        private static readonly int[]    ResolutionValues  = { 256, 512, 1024 };

        // Capture angle names
        private static readonly string[] AngleNames = { "Front", "Right", "Top", "Perspective" };

        /// <summary>
        /// Draws the full pipeline configuration section.
        /// Call this from a <see cref="SettingsProvider.OnGUI"/> method.
        /// </summary>
        public static void Draw()
        {
            EditorGUILayout.LabelField("Pipeline Settings", EditorStyles.boldLabel);

            DrawExecutionMode();

            EditorGUILayout.Space(6);
            DrawCaptureSettings();

            EditorGUILayout.Space(6);
            DrawCodeReviewSettings();

            EditorGUILayout.Space(6);
            DrawScriptApprovalGate();

            EditorGUILayout.Space(10);
            DrawResetButton();
        }

        // ---------------------------------------------------------------
        //  Default Execution Mode
        // ---------------------------------------------------------------

        private static void DrawExecutionMode()
        {
            var current = EditorPrefs.GetString(KeyDefaultMode, DefaultMode);
            int index = System.Array.FindIndex(ModeLabels, m => m.ToLowerInvariant() == current);
            if (index < 0) index = 0;

            var newIndex = EditorGUILayout.Popup("Default Execution Mode", index, ModeLabels);
            if (newIndex != index)
                EditorPrefs.SetString(KeyDefaultMode, ModeLabels[newIndex].ToLowerInvariant());
        }

        // ---------------------------------------------------------------
        //  Capture Settings (Verified / Reviewed modes)
        // ---------------------------------------------------------------

        private static void DrawCaptureSettings()
        {
            EditorGUILayout.LabelField("Capture Settings", EditorStyles.miniBoldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                // Resolution
                var resolution = EditorPrefs.GetInt(KeyCaptureResolution, DefaultResolution);
                var newResolution = EditorGUILayout.IntPopup("Resolution", resolution,
                    ResolutionLabels, ResolutionValues);
                if (newResolution != resolution)
                    EditorPrefs.SetInt(KeyCaptureResolution, newResolution);

                // Capture angles multi-toggle
                EditorGUILayout.LabelField("Capture Angles");
                var anglesRaw = EditorPrefs.GetString(KeyCaptureAngles, DefaultAngles);
                bool changed = false;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    for (int i = 0; i < AngleNames.Length; i++)
                    {
                        string key = AngleNames[i].ToLowerInvariant();
                        bool isOn = anglesRaw.Contains(key);

                        var style = new GUIStyle(EditorStyles.miniButton)
                        {
                            fixedWidth = 80
                        };

                        // Highlight active toggles
                        var prevColor = GUI.backgroundColor;
                        if (isOn) GUI.backgroundColor = new Color(0.5f, 0.85f, 1f);

                        if (GUILayout.Button(AngleNames[i], style))
                        {
                            isOn = !isOn;
                            changed = true;
                        }

                        GUI.backgroundColor = prevColor;

                        // Update raw string
                        if (changed)
                        {
                            anglesRaw = RebuildAngles(anglesRaw, key, isOn);
                            changed = false;
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                // Persist if any angle toggled
                if (anglesRaw != EditorPrefs.GetString(KeyCaptureAngles, DefaultAngles))
                    EditorPrefs.SetString(KeyCaptureAngles, anglesRaw);
            }
        }

        // ---------------------------------------------------------------
        //  Code Review Settings
        // ---------------------------------------------------------------

        private static void DrawCodeReviewSettings()
        {
            EditorGUILayout.LabelField("Code Review", EditorStyles.miniBoldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                var codeReview = EditorPrefs.GetBool(KeyCodeReviewEnabled, DefaultCodeReview);
                var newCodeReview = EditorGUILayout.Toggle("Enable Code Review", codeReview);
                if (newCodeReview != codeReview)
                    EditorPrefs.SetBool(KeyCodeReviewEnabled, newCodeReview);

                var runTests = EditorPrefs.GetBool(KeyCodeReviewRunTests, DefaultRunTests);
                var newRunTests = EditorGUILayout.Toggle("Run Tests After Script Changes", runTests);
                if (newRunTests != runTests)
                    EditorPrefs.SetBool(KeyCodeReviewRunTests, newRunTests);
            }
        }

        // ---------------------------------------------------------------
        //  Script Approval Gate
        // ---------------------------------------------------------------

        private static void DrawScriptApprovalGate()
        {
            EditorGUILayout.LabelField("Script Approval Gate", EditorStyles.miniBoldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                var approval = EditorPrefs.GetBool(KeyScriptApproval, DefaultScriptApproval);
                var newApproval = EditorGUILayout.Toggle("Enable Script Approval", approval);
                if (newApproval != approval)
                    EditorPrefs.SetBool(KeyScriptApproval, newApproval);
            }
        }

        // ---------------------------------------------------------------
        //  Reset
        // ---------------------------------------------------------------

        private static void DrawResetButton()
        {
            if (GUILayout.Button("Reset Pipeline Settings to Defaults"))
            {
                EditorPrefs.SetString(KeyDefaultMode, DefaultMode);
                EditorPrefs.SetInt(KeyCaptureResolution, DefaultResolution);
                EditorPrefs.SetString(KeyCaptureAngles, DefaultAngles);
                EditorPrefs.SetBool(KeyCodeReviewEnabled, DefaultCodeReview);
                EditorPrefs.SetBool(KeyCodeReviewRunTests, DefaultRunTests);
                EditorPrefs.SetBool(KeyScriptApproval, DefaultScriptApproval);
            }
        }

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        private static string RebuildAngles(string current, string key, bool include)
        {
            var list = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(current))
            {
                foreach (var part in current.Split(','))
                {
                    var trimmed = part.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(trimmed) && trimmed != key)
                        list.Add(trimmed);
                }
            }

            if (include)
                list.Add(key);

            return string.Join(",", list);
        }
    }
}
