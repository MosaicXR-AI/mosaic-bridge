using System;
using System.IO;
using Mosaic.Bridge.Core.Bootstrap;
using Mosaic.Bridge.Core.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Story 10.3 — Structured in-app feedback window.
    /// Saves feedback as JSON to {RuntimeDir}/feedback/ for support bundle collection.
    /// Does NOT send to a server.
    /// </summary>
    public class FeedbackWindow : EditorWindow
    {
        [MenuItem("Window/Mosaic/Send Feedback", priority = 40)]
        public static void ShowWindow()
        {
            var window = GetWindow<FeedbackWindow>("Mosaic Feedback");
            window.minSize = new Vector2(400, 450);
        }

        private static readonly string[] Categories =
        {
            "Bug Report",
            "Feature Request",
            "General Feedback",
            "Performance Issue"
        };

        private int _categoryIndex;
        private int _rating;
        private string _description = "";
        private bool _includeToolCalls = true;
        private bool _includeSystemInfo = true;
        private Vector2 _scrollPos;
        private bool _submitted;

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Send Feedback", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            if (_submitted)
            {
                EditorGUILayout.HelpBox(
                    "Thank you for your feedback! It has been saved locally and will be included in the next support bundle.",
                    MessageType.Info);

                EditorGUILayout.Space(10);
                if (GUILayout.Button("Submit Another"))
                {
                    _submitted = false;
                    _description = "";
                    _rating = 0;
                }

                EditorGUILayout.EndScrollView();
                return;
            }

            // Category
            _categoryIndex = EditorGUILayout.Popup("Category", _categoryIndex, Categories);

            // Rating (1-5 stars)
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Rating", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 1; i <= 5; i++)
                {
                    var style = new GUIStyle(EditorStyles.miniButton) { fixedWidth = 36 };
                    var prevColor = GUI.backgroundColor;
                    if (i <= _rating) GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);

                    if (GUILayout.Button(i.ToString(), style))
                        _rating = i;

                    GUI.backgroundColor = prevColor;
                }

                GUILayout.FlexibleSpace();
            }

            // Description
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Description", EditorStyles.miniBoldLabel);
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(120));

            // Options
            EditorGUILayout.Space(6);
            _includeToolCalls = EditorGUILayout.Toggle("Include last 10 tool calls", _includeToolCalls);
            _includeSystemInfo = EditorGUILayout.Toggle("Include system info", _includeSystemInfo);

            // Submit
            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledGroupScope(string.IsNullOrWhiteSpace(_description) || _rating == 0))
            {
                if (GUILayout.Button("Submit Feedback", GUILayout.Height(30)))
                {
                    SaveFeedback();
                    _submitted = true;
                }
            }

            if (string.IsNullOrWhiteSpace(_description) || _rating == 0)
            {
                EditorGUILayout.HelpBox("Please provide a rating and description before submitting.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void SaveFeedback()
        {
            var runtimeDir = BridgeBootstrap.RuntimeDirectory;
            if (string.IsNullOrEmpty(runtimeDir))
            {
                Debug.LogWarning("[Mosaic.Bridge] Cannot save feedback: runtime directory not available.");
                return;
            }

            var feedbackDir = Path.Combine(runtimeDir, "feedback");
            Directory.CreateDirectory(feedbackDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var filePath = Path.Combine(feedbackDir, $"feedback-{timestamp}.json");

            var feedback = new JObject
            {
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["category"] = Categories[_categoryIndex],
                ["rating"] = _rating,
                ["description"] = _description
            };

            if (_includeToolCalls)
            {
                var records = ToolCallLogger.GetRecords(10);
                var arr = new JArray();
                foreach (var r in records)
                {
                    arr.Add(new JObject
                    {
                        ["tool"] = r.ToolName,
                        ["status"] = r.StatusCode,
                        ["durationMs"] = r.DurationMs,
                        ["timestamp"] = r.Timestamp.ToString("o"),
                        ["isSuccess"] = r.IsSuccess
                    });
                }
                feedback["recentToolCalls"] = arr;
            }

            if (_includeSystemInfo)
            {
                feedback["systemInfo"] = ReportIssueHelper.CollectSystemInfo();
            }

            File.WriteAllText(filePath, feedback.ToString(Formatting.Indented));

            BridgeBootstrap.Logger?.Info("Feedback saved",
                ("path", (object)filePath),
                ("category", (object)Categories[_categoryIndex]));
        }
    }
}
