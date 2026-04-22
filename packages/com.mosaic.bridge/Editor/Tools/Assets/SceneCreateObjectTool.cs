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
    /// Mandatory entry point for all complex object creation.
    /// Runs the full decision tree and returns the action to take.
    /// </summary>
    public static class SceneCreateObjectTool
    {
        internal const string PrefKeyBuildPlanActive = "MosaicBridge.BuildPlanActive";
        internal const string PrefKeyBuildPlanFor    = "MosaicBridge.BuildPlanFor";

        [MosaicTool("scene/create-object",
                    "MANDATORY ENTRY POINT — call this before creating any complex object (ship, house, car, castle, etc.). " +
                    "Runs the full decision tree and returns Action telling you exactly what to do next. " +
                    "DECISION TREE — follow in order: " +
                    "Action='primitive'   → use probuilder/create with the returned Shape directly. " +
                    "Action='instantiate' → use asset/instantiate_prefab with AssetPath. " +
                    "Action='choose'      → multiple project assets found; show Matches list to the user and ask which they want; " +
                    "                       re-call with ChoiceIndex=N (1-based) to select; AutoApprove=true skips and picks best. " +
                    "Action='store'       → no project asset found; show StoreUrl to user, ask if they want to download. " +
                    "                       Yes → user downloads + imports → re-call scene/create-object. " +
                    "                       No  → re-call with SkipStore=true. " +
                    "Action='build'       → procedural build; if HasBuiltInPlan=true: create root GameObject, then call " +
                    "                       probuilder/create for each Part (Shape/Name/Dimensions/Position/Rotation/ParentName). " +
                    "                       After each part call material/create with Color=Part.MaterialHex and material/assign. " +
                    "                       if HasBuiltInPlan=false: ask the user to describe the object before building. " +
                    "Parameters: Name (what to create), AutoApprove (auto-pick best project match), " +
                    "SkipStore (skip Asset Store step), ChoiceIndex (user-selected index from 'choose' result).",
                    isReadOnly: false)]
        public static ToolResult<CreateObjectResult> Execute(CreateObjectParams p)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
                return ToolResult<CreateObjectResult>.Fail(
                    "Name is required.", Contracts.Errors.ErrorCodes.INVALID_PARAM);

            // Clear previous build-plan session so the guard in probuilder/create resets.
            EditorPrefs.DeleteKey(PrefKeyBuildPlanActive);
            EditorPrefs.DeleteKey(PrefKeyBuildPlanFor);

            string query = p.Name.Trim().ToLowerInvariant();

            // ── Step 1: Primitive? ────────────────────────────────────────────
            if (IsPrimitive(query))
                return ToolResult<CreateObjectResult>.Ok(new CreateObjectResult
                {
                    Action  = "primitive",
                    Shape   = ToPrimitiveShape(query),
                    Message = $"'{p.Name}' is a primitive shape. Use probuilder/create with Shape='{ToPrimitiveShape(query)}'."
                });

            // ── Step 2: Search project ────────────────────────────────────────
            var matches = FindAllInProject(query);
            if (matches.Count > 0)
            {
                // User confirmed a specific choice
                if (p.ChoiceIndex > 0 && p.ChoiceIndex <= matches.Count)
                {
                    var chosen = matches[p.ChoiceIndex - 1];
                    return ToolResult<CreateObjectResult>.Ok(new CreateObjectResult
                    {
                        Action    = "instantiate",
                        AssetPath = chosen.Path,
                        AssetName = chosen.Name,
                        Message   = $"Instantiating '{chosen.Name}'. Use asset/instantiate_prefab with Path='{chosen.Path}'."
                    });
                }

                // Single result or auto-approve — pick best (exact match first)
                if (p.AutoApprove || matches.Count == 1)
                {
                    var best = matches[0];
                    return ToolResult<CreateObjectResult>.Ok(new CreateObjectResult
                    {
                        Action    = "instantiate",
                        AssetPath = best.Path,
                        AssetName = best.Name,
                        Message   = $"Auto-selected best match '{best.Name}'. Use asset/instantiate_prefab with Path='{best.Path}'."
                    });
                }

                // Multiple — let user choose
                return ToolResult<CreateObjectResult>.Ok(new CreateObjectResult
                {
                    Action   = "choose",
                    Matches  = matches,
                    Message  = $"Found {matches.Count} matching assets in project. " +
                               $"Show this list to the user and ask which one they want. " +
                               $"Then re-call scene/create-object with ChoiceIndex=N (1-based index from the list below). " +
                               $"Or set AutoApprove=true to automatically pick the best match."
                });
            }

            // ── Step 3: Asset Store ───────────────────────────────────────────
            bool storeEnabled = EditorPrefs.GetBool("MosaicBridge.AssetStoreGuidance", true);
            if (storeEnabled && !p.SkipStore)
            {
                string url = BuildStoreUrl(query);
                return ToolResult<CreateObjectResult>.Ok(new CreateObjectResult
                {
                    Action   = "store",
                    StoreUrl = url,
                    Message  = $"No '{p.Name}' found in project. Search the Asset Store: {url}\n" +
                               $"Show this URL to the user and ask if they want to download a free asset.\n" +
                               $"• Yes → user downloads + imports → re-call scene/create-object.\n" +
                               $"• No  → re-call scene/create-object with SkipStore=true to get the build plan."
                });
            }

            // ── Step 4: Build plan ────────────────────────────────────────────
            bool hasBuiltIn  = HasBuiltInPlan(query);
            var  parts       = GetBuildPlan(query);

            // Signal probuilder/create that a build plan is now active (bypass complex-parent guard).
            EditorPrefs.SetBool(PrefKeyBuildPlanActive, true);
            EditorPrefs.SetString(PrefKeyBuildPlanFor,  p.Name);

            string msg = hasBuiltIn
                ? $"No asset found. Build '{p.Name}' using the Parts list below. " +
                  $"Steps: (1) Create empty root GameObject named '{p.Name}'. " +
                  $"(2) For each Part, call probuilder/create with Shape/Name/Dimensions/Position/Rotation/ParentName='{p.Name}'. " +
                  $"(3) After each part, call material/create with Color=Part.MaterialHex and material/assign to apply it."
                : $"No asset found and no built-in plan for '{p.Name}'. " +
                  $"Ask the user to describe the object's main components before building. " +
                  $"A single starter cube is included — replace it once the user describes the parts.";

            return ToolResult<CreateObjectResult>.Ok(new CreateObjectResult
            {
                Action         = "build",
                HasBuiltInPlan = hasBuiltIn,
                Parts          = parts,
                Message        = msg
            });
        }

        // ── Primitive detection ───────────────────────────────────────────────

        private static readonly HashSet<string> s_Primitives = new HashSet<string>
        {
            "cube", "box", "sphere", "ball", "cylinder", "capsule", "plane", "quad",
            "cone", "prism", "pyramid", "wedge", "torus", "ring", "pipe", "arch",
            "stairs", "stair", "door", "icosahedron", "hemisphere", "disc", "disk"
        };

        private static bool IsPrimitive(string q)
        {
            if (s_Primitives.Contains(q)) return true;
            foreach (string w in q.Split(' '))
                if (s_Primitives.Contains(w)) return true;
            return false;
        }

        private static string ToPrimitiveShape(string q)
        {
            switch (q)
            {
                case "box":  return "Cube";
                case "ball": return "Sphere";
                case "ring": return "Torus";
                case "stair":return "Stairs";
                default: return char.ToUpperInvariant(q[0]) + q.Substring(1);
            }
        }

        // ── Project search ────────────────────────────────────────────────────

        private static readonly HashSet<string> s_ModelExts =
            new HashSet<string> { ".fbx", ".obj", ".dae", ".3ds", ".gltf", ".glb" };

        private static List<Asset3DMatch> FindAllInProject(string query)
        {
            string[] keywords    = ExpandKeywords(query);
            string[] typeFilters = { "t:Prefab", "t:Model" };
            var seen    = new HashSet<string>();
            var exact   = new List<Asset3DMatch>();
            var partial = new List<Asset3DMatch>();

            foreach (string tf in typeFilters)
            {
                foreach (string kw in keywords)
                {
                    foreach (string guid in AssetDatabase.FindAssets(tf + " " + kw))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!seen.Add(path) || path.StartsWith("Packages/")) continue;
                        string ext = Path.GetExtension(path).ToLowerInvariant();
                        if (ext != ".prefab" && !s_ModelExts.Contains(ext)) continue;

                        string fn      = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                        bool   isExact = query.Split(' ').All(w => fn.Contains(w));

                        var m = new Asset3DMatch
                        {
                            Path      = path,
                            Name      = Path.GetFileNameWithoutExtension(path),
                            Type      = ext == ".prefab" ? "Prefab" : "Model",
                            Extension = ext
                        };

                        if (isExact) exact.Add(m);
                        else         partial.Add(m);
                    }
                }
            }

            // Exact matches first (highest relevance), then partial; cap at 10
            return exact.Concat(partial).Take(10).ToList();
        }

        // ── Keyword expansion ─────────────────────────────────────────────────

        private static string[] ExpandKeywords(string query)
        {
            var kws = new List<string>(query.Split(' '));
            var aliases = new Dictionary<string, string[]>
            {
                { "ship",      new[] { "ship", "boat", "vessel", "galleon", "schooner", "frigate" } },
                { "pirate",    new[] { "pirate", "buccaneer", "corsair" } },
                { "house",     new[] { "house", "home", "building", "cottage", "cabin" } },
                { "tree",      new[] { "tree", "oak", "pine", "palm", "birch" } },
                { "car",       new[] { "car", "vehicle", "auto", "truck" } },
                { "character", new[] { "character", "hero", "player", "npc", "humanoid" } },
                { "sword",     new[] { "sword", "blade", "katana", "saber" } },
                { "cannon",    new[] { "cannon", "mortar", "artillery" } },
                { "castle",    new[] { "castle", "fortress", "citadel", "tower" } },
                { "rock",      new[] { "rock", "stone", "boulder" } },
                { "dragon",    new[] { "dragon", "wyvern", "drake" } },
                { "chest",     new[] { "chest", "treasure", "loot", "crate" } },
            };
            foreach (string word in query.Split(' '))
                if (aliases.TryGetValue(word, out string[] syn)) kws.AddRange(syn);
            return kws.Distinct().ToArray();
        }

        // ── Asset Store URL ───────────────────────────────────────────────────

        private static string BuildStoreUrl(string query)
        {
            string encoded = UnityEngine.Networking.UnityWebRequest.EscapeURL(query);
            return "https://assetstore.unity.com/search#q=" + encoded + "&orderBy=1&free=true";
        }

        // ── Build plans ───────────────────────────────────────────────────────

        private static bool HasBuiltInPlan(string query)
        {
            return Contains(query, "pirate", "ship", "galleon", "boat", "vessel")
                || Contains(query, "house",  "home",  "cottage", "cabin", "building")
                || Contains(query, "castle", "fortress", "tower")
                || Contains(query, "tree");
        }

        private static List<BuildPart> GetBuildPlan(string query)
        {
            if (Contains(query, "pirate", "ship", "galleon", "boat", "vessel")) return ShipPlan();
            if (Contains(query, "house",  "home",  "cottage", "cabin", "building")) return HousePlan();
            if (Contains(query, "castle", "fortress", "tower")) return CastlePlan();
            if (Contains(query, "tree"))  return TreePlan();

            // Generic fallback
            return new List<BuildPart>
            {
                new BuildPart { Shape="Cube", Name="Body", Dimensions=new[]{1f,1f,1f}, Position=new[]{0f,0f,0f}, MaterialHex="#AAAAAA" }
            };
        }

        private static bool Contains(string q, params string[] words) =>
            words.Any(w => q.Contains(w));

        // ── Ship (19 parts) ───────────────────────────────────────────────────
        // Hull extends along X. Deck at Y≈2.8. Masts vertical (default Y).
        // Yardarms: Rotation=[90,0,0] → cylinder aligns along Z (across ship width).
        // Bowsprit: Rotation=[0,0,-55] → cylinder points forward+up (~35° elevation).
        private static List<BuildPart> ShipPlan() => new List<BuildPart>
        {
            new BuildPart { Shape="Cube",     Name="Hull",         Dimensions=new[]{18f,2.5f,6f},  Position=new[]{0f,1.25f,0f},    MaterialHex="#6B3A2A" },
            new BuildPart { Shape="Cube",     Name="Deck",         Dimensions=new[]{18f,0.3f,6f},  Position=new[]{0f,2.65f,0f},    MaterialHex="#8B6914" },
            new BuildPart { Shape="Prism",    Name="Bow",          Dimensions=new[]{4f,2.5f,6f},   Position=new[]{10f,1.25f,0f},   MaterialHex="#6B3A2A" },
            new BuildPart { Shape="Cube",     Name="Stern_Castle", Dimensions=new[]{5f,2f,6f},     Position=new[]{-7f,3.8f,0f},    MaterialHex="#6B3A2A" },
            new BuildPart { Shape="Cube",     Name="Cabin",        Dimensions=new[]{4f,2.5f,5f},   Position=new[]{-6f,5f,0f},      MaterialHex="#7B4F3A" },
            new BuildPart { Shape="Prism",    Name="Cabin_Roof",   Dimensions=new[]{4f,1.5f,5f},   Position=new[]{-6f,6.75f,0f},   MaterialHex="#5C3A1E" },
            new BuildPart { Shape="Cylinder", Name="Mast_Main",    Radius=0.2f,  Height=16f,       Position=new[]{1f,10.65f,0f},   MaterialHex="#5C4033" },
            new BuildPart { Shape="Cylinder", Name="Mast_Fore",    Radius=0.18f, Height=12f,       Position=new[]{5.5f,8.65f,0f},  MaterialHex="#5C4033" },
            new BuildPart { Shape="Cylinder", Name="Mast_Mizzen",  Radius=0.15f, Height=10f,       Position=new[]{-4f,7.65f,0f},   MaterialHex="#5C4033" },
            new BuildPart { Shape="Cylinder", Name="Bowsprit",     Radius=0.15f, Height=8f,        Position=new[]{12f,4f,0f},      Rotation=new[]{0f,0f,-55f}, MaterialHex="#5C4033" },
            new BuildPart { Shape="Cylinder", Name="Yard_Main",    Radius=0.1f,  Height=10f,       Position=new[]{1f,14f,0f},      Rotation=new[]{90f,0f,0f},  MaterialHex="#5C4033" },
            new BuildPart { Shape="Cylinder", Name="Yard_Fore",    Radius=0.09f, Height=7f,        Position=new[]{5.5f,11f,0f},    Rotation=new[]{90f,0f,0f},  MaterialHex="#5C4033" },
            new BuildPart { Shape="Cylinder", Name="Yard_Mizzen",  Radius=0.08f, Height=5f,        Position=new[]{-4f,10f,0f},     Rotation=new[]{90f,0f,0f},  MaterialHex="#5C4033" },
            new BuildPart { Shape="Plane",    Name="Sail_Main",    Dimensions=new[]{9f,0.05f,8f},  Position=new[]{1f,10f,0f},      MaterialHex="#F0E8D0" },
            new BuildPart { Shape="Plane",    Name="Sail_Fore",    Dimensions=new[]{6f,0.05f,6f},  Position=new[]{5.5f,7.5f,0f},   MaterialHex="#F0E8D0" },
            new BuildPart { Shape="Plane",    Name="Sail_Mizzen",  Dimensions=new[]{4.5f,0.05f,5f},Position=new[]{-4f,7f,0f},      MaterialHex="#F0E8D0" },
            new BuildPart { Shape="Cube",     Name="Crow_Nest",    Dimensions=new[]{1.5f,0.3f,1.5f},Position=new[]{1f,17.5f,0f},  MaterialHex="#7B5C44" },
            new BuildPart { Shape="Cylinder", Name="Cannon_Port",  Radius=0.2f,  Height=1.5f,      Position=new[]{0f,2.65f,3.5f},  Rotation=new[]{90f,0f,0f},  MaterialHex="#2C2C2C" },
            new BuildPart { Shape="Cylinder", Name="Cannon_Stbd",  Radius=0.2f,  Height=1.5f,      Position=new[]{0f,2.65f,-3.5f}, Rotation=new[]{90f,0f,0f},  MaterialHex="#2C2C2C" },
        };

        // ── House (5 parts) ───────────────────────────────────────────────────
        private static List<BuildPart> HousePlan() => new List<BuildPart>
        {
            new BuildPart { Shape="Cube",  Name="Walls",      Dimensions=new[]{8f,4f,8f},  Position=new[]{0f,2f,0f},     MaterialHex="#D4C5B0" },
            new BuildPart { Shape="Prism", Name="Roof",       Dimensions=new[]{9f,2f,9f},  Position=new[]{0f,5f,0f},     MaterialHex="#8B3030" },
            new BuildPart { Shape="Door",  Name="Front_Door", DoorWidth=1.5f, DoorHeight=2.5f, LedgeHeight=0.3f, LegWidth=0.3f, Depth=0.2f, Position=new[]{0f,1.25f,4.1f}, MaterialHex="#4A2E1A" },
            new BuildPart { Shape="Cube",  Name="Window_L",   Dimensions=new[]{1.2f,1f,0.15f}, Position=new[]{-2f,2.5f,4.05f}, MaterialHex="#87CEEB" },
            new BuildPart { Shape="Cube",  Name="Window_R",   Dimensions=new[]{1.2f,1f,0.15f}, Position=new[]{2f,2.5f,4.05f},  MaterialHex="#87CEEB" },
        };

        // ── Castle (9 parts) ──────────────────────────────────────────────────
        private static List<BuildPart> CastlePlan() => new List<BuildPart>
        {
            new BuildPart { Shape="Cube",     Name="Keep",       Dimensions=new[]{10f,12f,10f}, Position=new[]{0f,6f,0f},    MaterialHex="#888880" },
            new BuildPart { Shape="Cylinder", Name="Tower_FL",   Radius=2f, Height=14f,          Position=new[]{-6f,7f,-6f},  MaterialHex="#808080" },
            new BuildPart { Shape="Cylinder", Name="Tower_FR",   Radius=2f, Height=14f,          Position=new[]{6f,7f,-6f},   MaterialHex="#808080" },
            new BuildPart { Shape="Cylinder", Name="Tower_BL",   Radius=2f, Height=14f,          Position=new[]{-6f,7f,6f},   MaterialHex="#808080" },
            new BuildPart { Shape="Cylinder", Name="Tower_BR",   Radius=2f, Height=14f,          Position=new[]{6f,7f,6f},    MaterialHex="#808080" },
            new BuildPart { Shape="Cube",     Name="Wall_Front", Dimensions=new[]{12f,8f,1f},    Position=new[]{0f,4f,-7f},   MaterialHex="#888880" },
            new BuildPart { Shape="Cube",     Name="Wall_Back",  Dimensions=new[]{12f,8f,1f},    Position=new[]{0f,4f,7f},    MaterialHex="#888880" },
            new BuildPart { Shape="Cube",     Name="Wall_Left",  Dimensions=new[]{1f,8f,12f},    Position=new[]{-7f,4f,0f},   MaterialHex="#888880" },
            new BuildPart { Shape="Cube",     Name="Wall_Right", Dimensions=new[]{1f,8f,12f},    Position=new[]{7f,4f,0f},    MaterialHex="#888880" },
        };

        // ── Tree (2 parts) ────────────────────────────────────────────────────
        private static List<BuildPart> TreePlan() => new List<BuildPart>
        {
            new BuildPart { Shape="Cylinder",   Name="Trunk",  Radius=0.3f, Height=4f,  Position=new[]{0f,2f,0f},   MaterialHex="#5C4033" },
            new BuildPart { Shape="Icosahedron", Name="Canopy", Radius=2.5f,             Position=new[]{0f,5.5f,0f}, MaterialHex="#2D7A2D" },
        };
    }

    // ── Params & Results ──────────────────────────────────────────────────────

    public sealed class CreateObjectParams
    {
        [Required] public string Name        { get; set; }
        public           bool   AutoApprove  { get; set; }
        public           bool   SkipStore    { get; set; }
        public           int    ChoiceIndex  { get; set; }
    }

    public sealed class CreateObjectResult
    {
        public string           Action         { get; set; }  // "primitive"|"instantiate"|"choose"|"store"|"build"
        public string           Shape          { get; set; }  // for "primitive"
        public string           AssetPath      { get; set; }  // for "instantiate"
        public string           AssetName      { get; set; }  // for "instantiate"
        public List<Asset3DMatch> Matches      { get; set; }  // for "choose"
        public string           StoreUrl       { get; set; }  // for "store"
        public bool             HasBuiltInPlan { get; set; }  // for "build"
        public List<BuildPart>  Parts          { get; set; }  // for "build"
        public string           Message        { get; set; }
    }

    public sealed class BuildPart
    {
        public string  Shape       { get; set; }
        public string  Name        { get; set; }
        public float[] Dimensions  { get; set; }   // [w,h,d]
        public float[] Position    { get; set; }   // [x,y,z] world space
        public float[] Rotation    { get; set; }   // [x,y,z] euler angles
        public string  ParentName  { get; set; }   // set to root object name when building
        public string  MaterialHex { get; set; }   // CSS hex color e.g. "#8B4513"
        // Shape-specific
        public float   Radius      { get; set; }
        public float   Height      { get; set; }
        public float   DoorWidth   { get; set; }
        public float   DoorHeight  { get; set; }
        public float   LedgeHeight { get; set; }
        public float   LegWidth    { get; set; }
        public float   Depth       { get; set; }
    }
}
