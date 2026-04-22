using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Project Settings section for particle pack preferences.
    /// Lets the user choose which pack particle/create uses and guides
    /// them through downloading free Unity Technologies packs.
    /// </summary>
    public static class ParticlePackSection
    {
        // ── EditorPrefs keys ─────────────────────────────────────────────────
        internal const string PrefKey = "MosaicBridge.ParticlePackSource";

        // ── Pack definitions ─────────────────────────────────────────────────
        private static readonly PackDef[] s_Packs =
        {
            new PackDef
            {
                DisplayName    = "Any Installed Pack",
                Description    = "Search ALL particle prefabs in the project regardless of which pack they came from. Works with Unity Particle Pack, Cartoon FX, Synty Studios, custom packs, and more.",
                Price          = null,
                StoreUrl       = null,
                CacheFileNames = null,
                SourceId       = "any"
            },
            new PackDef
            {
                DisplayName  = "Unity Particle Pack",
                Description  = "Modern URP/HDRP-compatible effects: fire, smoke, sparks, magic, distortion. Recommended for Unity 6.",
                Price        = "Free",
                StoreUrl     = "https://assetstore.unity.com/packages/vfx/particles/particle-pack-127325",
                CacheFileNames = new[]
                {
                    Path.Combine("Unity Technologies", "VFX", "Particle Pack.unitypackage"),
                    Path.Combine("Unity Technologies", "Particle Pack.unitypackage"),
                },
                SourceId     = "particle-pack"
            },
            new PackDef
            {
                DisplayName  = "Starter Particle Pack",
                Description  = "Lightweight starter effects. Good for prototyping and smaller projects.",
                Price        = "Free",
                StoreUrl     = "https://assetstore.unity.com/packages/vfx/particles/starter-particle-pack-83179",
                CacheFileNames = new[]
                {
                    Path.Combine("Unity Technologies", "VFX", "Starter Particle Pack.unitypackage"),
                    Path.Combine("Unity Technologies", "Starter Particle Pack.unitypackage"),
                },
                SourceId     = "starter-particle-pack"
            },
            new PackDef
            {
                DisplayName  = "Legacy Particle Pack",
                Description  = "Built-in Render Pipeline only. Use only if you are NOT using URP or HDRP.",
                Price        = "Free",
                StoreUrl     = "https://assetstore.unity.com/packages/vfx/particles/legacy-particle-pack-73777",
                CacheFileNames = new[]
                {
                    Path.Combine("Unity Technologies", "VFX", "Legacy Particle Pack.unitypackage"),
                    Path.Combine("Unity Technologies", "Legacy Particle Pack.unitypackage"),
                },
                SourceId     = "legacy-particle-pack"
            },
            new PackDef
            {
                DisplayName  = "Built-in Presets Only",
                Description  = "Always use Mosaic Bridge's built-in particle presets. No Asset Store dependency. Works in any project.",
                Price        = null,
                StoreUrl     = null,
                CacheFileNames = null,
                SourceId     = "builtin"
            },
        };

        // ── GUI state ─────────────────────────────────────────────────────────
        private static bool s_Foldout = true;
        private static string[] s_CacheBasePaths;

        // ── Public helpers for ParticleCreateTool ─────────────────────────────

        public static string GetDisplayName(string sourceId)
        {
            foreach (var pack in s_Packs)
                if (pack.SourceId == sourceId) return pack.DisplayName;
            return sourceId;
        }

        public static void Draw()
        {
            s_Foldout = EditorGUILayout.BeginFoldoutHeaderGroup(s_Foldout, "Particle Pack Source");
            if (!s_Foldout)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUILayout.Space(4);

            string current = EditorPrefs.GetString(PrefKey, "any");

            EditorGUILayout.HelpBox(
                "When particle/create is called with a preset (fire, rain, smoke…), Mosaic Bridge checks:\n" +
                "1. Project assets — any matching prefab already imported\n" +
                "2. OS download cache — if you downloaded a pack before, it auto-imports\n" +
                "3. Built-in preset — always works, no dependencies\n\n" +
                "Select your preferred source below. Free packs give more polished results.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            foreach (var pack in s_Packs)
            {
                DrawPackRow(pack, ref current);
                EditorGUILayout.Space(4);
            }

            if (EditorPrefs.GetString(PrefKey, "any") != current)
                EditorPrefs.SetString(PrefKey, current);

            EditorGUILayout.Space(6);

            // How-to guide for first-time download
            EditorGUILayout.LabelField("How to get a free pack for the first time:", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "1. Click \"Open Store Page\" — log in to your Unity account and click \"Add to My Assets\" (free, ~10 sec).\n" +
                "2. In Unity: Window > Package Manager > My Assets > find the pack > Download > Import.\n" +
                "3. Done — Mosaic Bridge will detect and use it automatically from now on.",
                MessageType.None);

            if (GUILayout.Button("Open Unity Package Manager  →  My Assets"))
                EditorApplication.ExecuteMenuItem("Window/Package Manager");

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ── Per-pack row ──────────────────────────────────────────────────────

        private static void DrawPackRow(PackDef pack, ref string current)
        {
            bool isSelected = current == pack.SourceId;
            PackStatus status = GetStatus(pack);

            // Row background
            var boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 6, 6) };
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Radio button
                    bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(16));
                    if (newSelected) current = pack.SourceId;

                    // Name + price badge
                    EditorGUILayout.LabelField(pack.DisplayName, EditorStyles.boldLabel);

                    GUILayout.FlexibleSpace();

                    if (pack.Price != null)
                    {
                        var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal = { textColor = new Color(0.2f, 0.7f, 0.3f) }
                        };
                        EditorGUILayout.LabelField(pack.Price, badgeStyle, GUILayout.Width(36));
                    }

                    // Status badge
                    DrawStatusBadge(status);
                }

                // Description
                EditorGUILayout.LabelField(pack.Description, EditorStyles.wordWrappedMiniLabel);

                // Action buttons
                if (pack.StoreUrl != null)
                {
                    EditorGUILayout.Space(4);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (status == PackStatus.NotDownloaded)
                        {
                            if (GUILayout.Button("① Open Store Page", GUILayout.Height(22)))
                                Application.OpenURL(pack.StoreUrl);

                            if (GUILayout.Button("② Open Package Manager", GUILayout.Height(22)))
                                EditorApplication.ExecuteMenuItem("Window/Package Manager");
                        }
                        else if (status == PackStatus.InCache)
                        {
                            string cachePath = FindCachePath(pack);
                            if (GUILayout.Button("Import from Cache  →  Project", GUILayout.Height(22)))
                            {
                                AssetDatabase.ImportPackage(cachePath, false);
                                AssetDatabase.Refresh();
                            }
                        }
                        else if (status == PackStatus.InProject)
                        {
                            using (new EditorGUI.DisabledGroupScope(true))
                                GUILayout.Button("✓ Installed in Project", GUILayout.Height(22));
                        }

                        if (GUILayout.Button("View on Store", GUILayout.Width(90), GUILayout.Height(22)))
                            Application.OpenURL(pack.StoreUrl);
                    }
                }
            }
        }

        private static void DrawStatusBadge(PackStatus status)
        {
            string label;
            Color color;
            switch (status)
            {
                case PackStatus.InProject:
                    label = "● In Project";
                    color = new Color(0.2f, 0.75f, 0.3f);
                    break;
                case PackStatus.InCache:
                    label = "● Cached";
                    color = new Color(0.9f, 0.7f, 0.1f);
                    break;
                case PackStatus.NotDownloaded:
                    label = "○ Not Downloaded";
                    color = new Color(0.6f, 0.6f, 0.6f);
                    break;
                default:
                    label = "● Always Available";
                    color = new Color(0.3f, 0.6f, 0.9f);
                    break;
            }

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = color }
            };
            EditorGUILayout.LabelField(label, style, GUILayout.Width(120));
        }

        // ── Status detection ──────────────────────────────────────────────────

        private enum PackStatus { InProject, InCache, NotDownloaded, BuiltIn }

        private static PackStatus GetStatus(PackDef pack)
        {
            if (pack.CacheFileNames == null) return PackStatus.BuiltIn;

            // Check if the specific pack's marker prefab/folder exists in the project.
            // Each Unity Technologies pack has known folder names after import.
            string[] knownFolders = GetKnownFolders(pack.SourceId);
            foreach (string folder in knownFolders)
            {
                if (AssetDatabase.IsValidFolder(folder)) return PackStatus.InProject;
            }

            // Broader check: any particle prefab with a typical name from this pack
            string[] sampleNames = GetSamplePrefabNames(pack.SourceId);
            foreach (string sample in sampleNames)
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab " + sample);
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null && prefab.GetComponentInChildren<UnityEngine.ParticleSystem>(true) != null)
                        return PackStatus.InProject;
                }
            }

            // Check OS cache
            if (FindCachePath(pack) != null) return PackStatus.InCache;

            return PackStatus.NotDownloaded;
        }

        private static string[] GetKnownFolders(string sourceId)
        {
            switch (sourceId)
            {
                case "particle-pack":         return new[] { "Assets/ParticlePack", "Assets/Particle Pack" };
                case "starter-particle-pack": return new[] { "Assets/StarterParticlePack", "Assets/Starter Particle Pack" };
                case "legacy-particle-pack":  return new[] { "Assets/LegacyParticlePack", "Assets/Legacy Particle Pack" };
                default: return System.Array.Empty<string>();
            }
        }

        private static string[] GetSamplePrefabNames(string sourceId)
        {
            // Representative prefab names that exist in each pack
            switch (sourceId)
            {
                case "particle-pack":         return new[] { "Fire_A", "Smoke_A", "Magic_A", "Distortion_A" };
                case "starter-particle-pack": return new[] { "StarterFire", "StarterSmoke", "StarterSparks" };
                case "legacy-particle-pack":  return new[] { "LegacyFire", "LegacySmoke" };
                default: return System.Array.Empty<string>();
            }
        }

        private static string FindCachePath(PackDef pack)
        {
            if (pack.CacheFileNames == null) return null;

            foreach (string basePath in GetCacheBasePaths())
            {
                if (!Directory.Exists(basePath)) continue;
                foreach (string rel in pack.CacheFileNames)
                {
                    string full = Path.Combine(basePath, rel);
                    if (File.Exists(full)) return full;
                }
            }
            return null;
        }

        private static string[] GetCacheBasePaths()
        {
            if (s_CacheBasePaths != null) return s_CacheBasePaths;

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                s_CacheBasePaths = new[] { Path.Combine(appData, "Unity", "Asset Store-5.x") };
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                s_CacheBasePaths = new[] { Path.Combine(home, "Library", "Unity", "Asset Store-5.x") };
            }
            else
            {
                string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                s_CacheBasePaths = new[] { Path.Combine(home, ".local", "share", "unity3d", "Asset Store-5.x") };
            }

            return s_CacheBasePaths;
        }

        // ── Pack definition ───────────────────────────────────────────────────

        private class PackDef
        {
            public string   DisplayName;
            public string   Description;
            public string   Price;          // null = no badge
            public string   StoreUrl;       // null = built-in, no download needed
            public string[] CacheFileNames; // relative paths inside the OS cache root
            public string   SourceId;       // stored in EditorPrefs
        }
    }
}
