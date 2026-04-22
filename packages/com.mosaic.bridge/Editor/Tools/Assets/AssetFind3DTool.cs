using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Assets
{
    /// <summary>
    /// Decision gateway before building anything complex from scratch.
    /// Returns IsPrimitive=true for simple shapes so the AI skips searching.
    /// Returns project matches and/or an Asset Store search URL for complex objects.
    /// </summary>
    public static class AssetFind3DTool
    {
        public const string PrefKeyStoreGuidance = "MosaicBridge.AssetStoreGuidance";

        // ── Primitive shape names — build these directly, no search needed ────
        private static readonly HashSet<string> s_Primitives = new HashSet<string>
        {
            "cube", "box", "sphere", "ball", "cylinder", "capsule", "plane", "quad",
            "cone", "prism", "pyramid", "wedge", "torus", "ring", "disc", "disk",
            "pipe", "arch", "stairs", "stair", "door", "icosahedron", "hemisphere"
        };

        [MosaicTool("asset/find-3d",
                    "Decision gateway — call this BEFORE building any complex 3D object. " +
                    "FULL DECISION TREE (follow every step in order): " +
                    "STEP 1 — Primitive check: if IsPrimitive=true → use probuilder/create directly, done. " +
                    "STEP 2 — Project search: if Matches found → use asset/instantiate_prefab, done. " +
                    "STEP 3 — Asset Store (if StoreSearchEnabled=true): show the user StoreSearchUrl, " +
                    "   ask if they want to download an asset from the store. " +
                    "   • User downloads + imports → call asset/find-3d again → instantiate. " +
                    "   • User says no, or nothing suitable on store → go to STEP 4. " +
                    "STEP 4 — Build procedurally: ALWAYS the final fallback. " +
                    "   Use probuilder/create with primitive shapes (Cube, Cylinder, Prism, Plane, etc.) " +
                    "   to assemble the object piece by piece. There is ALWAYS something to build — " +
                    "   no asset in the store does NOT mean the task is impossible. " +
                    "Query: natural language, e.g. 'pirate ship', 'house', 'tree', 'character', 'cannon'. " +
                    "Searches prefabs, FBX, OBJ, DAE in Assets/ with semantic keyword aliases.",
                    isReadOnly: true)]
        public static ToolResult<AssetFind3DResult> Find(AssetFind3DParams p)
        {
            if (string.IsNullOrWhiteSpace(p.Query))
                return ToolResult<AssetFind3DResult>.Ok(new AssetFind3DResult
                {
                    IsPrimitive = false,
                    Matches     = new List<Asset3DMatch>(),
                    Count       = 0,
                    Message     = "Query is empty."
                });

            string query   = p.Query.Trim().ToLowerInvariant();
            int maxResults = p.MaxResults > 0 ? p.MaxResults : 10;

            // ── 1. Primitive check ────────────────────────────────────────────
            if (IsPrimitiveQuery(query))
            {
                return ToolResult<AssetFind3DResult>.Ok(new AssetFind3DResult
                {
                    IsPrimitive = true,
                    Matches     = new List<Asset3DMatch>(),
                    Count       = 0,
                    Message     = $"'{p.Query}' is a primitive shape. Use probuilder/create or " +
                                  "gameobject/create-primitive directly — no asset search needed."
                });
            }

            // ── 2. Search project ─────────────────────────────────────────────
            string[] keywords = ExpandKeywords(query);
            string[] typeFilters = { "t:Prefab", "t:Model" };

            var seen    = new HashSet<string>();
            var exact   = new List<Asset3DMatch>();
            var partial = new List<Asset3DMatch>();

            foreach (string typeFilter in typeFilters)
            {
                foreach (string kw in keywords)
                {
                    foreach (string guid in AssetDatabase.FindAssets(typeFilter + " " + kw))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!seen.Add(path)) continue;
                        if (path.StartsWith("Packages/")) continue;

                        string ext = Path.GetExtension(path).ToLowerInvariant();
                        if (!Is3DAsset(ext)) continue;

                        string filename = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                        bool isExact    = filename.Contains(query) ||
                                          query.Split(' ').All(w => filename.Contains(w));

                        var match = new Asset3DMatch
                        {
                            Path        = path,
                            Name        = Path.GetFileNameWithoutExtension(path),
                            Type        = ext == ".prefab" ? "Prefab" : "Model",
                            Extension   = ext,
                            HasRenderer = HasRenderer(path, ext)
                        };

                        if (isExact) exact.Add(match);
                        else         partial.Add(match);
                    }
                }
            }

            var combined = exact.Concat(partial).Take(maxResults).ToList();

            // ── 3. Store guidance ─────────────────────────────────────────────
            bool storeEnabled  = EditorPrefs.GetBool(PrefKeyStoreGuidance, true);
            string storeUrl    = BuildStoreUrl(query);
            string storeAdvice = storeEnabled
                ? $"Nothing found in project. STEP 2: Check the Asset Store (free assets): {storeUrl}\n" +
                  "Show this URL to the user and ask if they want to download an asset.\n" +
                  "• If yes → they download + import, then call asset/find-3d again to locate it.\n" +
                  "• If no, or nothing suitable on the store → STEP 3: build procedurally.\n" +
                  "STEP 3 (build procedurally): Use probuilder/create with individual primitive shapes " +
                  "(Cube for body/hull, Cylinder for masts/pillars, Prism for roofs, Plane for sails/floors, etc.). " +
                  "Break the object into parts — each probuilder/create call makes one primitive piece."
                : "Nothing found in project. Asset Store guidance is disabled.\n" +
                  "Proceed to build procedurally: use probuilder/create with individual primitive shapes " +
                  "(Cube for body/hull, Cylinder for masts/pillars, Prism for roofs, Plane for sails/floors, etc.). " +
                  "Break the object into parts — each probuilder/create call makes one primitive piece.";

            string message = combined.Count > 0
                ? $"Found {combined.Count} matching asset(s) in project. " +
                  "Use asset/instantiate_prefab with the Path to place one."
                : storeAdvice;

            return ToolResult<AssetFind3DResult>.Ok(new AssetFind3DResult
            {
                IsPrimitive       = false,
                Matches           = combined,
                Count             = combined.Count,
                StoreSearchUrl    = storeEnabled && combined.Count == 0 ? storeUrl : null,
                StoreSearchEnabled = storeEnabled,
                Message           = message
            });
        }

        // ── Primitive detection ───────────────────────────────────────────────

        private static bool IsPrimitiveQuery(string query)
        {
            // Match if the entire query (or any word in it) is a known primitive
            if (s_Primitives.Contains(query)) return true;
            foreach (string word in query.Split(' '))
                if (s_Primitives.Contains(word)) return true;
            return false;
        }

        // ── Keyword expansion ─────────────────────────────────────────────────

        private static string[] ExpandKeywords(string query)
        {
            var kws = new List<string>(query.Split(' '));

            var aliases = new Dictionary<string, string[]>
            {
                { "ship",      new[] { "ship", "boat", "vessel", "galleon", "schooner", "frigate", "sailboat" } },
                { "pirate",    new[] { "pirate", "buccaneer", "corsair" } },
                { "house",     new[] { "house", "home", "building", "cottage", "cabin", "dwelling" } },
                { "tree",      new[] { "tree", "oak", "pine", "palm", "birch", "cedar", "willow" } },
                { "car",       new[] { "car", "vehicle", "auto", "sedan", "truck" } },
                { "character", new[] { "character", "hero", "player", "npc", "humanoid", "person" } },
                { "sword",     new[] { "sword", "blade", "katana", "saber" } },
                { "gun",       new[] { "gun", "weapon", "pistol", "rifle", "firearm" } },
                { "cannon",    new[] { "cannon", "mortar", "artillery" } },
                { "chest",     new[] { "chest", "treasure", "loot", "crate" } },
                { "dragon",    new[] { "dragon", "wyvern", "drake" } },
                { "castle",    new[] { "castle", "fortress", "citadel", "keep", "tower" } },
                { "rock",      new[] { "rock", "stone", "boulder" } },
                { "fence",     new[] { "fence", "railing", "barrier" } },
                { "bridge",    new[] { "bridge", "overpass", "walkway" } },
                { "village",   new[] { "village", "town", "settlement", "hamlet" } },
            };

            foreach (string word in query.Split(' '))
                if (aliases.TryGetValue(word, out string[] synonyms))
                    kws.AddRange(synonyms);

            return kws.Distinct().ToArray();
        }

        // ── Asset Store URL ───────────────────────────────────────────────────

        private static string BuildStoreUrl(string query)
        {
            string encoded = UnityEngine.Networking.UnityWebRequest.EscapeURL(query);
            return "https://assetstore.unity.com/search#q=" + encoded + "&orderBy=1&free=true";
        }

        // ── Asset type helpers ────────────────────────────────────────────────

        private static readonly HashSet<string> s_ModelExtensions =
            new HashSet<string> { ".fbx", ".obj", ".dae", ".3ds", ".blend", ".gltf", ".glb" };

        private static bool Is3DAsset(string ext) =>
            ext == ".prefab" || s_ModelExtensions.Contains(ext);

        private static bool HasRenderer(string path, string ext)
        {
            if (ext != ".prefab") return true;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return go != null && go.GetComponentInChildren<Renderer>(true) != null;
        }
    }

    // ── Params & Result ───────────────────────────────────────────────────────

    public sealed class AssetFind3DParams
    {
        [Required] public string Query      { get; set; }
        public           int    MaxResults  { get; set; }
    }

    public sealed class AssetFind3DResult
    {
        public bool              IsPrimitive        { get; set; }
        public List<Asset3DMatch> Matches           { get; set; }
        public int               Count              { get; set; }
        public string            StoreSearchUrl     { get; set; }
        public bool              StoreSearchEnabled { get; set; }
        public string            Message            { get; set; }
    }

    public sealed class Asset3DMatch
    {
        public string Path        { get; set; }
        public string Name        { get; set; }
        public string Type        { get; set; }
        public string Extension   { get; set; }
        public bool   HasRenderer { get; set; }
    }
}
