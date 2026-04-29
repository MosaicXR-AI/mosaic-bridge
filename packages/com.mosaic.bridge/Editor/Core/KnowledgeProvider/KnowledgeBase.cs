using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Core.Knowledge
{
    /// <summary>
    /// Provides access to curated physics constants and PBR material reference data.
    /// Values are sourced from NIST CODATA 2022 and PhysicallyBased API v2.
    /// Lazy-loaded on first access; cached for subsequent calls.
    /// </summary>
    public static class KnowledgeBase
    {
        // ------------------------------------------------------------------ //
        // Private cache fields
        // ------------------------------------------------------------------ //

        private static JObject _physicsConstants;
        private static JObject _pbrMaterials;
        private static bool _physicsLoaded;
        private static bool _pbrLoaded;

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the full physics constants document as a JObject.
        /// Keys: "constants" (object), plus metadata fields.
        /// Returns null if the asset cannot be found (non-fatal).
        /// </summary>
        public static JObject GetPhysicsConstants()
        {
            EnsurePhysicsLoaded();
            return _physicsConstants;
        }

        /// <summary>
        /// Returns the physics material entry for the given material name
        /// from the physics constants file's "materials" array (case-insensitive).
        /// Returns null if not found.
        /// </summary>
        /// <param name="name">Material name, e.g. "steel", "rubber", "ice".</param>
        public static JToken GetPhysicsMaterial(string name)
        {
            EnsurePhysicsLoaded();
            if (_physicsConstants == null) return null;

            var materials = _physicsConstants["materials"] as JArray;
            if (materials == null) return null;

            foreach (var entry in materials)
            {
                var entryName = entry["name"]?.Value<string>();
                if (string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
            return null;
        }

        /// <summary>
        /// Returns all physics material entries as a JArray.
        /// Returns null if the file cannot be loaded.
        /// </summary>
        public static JArray GetAllPhysicsMaterials()
        {
            EnsurePhysicsLoaded();
            return _physicsConstants?["materials"] as JArray;
        }

        /// <summary>
        /// Returns the PBR material entry for the given material name (case-insensitive).
        /// Supports both array ("materials": [...]) and object ("materials": {...}) layouts.
        /// Returns null if not found.
        /// </summary>
        /// <param name="name">Material name, e.g. "wood_oak", "steel_polished", "glass_clear".</param>
        public static JToken GetPbrMaterial(string name)
        {
            EnsurePbrLoaded();
            if (_pbrMaterials == null) return null;

            var materialsToken = _pbrMaterials["materials"];
            if (materialsToken == null) return null;

            if (materialsToken is JArray array)
            {
                foreach (var entry in array)
                {
                    var entryName = entry["name"]?.Value<string>();
                    if (string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase))
                        return entry;
                }
            }
            else if (materialsToken is JObject obj)
            {
                // Object layout: keys are material names
                foreach (var prop in obj.Properties())
                {
                    if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                        return prop.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns all PBR materials as a JArray.
        /// If the source document uses an object layout, converts to array automatically
        /// (each entry gains a "name" property from the object key).
        /// Returns null if the file cannot be loaded.
        /// </summary>
        public static JArray GetAllPbrMaterials()
        {
            EnsurePbrLoaded();
            if (_pbrMaterials == null) return null;

            var materialsToken = _pbrMaterials["materials"];
            if (materialsToken is JArray array)
                return array;

            if (materialsToken is JObject obj)
            {
                var result = new JArray();
                foreach (var prop in obj.Properties())
                {
                    var entry = (JObject)prop.Value.DeepClone();
                    if (entry["name"] == null)
                        entry.AddFirst(new JProperty("name", prop.Name));
                    result.Add(entry);
                }
                return result;
            }

            return null;
        }

        // ------------------------------------------------------------------ //
        // Lazy-load helpers
        // ------------------------------------------------------------------ //

        private static void EnsurePhysicsLoaded()
        {
            if (_physicsLoaded) return;
            _physicsLoaded = true;

            var asset = LoadData("constants");
            if (asset == null)
            {
                Debug.LogWarning("[Mosaic.Bridge] KnowledgeBase: physics constants asset not found.");
                return;
            }

            try
            {
                _physicsConstants = JObject.Parse(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Mosaic.Bridge] KnowledgeBase: failed to parse physics constants — {ex.Message}");
            }
        }

        private static void EnsurePbrLoaded()
        {
            if (_pbrLoaded) return;
            _pbrLoaded = true;

            var asset = LoadData("pbr-materials");
            if (asset == null)
            {
                Debug.LogWarning("[Mosaic.Bridge] KnowledgeBase: PBR materials asset not found.");
                return;
            }

            try
            {
                _pbrMaterials = JObject.Parse(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Mosaic.Bridge] KnowledgeBase: failed to parse PBR materials — {ex.Message}");
            }
        }

        /// <summary>
        /// Locates a TextAsset by filename within the Mosaic Bridge package,
        /// using AssetDatabase.FindAssets so the lookup works for both local-path
        /// and registry-installed packages.
        /// </summary>
        private static TextAsset LoadData(string filename)
        {
            var guids = AssetDatabase.FindAssets(
                $"{filename} t:TextAsset",
                new[] { "Packages/com.mosaic.bridge" });

            if (guids.Length == 0) return null;

            return AssetDatabase.LoadAssetAtPath<TextAsset>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ------------------------------------------------------------------ //
        // Story 5.2: Additional query methods
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns a single physics constant by key (e.g., "gravity_earth").
        /// Returns null if not found. Never throws.
        /// </summary>
        public static JToken GetConstant(string key)
        {
            EnsurePhysicsLoaded();
            if (_physicsConstants == null || string.IsNullOrEmpty(key)) return null;

            var constants = _physicsConstants["constants"] as JObject;
            return constants?[key];
        }

        /// <summary>
        /// Finds physics materials matching a predicate.
        /// Returns an empty array if none match or KB is unavailable.
        /// </summary>
        public static JArray FindMaterials(Func<JToken, bool> predicate)
        {
            var result = new JArray();
            var all = GetAllPhysicsMaterials();
            if (all == null || predicate == null) return result;

            foreach (var entry in all)
            {
                if (predicate(entry))
                    result.Add(entry);
            }
            return result;
        }

        /// <summary>
        /// Returns contextual summary for a KB category ("physics" or "rendering").
        /// Useful for providing LLMs with category-level context.
        /// </summary>
        public static JObject GetContextForCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return null;

            switch (category.ToLowerInvariant())
            {
                case "physics":
                    EnsurePhysicsLoaded();
                    if (_physicsConstants == null) return null;
                    return new JObject
                    {
                        ["category"] = "physics",
                        ["source"] = _physicsConstants["source"],
                        ["version"] = _physicsConstants["version"],
                        ["constantCount"] = (_physicsConstants["constants"] as JObject)?.Count ?? 0,
                        ["materialCount"] = (_physicsConstants["materials"] as JArray)?.Count ?? 0
                    };

                case "rendering":
                    EnsurePbrLoaded();
                    if (_pbrMaterials == null) return null;
                    return new JObject
                    {
                        ["category"] = "rendering",
                        ["source"] = _pbrMaterials["source"],
                        ["version"] = _pbrMaterials["version"],
                        ["materialCount"] = GetAllPbrMaterials()?.Count ?? 0
                    };

                default:
                    return null;
            }
        }

        // ------------------------------------------------------------------ //
        // Generic KB entry access (entry-schema files: id, title, category…)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Loads a KB entry file by category and key (filename without .json).
        /// E.g. LoadEntry("core", "asset-database") → Editor/Knowledge/core/asset-database.json
        /// </summary>
        public static JObject LoadEntry(string category, string key)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key)) return null;

            var guids = AssetDatabase.FindAssets(
                $"{key} t:TextAsset",
                new[] { $"Packages/com.mosaic.bridge/Editor/Knowledge/{category}" });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith($"{key}.json")) continue;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null) continue;
                try { return JObject.Parse(asset.text); } catch { }
            }
            return null;
        }

        /// <summary>
        /// Returns all KB entry files (those with an "id" field) across all categories,
        /// or within a specific category if provided. Skips reference data files
        /// (constants.json, pbr-materials.json) which use a different schema.
        /// </summary>
        public static JObject[] ListEntries(string category = null)
        {
            var searchPath = category != null
                ? $"Packages/com.mosaic.bridge/Editor/Knowledge/{category}"
                : "Packages/com.mosaic.bridge/Editor/Knowledge";

            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { searchPath });
            var result = new List<JObject>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json")) continue;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null) continue;
                try
                {
                    var obj = JObject.Parse(asset.text);
                    if (obj["id"] != null)
                        result.Add(obj);
                }
                catch { }
            }
            return result.ToArray();
        }

        // ------------------------------------------------------------------ //
        // Cache invalidation (call if data files are reimported at runtime)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Clears the in-memory cache so data is reloaded on next access.
        /// Useful after domain reloads or if JSON assets have been modified.
        /// </summary>
        public static void InvalidateCache()
        {
            _physicsConstants = null;
            _pbrMaterials = null;
            _physicsLoaded = false;
            _pbrLoaded = false;
        }
    }
}
