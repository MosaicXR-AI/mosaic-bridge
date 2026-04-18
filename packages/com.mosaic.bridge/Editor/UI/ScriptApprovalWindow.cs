using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Core.Pipeline.Stages;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Dockable Editor window for reviewing pending script approvals.
    /// Shows a scrollable list of pending script/create and script/update operations
    /// with diff preview, countdown timer, and approve/reject actions.
    /// Accessible via Window > Mosaic > Script Approvals.
    /// </summary>
    public class ScriptApprovalWindow : EditorWindow
    {
        [MenuItem("Window/Mosaic/Script Approvals", priority = 8)]
        public static void ShowWindow()
        {
            GetWindow<ScriptApprovalWindow>("Script Approvals");
        }

        private Vector2 _listScrollPos;
        private readonly Dictionary<string, Vector2> _newContentScrollPos = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Vector2> _existingContentScrollPos = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, bool> _expandedItems = new Dictionary<string, bool>();
        private int _repaintCounter;
        private bool _autoRefresh = true;

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
            if (!_autoRefresh) return;

            // Repaint roughly every second to keep countdowns accurate
            if (++_repaintCounter % 60 == 0)
                Repaint();
        }

        private void OnGUI()
        {
            var approvals = ScriptApprovalStage.GetPendingApprovals();

            DrawHeader(approvals.Count);
            EditorGUILayout.Space(4);

            if (approvals.Count == 0)
            {
                DrawEmptyState();
            }
            else
            {
                DrawApprovalList(approvals);
            }

            EditorGUILayout.Space(4);
            DrawFooter(approvals.Count);
        }

        // -----------------------------------------------------------
        // Header
        // -----------------------------------------------------------

        private void DrawHeader(int count)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var label = count > 0
                    ? $"Pending Script Approvals ({count})"
                    : "Pending Script Approvals";
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                var newAutoRefresh = GUILayout.Toggle(_autoRefresh, "Auto-refresh",
                    GUILayout.Width(100));
                if (newAutoRefresh != _autoRefresh)
                    _autoRefresh = newAutoRefresh;
            }
        }

        // -----------------------------------------------------------
        // Empty state
        // -----------------------------------------------------------

        private void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("No pending approvals.",
                    EditorStyles.centeredGreyMiniLabel, GUILayout.Width(200));
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                var enabled = EditorPrefs.GetBool("MosaicBridge.ScriptApprovalEnabled", false);
                var statusText = enabled
                    ? "Script approval is enabled. Pending changes will appear here."
                    : "Script approval is disabled. Enable it in Pipeline Settings.";
                EditorGUILayout.LabelField(statusText,
                    EditorStyles.centeredGreyMiniLabel, GUILayout.Width(400));
                GUILayout.FlexibleSpace();
            }

            GUILayout.FlexibleSpace();
        }

        // -----------------------------------------------------------
        // Approval list
        // -----------------------------------------------------------

        private void DrawApprovalList(IReadOnlyList<PendingApprovalInfo> approvals)
        {
            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);

            for (int i = 0; i < approvals.Count; i++)
            {
                DrawApprovalEntry(approvals[i]);
                if (i < approvals.Count - 1)
                    EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawApprovalEntry(PendingApprovalInfo info)
        {
            var remaining = info.ExpiresAt - DateTime.UtcNow;
            if (remaining.TotalSeconds <= 0)
                return; // expired, will be cleaned up next tick

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Row 1: path + action + countdown
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(info.Path, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    var action = info.ToolName != null && info.ToolName.Contains("create")
                        ? "CREATE"
                        : "UPDATE";
                    var actionColor = action == "CREATE"
                        ? new Color(0.3f, 0.8f, 0.3f)
                        : new Color(0.9f, 0.7f, 0.2f);
                    var oldColor = GUI.color;
                    GUI.color = actionColor;
                    GUILayout.Label(action, EditorStyles.miniLabel, GUILayout.Width(55));
                    GUI.color = oldColor;

                    var mins = (int)remaining.TotalMinutes;
                    var secs = remaining.Seconds;
                    var timeStr = $"{mins}:{secs:D2}";
                    if (remaining.TotalSeconds < 60)
                    {
                        oldColor = GUI.color;
                        GUI.color = Color.red;
                        GUILayout.Label(timeStr, EditorStyles.miniLabel, GUILayout.Width(40));
                        GUI.color = oldColor;
                    }
                    else
                    {
                        GUILayout.Label(timeStr, EditorStyles.miniLabel, GUILayout.Width(40));
                    }
                }

                // Foldout for content preview
                if (!_expandedItems.ContainsKey(info.Token))
                    _expandedItems[info.Token] = true;

                _expandedItems[info.Token] = EditorGUILayout.Foldout(
                    _expandedItems[info.Token], "Content Preview", true);

                if (_expandedItems[info.Token])
                {
                    DrawContentPreview(info);
                }

                EditorGUILayout.Space(4);

                // Action buttons
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Copy Token", GUILayout.Width(90)))
                    {
                        EditorGUIUtility.systemCopyBuffer = info.Token;
                        Debug.Log($"[Mosaic] Approval token copied: {info.Token}");
                    }

                    EditorGUILayout.Space(4);

                    var oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                    if (GUILayout.Button("Approve (Copy Token)", GUILayout.Width(150)))
                    {
                        EditorGUIUtility.systemCopyBuffer = info.Token;
                        Debug.Log($"[Mosaic] Approval token copied: {info.Token}. " +
                                  "Paste it as _approvalToken in your next MCP call to execute.");
                    }
                    GUI.backgroundColor = oldBg;

                    EditorGUILayout.Space(4);

                    GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
                    if (GUILayout.Button("Reject", GUILayout.Width(70)))
                    {
                        if (ScriptApprovalStage.RejectApproval(info.Token))
                        {
                            Debug.Log($"[Mosaic] Approval rejected: {info.Token} ({info.Path})");
                            CleanupScrollState(info.Token);
                        }
                    }
                    GUI.backgroundColor = oldBg;
                }
            }
        }

        // -----------------------------------------------------------
        // Content preview (diff for updates, single pane for creates)
        // -----------------------------------------------------------

        private void DrawContentPreview(PendingApprovalInfo info)
        {
            var isUpdate = info.ToolName != null && info.ToolName.Contains("update");
            string existingContent = null;

            if (isUpdate)
            {
                // Try reading existing file to show side-by-side
                try
                {
                    var fullPath = Path.GetFullPath(info.Path);
                    if (File.Exists(fullPath))
                        existingContent = File.ReadAllText(fullPath);
                }
                catch
                {
                    // Silently ignore read errors
                }
            }

            if (existingContent != null)
            {
                // Side-by-side layout for updates
                EditorGUILayout.LabelField("Existing \u2192 New", EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Existing content
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.47f)))
                    {
                        EditorGUILayout.LabelField("Current File", EditorStyles.centeredGreyMiniLabel);
                        DrawCodeArea(info.Token + "_existing", existingContent,
                            _existingContentScrollPos);
                    }

                    GUILayout.Space(4);

                    // New content
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.47f)))
                    {
                        EditorGUILayout.LabelField("New Content", EditorStyles.centeredGreyMiniLabel);
                        DrawCodeArea(info.Token + "_new", info.Content ?? "",
                            _newContentScrollPos);
                    }
                }
            }
            else
            {
                // Single pane for creates or when existing file not found
                if (isUpdate)
                    EditorGUILayout.LabelField("(Existing file not found on disk)",
                        EditorStyles.centeredGreyMiniLabel);

                DrawCodeArea(info.Token + "_new", info.Content ?? "",
                    _newContentScrollPos);
            }
        }

        private void DrawCodeArea(string key, string content,
            Dictionary<string, Vector2> scrollStore)
        {
            if (!scrollStore.ContainsKey(key))
                scrollStore[key] = Vector2.zero;

            var scroll = scrollStore[key];
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(200));

            // Read-only monospace text area
            var style = new GUIStyle(EditorStyles.textArea)
            {
                font = Font.CreateDynamicFontFromOSFont("Courier New", 12),
                wordWrap = false,
                richText = false
            };

            EditorGUILayout.TextArea(content, style, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            scrollStore[key] = scroll;
        }

        // -----------------------------------------------------------
        // Footer
        // -----------------------------------------------------------

        private void DrawFooter(int count)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (count > 0)
                {
                    GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
                    if (GUILayout.Button("Clear All", GUILayout.Width(80)))
                    {
                        ScriptApprovalStage.ClearPending();
                        _newContentScrollPos.Clear();
                        _existingContentScrollPos.Clear();
                        _expandedItems.Clear();
                        Debug.Log("[Mosaic] All pending approvals cleared.");
                    }
                    GUI.backgroundColor = Color.white;
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Pipeline Settings", EditorStyles.linkLabel,
                    GUILayout.Width(110)))
                {
                    SettingsService.OpenProjectSettings("Project/Mosaic Bridge");
                }
            }
        }

        // -----------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------

        private void CleanupScrollState(string token)
        {
            _newContentScrollPos.Remove(token + "_new");
            _existingContentScrollPos.Remove(token + "_existing");
            _expandedItems.Remove(token);
        }
    }
}
