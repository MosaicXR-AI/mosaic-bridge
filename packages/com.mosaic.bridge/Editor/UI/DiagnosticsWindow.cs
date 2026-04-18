using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Core.Diagnostics;

namespace Mosaic.Bridge.UI
{
    public class DiagnosticsWindow : EditorWindow
    {
        [MenuItem("Window/Mosaic/Diagnostics")]
        public static void ShowWindow()
        {
            GetWindow<DiagnosticsWindow>("Mosaic Diagnostics");
        }

        private Vector2 _scrollPos;
        private int _repaintCounter;

        private void OnEnable()
        {
            EditorApplication.update += OnUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
        }

        private void OnUpdate()
        {
            // Repaint every 60th frame (~1 second)
            if (++_repaintCounter % 60 == 0)
                Repaint();
        }

        private void OnGUI()
        {
            // Summary bar
            var summary = ToolCallLogger.GetSummary();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"Total: {summary.TotalCalls}", EditorStyles.boldLabel);
                GUILayout.Label($"Success: {summary.SuccessCount}", EditorStyles.boldLabel);

                var oldColor = GUI.color;
                GUI.color = summary.FailureCount > 0 ? Color.red : Color.green;
                GUILayout.Label($"Failed: {summary.FailureCount}", EditorStyles.boldLabel);
                GUI.color = oldColor;

                GUILayout.Label($"Avg: {summary.AverageDurationMs:F1}ms", EditorStyles.boldLabel);
                GUILayout.Label($"Error Rate: {summary.ErrorRate:F1}%", EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                    ToolCallLogger.Clear();
            }

            EditorGUILayout.Space(5);

            // Recent calls table
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Header
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Time", EditorStyles.boldLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField("Tool", EditorStyles.boldLabel, GUILayout.Width(250));
                EditorGUILayout.LabelField("Status", EditorStyles.boldLabel, GUILayout.Width(60));
                EditorGUILayout.LabelField("Duration", EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField("Error", EditorStyles.boldLabel);
            }

            var records = ToolCallLogger.GetRecords(50);
            // Show newest first
            for (int i = records.Count - 1; i >= 0; i--)
            {
                var r = records[i];
                var oldColor = GUI.color;
                if (!r.IsSuccess) GUI.color = new Color(1f, 0.7f, 0.7f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(r.Timestamp.ToLocalTime().ToString("HH:mm:ss"), GUILayout.Width(70));
                    EditorGUILayout.LabelField(r.ToolName ?? "", GUILayout.Width(250));
                    EditorGUILayout.LabelField(r.StatusCode.ToString(), GUILayout.Width(60));
                    EditorGUILayout.LabelField($"{r.DurationMs:F1}ms", GUILayout.Width(80));
                    EditorGUILayout.LabelField(r.ErrorCode ?? "", GUILayout.MaxWidth(200));
                }

                GUI.color = oldColor;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);

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
