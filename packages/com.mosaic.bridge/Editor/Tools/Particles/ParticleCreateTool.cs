using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Particles
{
    public static class ParticleCreateTool
    {
        [MosaicTool("particle/create",
                    "Creates a ParticleSystem in the scene. Presets: fire, smoke, sparks, rain, snow, explosion. " +
                    "ALWAYS searches the project first for any matching particle prefab from ANY installed pack " +
                    "(Unity Particle Pack, Starter Pack, Cartoon FX, Synty FX, or any custom pack). " +
                    "Uses keyword aliases so 'fire' finds Fire.prefab, FX_Fire.prefab, CampFire.prefab, Flame.prefab, etc. " +
                    "If nothing found in project, checks the OS Asset Store download cache and auto-imports if found. " +
                    "Falls back to built-in URP-compatible preset only if no prefab exists anywhere. " +
                    "Result: FromPrefab=true + SourcePrefabPath tells you which prefab was used. " +
                    "Set UseExistingPrefab=false to always use built-in presets and skip all prefab detection.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ParticleCreateResult> Execute(ParticleCreateParams p)
        {
            string name = string.IsNullOrEmpty(p.Name) ? "Particle System" : p.Name;
            var position = p.Position != null && p.Position.Length == 3
                ? new Vector3(p.Position[0], p.Position[1], p.Position[2])
                : Vector3.zero;

            // --- Try prefab path first (explicit override) ---
            if (!string.IsNullOrEmpty(p.PrefabPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.PrefabPath);
                if (prefab == null)
                    return ToolResult<ParticleCreateResult>.Fail(
                        $"Prefab not found at '{p.PrefabPath}'", ErrorCodes.NOT_FOUND);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = name;
                go.transform.position = position;
                Undo.RegisterCreatedObjectUndo(go, "Mosaic: Instantiate Particle Prefab");

                return ToolResult<ParticleCreateResult>.Ok(new ParticleCreateResult
                {
                    InstanceId      = go.GetInstanceID(),
                    Name            = go.name,
                    HierarchyPath   = ParticleToolHelpers.GetHierarchyPath(go.transform),
                    Preset          = p.Preset,
                    SourcePrefabPath = p.PrefabPath,
                    FromPrefab      = true
                });
            }

            string presetKey = string.IsNullOrEmpty(p.Preset) ? null : p.Preset.ToLowerInvariant();

            // Read the user's preferred source from Project Settings > Mosaic Bridge
            string preferredSource = EditorPrefs.GetString(
                "MosaicBridge.ParticlePackSource", "any");

            // --- Built-in only: skip all prefab search ---
            if (!p.UseExistingPrefab || preferredSource == "builtin")
                return CreateFromBuiltinPreset(name, position, presetKey);

            if (presetKey != null)
            {
                // --- Specific pack selected: search only within that pack ---
                if (preferredSource != "any")
                {
                    string foundPath = FindInSpecificPack(presetKey, preferredSource);

                    if (foundPath != null)
                        return InstantiatePrefab(foundPath, name, position, presetKey);

                    // Not in project yet — try importing from OS cache for this specific pack
                    string cachedPkg = FindCachedPackById(preferredSource);
                    if (cachedPkg != null)
                    {
                        AssetDatabase.ImportPackage(cachedPkg, false);
                        AssetDatabase.Refresh();
                        foundPath = FindInSpecificPack(presetKey, preferredSource);
                        if (foundPath != null)
                            return InstantiatePrefab(foundPath, name, position, presetKey);
                    }

                    // Specific pack configured but not found anywhere — return clear error
                    string packName = GetPackDisplayName(preferredSource);
                    return ToolResult<ParticleCreateResult>.Fail(
                        $"Preferred pack '{packName}' is not installed and was not found in the " +
                        $"OS Asset Store cache. Either install it (Edit > Project Settings > Mosaic Bridge " +
                        $"> Particle Pack Source) or set UseExistingPrefab=false to use built-in presets.",
                        ErrorCodes.NOT_FOUND);
                }

                // --- "Any" mode: search ALL project prefabs from any pack ---
                {
                    string foundPath = FindParticlePrefabInProject(presetKey);
                    if (foundPath != null)
                        return InstantiatePrefab(foundPath, name, position, presetKey);

                    // Not in project — try any cached pack
                    string cachedPkg = FindCachedParticlePack();
                    if (cachedPkg != null)
                    {
                        AssetDatabase.ImportPackage(cachedPkg, false);
                        AssetDatabase.Refresh();
                        foundPath = FindParticlePrefabInProject(presetKey);
                        if (foundPath != null)
                            return InstantiatePrefab(foundPath, name, position, presetKey);
                    }
                }
            }

            // --- Nothing found anywhere — create from scratch using built-in preset ---
            return CreateFromBuiltinPreset(name, position, presetKey);
        }

        private static ToolResult<ParticleCreateResult> InstantiatePrefab(
            string prefabPath, string name, Vector3 position, string presetKey)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.position = position;
            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Instantiate Particle Prefab");
            return ToolResult<ParticleCreateResult>.Ok(new ParticleCreateResult
            {
                InstanceId       = go.GetInstanceID(),
                Name             = go.name,
                HierarchyPath    = ParticleToolHelpers.GetHierarchyPath(go.transform),
                Preset           = presetKey,
                SourcePrefabPath = prefabPath,
                FromPrefab       = true
            });
        }

        private static ToolResult<ParticleCreateResult> CreateFromBuiltinPreset(
            string name, Vector3 position, string presetKey)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            if (presetKey != null) ApplyPreset(ps, presetKey);
            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create ParticleSystem");
            return ToolResult<ParticleCreateResult>.Ok(new ParticleCreateResult
            {
                InstanceId    = go.GetInstanceID(),
                Name          = go.name,
                HierarchyPath = ParticleToolHelpers.GetHierarchyPath(go.transform),
                Preset        = presetKey,
                FromPrefab    = false
            });
        }

        /// <summary>
        /// Searches the entire project for any prefab that:
        ///   1. Contains a ParticleSystem component (on root or any child)
        ///   2. Has a name matching any keyword alias for the preset
        /// Covers naming conventions from any pack: "Rain.prefab", "FX_Rain.prefab",
        /// "HeavyRain_VFX.prefab", "P_Rain.prefab", "Cartoon_Rain.prefab", etc.
        /// </summary>
        private static string FindParticlePrefabInProject(string presetKey)
        {
            string[] keywords = GetKeywordAliases(presetKey);

            // Try each keyword — broader search catches more naming conventions
            foreach (string kw in keywords)
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab " + kw);
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                    // Name must contain the keyword (avoids false positives from AssetDatabase search)
                    if (!fileName.Contains(kw)) continue;

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null && prefab.GetComponentInChildren<ParticleSystem>(includeInactive: true) != null)
                        return path;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns keyword aliases for a preset so we match naming conventions
        /// across different packs (Unity Particle Pack, Cartoon FX, Synty, custom, etc.)
        /// </summary>
        private static string[] GetKeywordAliases(string preset)
        {
            switch (preset)
            {
                case "fire":      return new[] { "fire", "flame", "torch", "campfire" };
                case "smoke":     return new[] { "smoke", "steam", "mist", "fog" };
                case "rain":      return new[] { "rain", "drizzle", "downpour" };
                case "snow":      return new[] { "snow", "blizzard", "snowfall", "flurry" };
                case "sparks":    return new[] { "spark", "embers", "ember", "cinder" };
                case "explosion": return new[] { "explosion", "explode", "blast", "boom", "detonation" };
                default:          return new[] { preset };
            }
        }

        /// <summary>
        /// Searches only within the known project folders for a specific pack.
        /// Returns null if the pack is not imported (folders don't exist).
        /// </summary>
        private static string FindInSpecificPack(string presetKey, string packId)
        {
            string[] searchFolders = GetPackProjectFolders(packId);
            string[] existingFolders = System.Array.FindAll(
                searchFolders, f => AssetDatabase.IsValidFolder(f));

            if (existingFolders.Length == 0) return null; // pack not imported

            string[] keywords = GetKeywordAliases(presetKey);
            foreach (string kw in keywords)
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab " + kw, existingFolders);
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    if (!fileName.Contains(kw)) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null && prefab.GetComponentInChildren<ParticleSystem>(includeInactive: true) != null)
                        return path;
                }
            }
            return null;
        }

        private static string[] GetPackProjectFolders(string packId)
        {
            switch (packId)
            {
                case "particle-pack":         return new[] { "Assets/ParticlePack", "Assets/Particle Pack" };
                case "starter-particle-pack": return new[] { "Assets/StarterParticlePack", "Assets/Starter Particle Pack" };
                case "legacy-particle-pack":  return new[] { "Assets/LegacyParticlePack", "Assets/Legacy Particle Pack" };
                default: return System.Array.Empty<string>();
            }
        }

        private static string GetPackDisplayName(string sourceId)
        {
            switch (sourceId)
            {
                case "particle-pack":         return "Unity Particle Pack";
                case "starter-particle-pack": return "Starter Particle Pack";
                case "legacy-particle-pack":  return "Legacy Particle Pack";
                case "any":                   return "Any Installed Pack";
                case "builtin":               return "Built-in Presets Only";
                default:                      return sourceId;
            }
        }

        private static string[] GetPackCacheFileNames(string packId)
        {
            switch (packId)
            {
                case "particle-pack":
                    return new[]
                    {
                        Path.Combine("Unity Technologies", "VFX", "Particle Pack.unitypackage"),
                        Path.Combine("Unity Technologies", "Particle Pack.unitypackage"),
                    };
                case "starter-particle-pack":
                    return new[]
                    {
                        Path.Combine("Unity Technologies", "VFX", "Starter Particle Pack.unitypackage"),
                        Path.Combine("Unity Technologies", "Starter Particle Pack.unitypackage"),
                    };
                case "legacy-particle-pack":
                    return new[]
                    {
                        Path.Combine("Unity Technologies", "VFX", "Legacy Particle Pack.unitypackage"),
                        Path.Combine("Unity Technologies", "Legacy Particle Pack.unitypackage"),
                    };
                default: return null;
            }
        }

        /// <summary>
        /// Finds the cached .unitypackage for a specific pack by ID.
        /// </summary>
        private static string FindCachedPackById(string packId)
        {
            string[] cacheFileNames = GetPackCacheFileNames(packId);
            if (cacheFileNames == null) return null;

            foreach (string basePath in GetCacheBasePaths())
            {
                if (!Directory.Exists(basePath)) continue;
                foreach (string rel in cacheFileNames)
                {
                    string full = Path.Combine(basePath, rel);
                    if (File.Exists(full)) return full;
                }
            }
            return null;
        }

        private static string[] GetCacheBasePaths()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                return new[] { Path.Combine(appData, "Unity", "Asset Store-5.x") };
            }
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                return new[] { Path.Combine(home, "Library", "Unity", "Asset Store-5.x") };
            }
            // Linux
            {
                string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                return new[] { Path.Combine(home, ".local", "share", "unity3d", "Asset Store-5.x") };
            }
        }

        /// <summary>
        /// Returns the path of the first known particle .unitypackage found in
        /// Unity's Asset Store OS cache, or null if none found.
        /// </summary>
        private static string FindCachedParticlePack()
        {
            // Unity caches Asset Store downloads here (OS-dependent)
            string[] basePaths;
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                basePaths = new[] { Path.Combine(appData, "Unity", "Asset Store-5.x") };
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                basePaths = new[] { Path.Combine(home, "Library", "Unity", "Asset Store-5.x") };
            }
            else // Linux
            {
                string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                basePaths = new[] { Path.Combine(home, ".local", "share", "unity3d", "Asset Store-5.x") };
            }

            // Known Unity Technologies particle pack filenames (any of these work)
            string[] knownPacks =
            {
                Path.Combine("Unity Technologies", "VFX", "Particle Pack.unitypackage"),
                Path.Combine("Unity Technologies", "VFX", "Starter Particle Pack.unitypackage"),
                Path.Combine("Unity Technologies", "Particle Pack.unitypackage"),
                Path.Combine("Unity Technologies", "Starter Particle Pack.unitypackage"),
            };

            foreach (string basePath in basePaths)
            {
                if (!Directory.Exists(basePath)) continue;
                foreach (string relPath in knownPacks)
                {
                    string full = Path.Combine(basePath, relPath);
                    if (File.Exists(full)) return full;
                }
            }
            return null;
        }

        private static void ApplyPreset(ParticleSystem ps, string preset)
        {
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            // Auto-assign URP-compatible material on all presets to avoid magenta in URP projects
            var urpShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit");
            if (urpShader != null)
                renderer.sharedMaterial = new Material(urpShader);

            switch (preset)
            {
                case "fire":
                    main.duration = 5f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 0.6f, 0f, 1f), new Color(1f, 0.2f, 0f, 1f));
                    main.gravityModifier = -0.2f;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 30f;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 15f;
                    shape.radius = 0.3f;
                    break;

                case "smoke":
                    main.duration = 5f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 2f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(0.5f, 0.5f, 0.5f, 0.6f), new Color(0.3f, 0.3f, 0.3f, 0.3f));
                    main.gravityModifier = -0.05f;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 15f;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 25f;
                    shape.radius = 0.5f;
                    break;

                case "sparks":
                    main.duration = 2f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 0.9f, 0.4f, 1f), new Color(1f, 0.6f, 0.1f, 1f));
                    main.gravityModifier = 1f;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 50f;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.1f;
                    break;

                case "rain":
                    main.duration = 5f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 12f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(0.5f, 0.7f, 1f, 0.9f));
                    main.gravityModifier = 0.5f;
                    main.maxParticles = 5000;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 800f;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(30f, 1f, 30f);
                    // Renderer: Stretched Billboard so particles become rain streaks (not dots)
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.velocityScale = 0.8f;
                    renderer.lengthScale = 3f;
                    renderer.maxParticleSize = 0.5f;
                    if (renderer.sharedMaterial != null)
                        renderer.sharedMaterial.color = new Color(0.5f, 0.7f, 1f, 0.9f);
                    break;

                case "snow":
                    main.duration = 5f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 1f, 1f, 0.9f));
                    main.gravityModifier = 0.1f;
                    main.maxParticles = 3000;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 100f;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(10f, 0f, 10f);
                    break;

                case "explosion":
                    main.duration = 0.5f;
                    main.loop = false;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 1f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 0.8f, 0.2f, 1f), new Color(1f, 0.3f, 0f, 1f));
                    main.gravityModifier = 0.5f;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    emission.rateOverTime = 0f;
                    var burst = new ParticleSystem.Burst(0f, 50);
                    emission.SetBursts(new[] { burst });
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.2f;
                    break;
            }
        }
    }
}
