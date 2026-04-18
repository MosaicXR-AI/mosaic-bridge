using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Core.Bootstrap;
using Mosaic.Bridge.Core.Runtime;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// First-run onboarding wizard that introduces new users to Mosaic Bridge.
    /// Shows automatically on first Editor load (per <see cref="OnboardingVersion"/>),
    /// and can be reopened via Window > Mosaic > Onboarding Wizard.
    /// </summary>
    public class OnboardingWizard : EditorWindow
    {
        private const string PrefKeyComplete = "MosaicBridge.OnboardingComplete";
        private const string PrefKeyVersion = "MosaicBridge.OnboardingVersion";
        private const int OnboardingVersion = 1;
        private const int PageCount = 4;

        private int _currentPage;
        private bool _dontShowAgain = true;
        private Vector2 _scrollPos;

        // ── Auto-open on first run ──────────────────────────────────────────────

        [InitializeOnLoadMethod]
        private static void CheckFirstRun()
        {
            // Delay so we don't open during bootstrap / domain reload
            EditorApplication.delayCall += () =>
            {
                if (ShouldShowOnboarding())
                    ShowWindow();
            };
        }

        private static bool ShouldShowOnboarding()
        {
            if (EditorPrefs.GetBool(PrefKeyComplete, false))
            {
                // Re-show if onboarding version has been bumped
                int completedVersion = EditorPrefs.GetInt(PrefKeyVersion, 0);
                return completedVersion < OnboardingVersion;
            }
            return true;
        }

        // ── Menu item ───────────────────────────────────────────────────────────

        [MenuItem("Window/Mosaic/Onboarding Wizard", priority = 50)]
        public static void ShowWindow()
        {
            var window = GetWindow<OnboardingWizard>(utility: true, title: "Mosaic Bridge Setup");
            window.minSize = new Vector2(500, 400);
            window.maxSize = new Vector2(500, 400);
            window.ShowUtility();
        }

        // ── GUI ─────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            GUILayout.Space(10);

            switch (_currentPage)
            {
                case 0: DrawWelcomePage(); break;
                case 1: DrawConnectionPage(); break;
                case 2: DrawQuickTourPage(); break;
                case 3: DrawGetStartedPage(); break;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndScrollView();

            DrawNavigationBar();
        }

        // ── Page 1: Welcome ─────────────────────────────────────────────────────

        private void DrawWelcomePage()
        {
            EditorGUILayout.LabelField("Welcome to Mosaic Bridge", EditorStyles.boldLabel);
            GUILayout.Space(8);

            EditorGUILayout.LabelField(
                "Mosaic Bridge connects AI assistants to Unity through the Model Context Protocol (MCP). " +
                "Your AI can now create GameObjects, modify scenes, manage assets, and more \u2014 all through natural language.",
                EditorStyles.wordWrappedLabel);

            GUILayout.Space(16);

            // Bridge status
            EditorGUILayout.LabelField("Bridge Status", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                var state = BridgeBootstrap.State;
                var color = state == BridgeState.Running ? Color.green
                          : state == BridgeState.Error   ? Color.red
                          : Color.yellow;

                using (new EditorGUILayout.HorizontalScope())
                {
                    var oldColor = GUI.color;
                    GUI.color = color;
                    GUILayout.Label("\u25cf", GUILayout.Width(16));
                    GUI.color = oldColor;
                    EditorGUILayout.LabelField(state.ToString());
                }

                var port = BridgeBootstrap.Server?.Port ?? 0;
                EditorGUILayout.LabelField("Port", port > 0 ? port.ToString() : "Not running");
            }
        }

        // ── Page 2: Connection Setup ────────────────────────────────────────────

        private void DrawConnectionPage()
        {
            EditorGUILayout.LabelField("Connection Setup", EditorStyles.boldLabel);
            GUILayout.Space(8);

            EditorGUILayout.LabelField(
                "Configure your MCP client (Claude Desktop, Cursor, etc.) to connect to this endpoint.",
                EditorStyles.wordWrappedLabel);

            GUILayout.Space(12);

            // Discovery file
            EditorGUILayout.LabelField("Discovery File", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                var discoveryPath = RuntimeDirectoryResolver.GetDiscoveryFilePath();
                EditorGUILayout.SelectableLabel(discoveryPath, EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            GUILayout.Space(8);

            // Health URL
            var port = BridgeBootstrap.Server?.Port ?? 8282;
            var healthUrl = $"http://127.0.0.1:{port}/health";

            EditorGUILayout.LabelField("Health URL", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.SelectableLabel(healthUrl, EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            GUILayout.Space(4);

            if (GUILayout.Button("Copy Health URL", GUILayout.Width(140)))
            {
                EditorGUIUtility.systemCopyBuffer = healthUrl;
            }

            GUILayout.Space(12);

            EditorGUILayout.HelpBox(
                "The discovery file contains connection details (port, auth token) that MCP clients " +
                "read automatically. Most clients only need the file path above.",
                MessageType.Info);
        }

        // ── Page 3: Quick Tour ──────────────────────────────────────────────────

        private void DrawQuickTourPage()
        {
            EditorGUILayout.LabelField("Quick Tour", EditorStyles.boldLabel);
            GUILayout.Space(8);

            // Tool categories
            EditorGUILayout.LabelField("Tool Categories", EditorStyles.boldLabel);
            GUILayout.Space(4);

            var categories = GetToolCategoryCounts();
            var totalTools = BridgeBootstrap.ToolRegistry?.Count ?? 0;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var kvp in categories)
                {
                    EditorGUILayout.LabelField(FormatCategoryName(kvp.Key), kvp.Value.ToString() + " tools");
                }

                GUILayout.Space(4);
                EditorGUILayout.LabelField("Total", totalTools.ToString() + " tools", EditorStyles.boldLabel);
            }

            GUILayout.Space(12);

            // Execution modes
            EditorGUILayout.LabelField("Execution Modes", EditorStyles.boldLabel);
            GUILayout.Space(4);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawModeRow("Direct", "Fast execution, no extra checks");
                DrawModeRow("Validated", "Semantic pre-validation before execution");
                DrawModeRow("Verified", "Captures visual proof after execution");
                DrawModeRow("Reviewed", "AI code review for script changes");
            }

            GUILayout.Space(8);

            if (GUILayout.Button("Configure Pipeline in Project Settings", GUILayout.Width(260)))
            {
                SettingsService.OpenProjectSettings("Project/Mosaic Bridge");
            }
        }

        // ── Page 4: Get Started ─────────────────────────────────────────────────

        private void DrawGetStartedPage()
        {
            EditorGUILayout.LabelField("You're All Set!", EditorStyles.boldLabel);
            GUILayout.Space(8);

            EditorGUILayout.LabelField(
                "Mosaic Bridge is running and ready to accept commands from your AI assistant. " +
                "Use the links below to explore further.",
                EditorStyles.wordWrappedLabel);

            GUILayout.Space(16);

            EditorGUILayout.LabelField("Quick Links", EditorStyles.boldLabel);
            GUILayout.Space(4);

            if (GUILayout.Button("Open Dashboard", GUILayout.Width(200)))
            {
                BridgeDashboardWindow.ShowWindow();
            }

            GUILayout.Space(2);

            if (GUILayout.Button("Open Diagnostics", GUILayout.Width(200)))
            {
                EditorApplication.ExecuteMenuItem("Window/Mosaic/Diagnostics");
            }

            GUILayout.Space(2);

            if (GUILayout.Button("View License", GUILayout.Width(200)))
            {
                LicenseStatusPanel.ShowWindow();
            }

            GUILayout.Space(16);

            _dontShowAgain = EditorGUILayout.ToggleLeft("Don't show this again", _dontShowAgain);
        }

        // ── Navigation bar ──────────────────────────────────────────────────────

        private void DrawNavigationBar()
        {
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Page indicator
                GUILayout.Label($"Step {_currentPage + 1} of {PageCount}", EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                // Back button
                using (new EditorGUI.DisabledScope(_currentPage == 0))
                {
                    if (GUILayout.Button("Back", GUILayout.Width(70)))
                        _currentPage--;
                }

                // Next / Finish button
                if (_currentPage < PageCount - 1)
                {
                    if (GUILayout.Button("Next", GUILayout.Width(70)))
                        _currentPage++;
                }
                else
                {
                    if (GUILayout.Button("Finish", GUILayout.Width(70)))
                        CompleteOnboarding();
                }
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private void CompleteOnboarding()
        {
            if (_dontShowAgain)
            {
                EditorPrefs.SetBool(PrefKeyComplete, true);
                EditorPrefs.SetInt(PrefKeyVersion, OnboardingVersion);
            }
            Close();
        }

        private static Dictionary<string, int> GetToolCategoryCounts()
        {
            var counts = new Dictionary<string, int>();

            // Known categories in the Mosaic Bridge toolset
            string[] knownCategories = {
                "gameobject", "component", "scene", "asset", "search",
                "script", "prefab", "material", "console", "editor",
                "selection", "undo", "settings", "test", "build"
            };

            var registry = BridgeBootstrap.ToolRegistry;
            if (registry == null)
                return counts;

            foreach (var cat in knownCategories)
            {
                // Mosaic tool names follow the pattern: mosaic_{category}_{action}
                // Count by checking GetEntry for known tool name prefixes
                // Since we cannot enumerate the private dictionary, we rely on the
                // /tools endpoint pattern. Use the total count as a fallback.
            }

            // Use the GET /tools schema: tool names are mosaic_{category}_{action}
            // We can probe each category by attempting GetEntry for each known tool.
            // Instead, provide the static counts derived from the registered tool set.
            // This keeps the onboarding wizard lightweight and avoids reflection.
            //
            // The actual counts will be shown as a total from ToolRegistry.Count.
            // Per-category breakdown uses known tool routes from the codebase.
            int accounted = 0;
            foreach (var cat in knownCategories)
            {
                int count = CountToolsInCategory(registry, cat);
                if (count > 0)
                {
                    counts[cat] = count;
                    accounted += count;
                }
            }

            // Catch any uncategorized tools
            int total = registry.Count;
            if (accounted < total)
                counts["other"] = total - accounted;

            return counts;
        }

        /// <summary>
        /// Counts tools belonging to a category by probing known tool name patterns.
        /// Tool names follow "mosaic_{category}_{action}" convention.
        /// </summary>
        private static int CountToolsInCategory(Core.Discovery.ToolRegistry registry, string category)
        {
            // Known tool actions per category (derived from MosaicTool routes in the codebase)
            var toolActions = new Dictionary<string, string[]>
            {
                ["gameobject"] = new[] { "create", "delete", "duplicate", "find_by_name", "get_info", "reparent", "set_active", "set_transform" },
                ["component"]  = new[] { "add", "get_properties", "list", "remove", "set_property", "set_reference" },
                ["scene"]      = new[] { "get_hierarchy", "get_info", "get_stats", "new", "open", "save" },
                ["asset"]      = new[] { "create_prefab", "delete", "import", "info", "instantiate_prefab", "list" },
                ["search"]     = new[] { "by_component", "by_layer", "by_name", "by_tag", "missing_references" },
                ["script"]     = new[] { "create", "read", "update" },
                ["prefab"]     = new[] { "apply-overrides", "create", "create-variant", "info", "revert" },
                ["material"]   = new[] { "assign", "create", "set-property" },
                ["console"]    = new[] { "clear", "get-errors", "log" },
                ["editor"]     = new[] { "refresh" },
                ["selection"]  = new[] { "focus-scene-view", "get", "set" },
                ["undo"]       = new[] { "history", "redo", "undo" },
                ["settings"]   = new[] { "get-player", "get-quality", "get-render", "set-player", "set-quality", "set-render" },
                ["test"]       = new[] { "readonly", "run", "throws", "write" },
                ["build"]      = new[] { "build" },
                ["health"]     = new[] { "test" },
            };

            if (!toolActions.TryGetValue(category, out var actions))
                return 0;

            int count = 0;
            foreach (var action in actions)
            {
                var toolName = $"mosaic_{category}_{action}";
                if (registry.GetEntry(toolName) != null)
                    count++;
            }
            return count;
        }

        private static string FormatCategoryName(string category)
        {
            if (string.IsNullOrEmpty(category)) return category;
            // Capitalize first letter
            return char.ToUpperInvariant(category[0]) + category.Substring(1);
        }

        private static void DrawModeRow(string mode, string description)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(mode, EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField("\u2014 " + description, EditorStyles.wordWrappedLabel);
            }
        }
    }
}
