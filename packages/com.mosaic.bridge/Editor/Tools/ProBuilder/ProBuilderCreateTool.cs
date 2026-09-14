#if MOSAIC_HAS_PROBUILDER
using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using PbEditorMeshUtility = UnityEditor.ProBuilder.EditorMeshUtility;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;
using Mosaic.Bridge.Core.Extensibility;
using Mosaic.Bridge.Core.Scenes;

namespace Mosaic.Bridge.Tools.ProBuilder
{
    public static class ProBuilderCreateTool
    {
        // EditorPrefs flag set by scene/create-object when it returns Action="build"
        internal const string PrefKeyBuildPlanActive = "MosaicBridge.BuildPlanActive";
        internal const string PrefKeyBuildPlanFor    = "MosaicBridge.BuildPlanFor";

        // Words that suggest the caller is assembling a complex object
        private static readonly System.Collections.Generic.HashSet<string> s_ComplexWords =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "ship", "pirate", "galleon", "boat", "vessel", "frigate", "schooner",
                "house", "home", "building", "cottage", "cabin",
                "castle", "fortress", "tower", "citadel",
                "vehicle", "car", "truck",
                "character", "person", "human",
                "dragon", "monster", "creature"
            };

        // Known ProBuilder primitive shape names — anything outside this list is a complex object.
        // L2: "hemisphere", "pyramid", "wedge", "quad", "disc", "disk" used to be advertised here
        // too, but none of them is a real UnityEngine.ProBuilder.ShapeType member (the actual set
        // is Cube, Stair, CurvedStair, Prism, Cylinder, Plane, Door, Pipe, Cone, Sprite, Arch,
        // Sphere, Torus) and none has its own explicit switch case below either — they passed
        // this guard only to fail later at Enum.TryParse with a confusing "Unknown Shape" error,
        // even though this same list is what told the caller the shape was valid.
        private static readonly System.Collections.Generic.HashSet<string> s_KnownShapes =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "cube", "box", "prism", "cylinder", "stairs", "stair", "curvedstair", "arch",
                "door", "plane", "pipe", "cone", "icosahedron", "sphere", "torus"
            };

        [MosaicTool("probuilder/create",
                    "Creates a ProBuilder mesh shape with correct geometry. " +
                    "⚠️ MANDATORY PREREQUISITE — read this before every call: " +
                    "This tool creates SINGLE primitive shapes only. " +
                    "For ANY complex object (ship, house, castle, vehicle, character, etc.) you MUST call " +
                    "scene/create-object FIRST. It checks the project, searches the Asset Store, and " +
                    "returns an exact build plan (Parts list) when procedural building is needed. " +
                    "Only call probuilder/create: " +
                    "(1) when scene/create-object returns Action='primitive', OR " +
                    "(2) when scene/create-object returns Action='build' and you are executing its Parts list. " +
                    "Skipping scene/create-object and calling probuilder/create directly for a complex object " +
                    "will be REJECTED with an error. " +
                    "Shapes: Cube, Prism, Cylinder, Stairs, CurvedStair, Arch, Door, Plane, Pipe, Cone, Icosahedron, Torus. " +
                    "IMPORTANT shape notes: " +
                    "• 'Cube' = box — use Dimensions:[w,h,d] for exact size (calls GenerateCube). " +
                    "• 'Prism' = triangular prism — perfect for roofs, use Dimensions:[w,h,d] (calls GeneratePrism). " +
                    "• 'Cylinder' — use Radius, Height, AxisDivisions (default 8). " +
                    "• 'Door' — use DoorWidth, DoorHeight, LedgeHeight (top ledge), LegWidth (side frame), Depth. " +
                    "• 'Stairs' — use Dimensions:[w,h,d] for stair volume, Steps (default 6). " +
                    "• 'Arch' — use Radius, ArchWidth (depth), ArchDepth, Angle (degrees, default 180), RadialCuts. " +
                    "• All others — use Dimensions:[w,h,d] as fallback (applied via localScale). " +
                    "All shapes support Position:[x,y,z], Rotation:[x,y,z] euler angles, and Name. " +
                    "Rotation tips: yardarms/horizontal cylinders → Rotation:[90,0,0]; " +
                    "bowsprit (forward+up 35°) → Rotation:[0,0,-55]; vertical (default) → omit Rotation. " +
                    "GenerateBox does NOT exist — always use Shape='Cube'. " +
                    "Collider: 'Mesh' (default, matches the ProBuilder Editor UI's own default), 'Box', or 'None'. " +
                    "Convex (Mesh collider only) and IsTrigger are both false by default — a trigger MeshCollider " +
                    "requires Convex=true, same as Unity's own rule.",
                    isReadOnly: false, category: "probuilder")]
        public static ToolResult<ProBuilderCreateResult> Create(ProBuilderCreateParams p)
        {
            if (string.IsNullOrEmpty(p.Shape))
                return ToolResult<ProBuilderCreateResult>.Fail(
                    "Shape is required. Valid: Cube, Prism, Cylinder, Stairs, CurvedStair, Arch, Door, Plane, Pipe, Cone, Icosahedron, Torus",
                    ErrorCodes.INVALID_PARAM);

            // Reject non-primitive shape names.
            if (!s_KnownShapes.Contains(p.Shape))
                return ToolResult<ProBuilderCreateResult>.Fail(
                    $"'{p.Shape}' is not a ProBuilder primitive shape. " +
                    $"Call scene/create-object first with Name='{p.Shape}' — it will check the project, " +
                    $"search the Asset Store, and return an exact build plan if needed. " +
                    $"Valid primitive shapes: Cube, Prism, Cylinder, Stairs, Arch, Door, Plane, Pipe, Cone, Torus.",
                    ErrorCodes.INVALID_PARAM);

            // Detect assembly of a complex object without going through scene/create-object first.
            // scene/create-object sets BuildPlanActive=true when returning a build plan;
            // if that flag is not set and the parent/name suggests a complex object, reject.
            bool buildPlanActive = UnityEditor.EditorPrefs.GetBool(PrefKeyBuildPlanActive, false);
            if (!buildPlanActive && !string.IsNullOrEmpty(p.ParentName))
            {
                string parentLower = p.ParentName.ToLowerInvariant();
                foreach (string word in s_ComplexWords)
                {
                    if (parentLower.Contains(word))
                        return ToolResult<ProBuilderCreateResult>.Fail(
                            $"You are assembling a complex object ('{p.ParentName}') without calling " +
                            $"scene/create-object first. Call scene/create-object with Name='{p.ParentName}' now. " +
                            $"It will search the project and Asset Store, and return an exact build plan " +
                            $"(Parts list with shapes, sizes, and positions) when you need to build procedurally.",
                            ErrorCodes.INVALID_PARAM);
                }
            }

            ProBuilderMesh mesh;

            string shape = p.Shape.ToLowerInvariant();

            switch (shape)
            {
                case "cube":
                case "box":
                {
                    var size = p.Dimensions != null && p.Dimensions.Length == 3
                        ? new Vector3(p.Dimensions[0], p.Dimensions[1], p.Dimensions[2])
                        : Vector3.one;
                    mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
                    break;
                }

                case "prism":
                {
                    var size = p.Dimensions != null && p.Dimensions.Length == 3
                        ? new Vector3(p.Dimensions[0], p.Dimensions[1], p.Dimensions[2])
                        : Vector3.one;
                    mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, size);
                    break;
                }

                case "cylinder":
                {
                    float radius = p.Radius > 0f ? p.Radius : 0.5f;
                    float height = p.Height > 0f ? p.Height : 1f;
                    int axisDivisions = p.AxisDivisions > 2 ? p.AxisDivisions : 8;
                    // L2: the hardcoded trailing 0 forced smoothingGroup=0 (hard-shaded, faceted
                    // sides) on every cylinder this tool ever created. Omitting it uses
                    // GenerateCylinder's own default (-1), matching what the ProBuilder Editor's
                    // own GameObject > ProBuilder > Cylinder menu command produces.
                    mesh = ShapeGenerator.GenerateCylinder(PivotLocation.Center, axisDivisions, radius, height, 1);
                    break;
                }

                case "stairs":
                case "stair":
                {
                    var size = p.Dimensions != null && p.Dimensions.Length == 3
                        ? new Vector3(p.Dimensions[0], p.Dimensions[1], p.Dimensions[2])
                        : new Vector3(2f, 2f, 4f);
                    int steps = p.Steps > 1 ? p.Steps : 6;
                    mesh = ShapeGenerator.GenerateStair(PivotLocation.FirstVertex, size, steps, true);
                    break;
                }

                case "door":
                {
                    float dw = p.DoorWidth > 0f ? p.DoorWidth : 2f;
                    float dh = p.DoorHeight > 0f ? p.DoorHeight : 3f;
                    float ledge = p.LedgeHeight >= 0f ? p.LedgeHeight : 0.5f;
                    float leg = p.LegWidth > 0f ? p.LegWidth : 0.4f;
                    float depth = p.Depth > 0f ? p.Depth : 0.5f;
                    mesh = ShapeGenerator.GenerateDoor(PivotLocation.Center, dw, dh, ledge, leg, depth);
                    break;
                }

                case "arch":
                {
                    float angle = p.Angle > 0f ? p.Angle : 180f;
                    float radius = p.Radius > 0f ? p.Radius : 1f;
                    float archWidth = p.ArchWidth > 0f ? p.ArchWidth : 0.5f;
                    float archDepth = p.ArchDepth > 0f ? p.ArchDepth : 0.5f;
                    int radialCuts = p.RadialCuts > 1 ? p.RadialCuts : 6;
                    mesh = ShapeGenerator.GenerateArch(PivotLocation.Center,
                        angle, radius, archWidth, archDepth, radialCuts,
                        true, true, true, true, true);
                    break;
                }

                case "plane":
                {
                    float w = p.Dimensions != null && p.Dimensions.Length >= 1 ? p.Dimensions[0] : 5f;
                    float h = p.Dimensions != null && p.Dimensions.Length >= 3 ? p.Dimensions[2] : 5f;
                    mesh = ShapeGenerator.GeneratePlane(PivotLocation.Center, w, h, 2, 2, Axis.Up);
                    break;
                }

                case "pipe":
                {
                    float radius = p.Radius > 0f ? p.Radius : 1f;
                    float height = p.Height > 0f ? p.Height : 2f;
                    float thickness = p.Depth > 0f ? p.Depth : 0.2f;
                    int axisDivisions = p.AxisDivisions > 2 ? p.AxisDivisions : 8;
                    mesh = ShapeGenerator.GeneratePipe(PivotLocation.Center, radius, height, thickness, axisDivisions, 1);
                    break;
                }

                case "cone":
                {
                    float radius = p.Radius > 0f ? p.Radius : 0.5f;
                    float height = p.Height > 0f ? p.Height : 1f;
                    int axisDivisions = p.AxisDivisions > 2 ? p.AxisDivisions : 8;
                    mesh = ShapeGenerator.GenerateCone(PivotLocation.Center, radius, height, axisDivisions);
                    break;
                }

                case "torus":
                {
                    float inner = p.Radius > 0f ? p.Radius : 0.5f;
                    float outer = p.Radius > 0f ? p.Radius * 2f : 1f;
                    if (p.Dimensions != null && p.Dimensions.Length >= 2)
                    {
                        inner = p.Dimensions[0];
                        outer = p.Dimensions[1];
                    }
                    mesh = ShapeGenerator.GenerateTorus(PivotLocation.Center, 8, 16, inner, outer, true, 360f, 360f, false);
                    break;
                }

                case "icosahedron":
                case "sphere":
                {
                    float radius = p.Radius > 0f ? p.Radius : 0.5f;
                    int subdivisions = p.Steps > 0 ? p.Steps : 2;
                    mesh = ShapeGenerator.GenerateIcosahedron(PivotLocation.Center, radius, subdivisions, true, false);
                    break;
                }

                default:
                {
                    if (!System.Enum.TryParse<ShapeType>(p.Shape, true, out var shapeType))
                        return ToolResult<ProBuilderCreateResult>.Fail(
                            $"Unknown Shape '{p.Shape}'. Valid: Cube, Prism, Cylinder, Stairs, Arch, Door, Plane, Pipe, Cone, Icosahedron, Torus",
                            ErrorCodes.INVALID_PARAM);
                    mesh = ShapeGenerator.CreateShape(shapeType);
                    if (p.Dimensions != null && p.Dimensions.Length == 3)
                        mesh.transform.localScale = new Vector3(p.Dimensions[0], p.Dimensions[1], p.Dimensions[2]);
                    break;
                }
            }

            if (!string.IsNullOrEmpty(p.Name))
                mesh.gameObject.name = p.Name;

            if (p.Position != null && p.Position.Length == 3)
                mesh.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            if (p.Rotation != null && p.Rotation.Length == 3)
                mesh.transform.eulerAngles = new Vector3(p.Rotation[0], p.Rotation[1], p.Rotation[2]);

            if (p.ParentInstanceId != null || !string.IsNullOrEmpty(p.ParentName))
            {
                if (GameObjectResolver.TryResolve(p.ParentInstanceId, p.ParentName, out var parent, out _))
                    mesh.transform.SetParent(parent.transform, true);
            }

            mesh.ToMesh();
            mesh.Refresh();
            PbEditorMeshUtility.Optimize(mesh);

            // G2: ShapeGenerator never adds a collider — only the ProBuilder Editor UI's own
            // internal init does (EditorUtility.InitObject, prefs s_ColliderType /
            // s_MeshColliderIsConvex). Default here mirrors that UI default: a non-convex
            // MeshCollider, so a course object is physically solid the moment it's created.
            string colliderType = string.IsNullOrEmpty(p.Collider) ? "Mesh" : p.Collider;
            if (!string.Equals(colliderType, "Mesh", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(colliderType, "Box", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(colliderType, "None", System.StringComparison.OrdinalIgnoreCase))
                return ToolResult<ProBuilderCreateResult>.Fail(
                    $"Unknown Collider '{p.Collider}'. Valid: Mesh, Box, None", ErrorCodes.INVALID_PARAM);

            if (p.IsTrigger && string.Equals(colliderType, "Mesh", System.StringComparison.OrdinalIgnoreCase) && !p.Convex)
                return ToolResult<ProBuilderCreateResult>.Fail(
                    "IsTrigger=true on a Mesh collider requires Convex=true — Unity does not support a " +
                    "non-convex trigger MeshCollider. Use Convex=true, Collider='Box', or IsTrigger=false.",
                    ErrorCodes.INVALID_PARAM);

            if (string.Equals(colliderType, "Mesh", System.StringComparison.OrdinalIgnoreCase))
            {
                var mc = mesh.gameObject.AddComponent<MeshCollider>();
                // ProBuilderMesh has no public Mesh property of its own — ToMesh()/Refresh() build
                // and attach the runtime Mesh onto the sibling MeshFilter, same as any other
                // procedural mesh workflow.
                mc.sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
                mc.convex = p.Convex;
                mc.isTrigger = p.IsTrigger;
            }
            else if (string.Equals(colliderType, "Box", System.StringComparison.OrdinalIgnoreCase))
            {
                var box = mesh.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = p.IsTrigger;
            }
            else
            {
                colliderType = "None";
            }

            Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Mosaic: ProBuilder Create");

            var qa = ObjectCreationHooks.TryRun(mesh.gameObject, "probuilder/create");
            if (qa != null && qa.Status == "failed")
                return ToolResult<ProBuilderCreateResult>.Fail(
                    "Object failed quality checks: " + string.Join("; ", qa.Violations),
                    ErrorCodes.OBJECT_QA_FAILED);

            return ToolResult<ProBuilderCreateResult>.Ok(new ProBuilderCreateResult
            {
                Name        = mesh.gameObject.name,
                InstanceId  = UnityIds.Of(mesh.gameObject),
                VertexCount = mesh.vertexCount,
                FaceCount   = mesh.faceCount,
                ColliderType = colliderType,
                QualityCheck = qa
            });
        }
    }

    public sealed class ProBuilderCreateParams
    {
        [Required] public string Shape { get; set; }
        public string  Name       { get; set; }
        public float[] Dimensions { get; set; }   // [w, h, d] — Cube, Prism, Stairs, Plane
        public float[] Position   { get; set; }   // [x, y, z] world position
        public float[] Rotation   { get; set; }   // [x, y, z] euler angles (e.g. [90,0,0] for yardarms)
        public string  ParentName { get; set; }   // parent GameObject name
        public int?    ParentInstanceId { get; set; }

        // Cylinder / Cone / Pipe / Icosahedron
        public float Radius { get; set; }
        public float Height { get; set; }
        public int AxisDivisions { get; set; }

        // Stairs
        public int Steps { get; set; }

        // Door
        public float DoorWidth { get; set; }
        public float DoorHeight { get; set; }
        public float LedgeHeight { get; set; }
        public float LegWidth { get; set; }
        public float Depth { get; set; }

        // Arch
        public float Angle { get; set; }
        public float ArchWidth { get; set; }
        public float ArchDepth { get; set; }
        public int RadialCuts { get; set; }

        // Collider (G2) — "Mesh" (default), "Box", or "None"
        public string Collider { get; set; }
        public bool Convex { get; set; }
        public bool IsTrigger { get; set; }
    }

    public sealed class ProBuilderCreateResult
    {
        public string Name { get; set; }
        public int InstanceId { get; set; }
        public int VertexCount { get; set; }
        public int FaceCount { get; set; }

        /// <summary>What was actually added: "Mesh", "Box", or "None".</summary>
        public string ColliderType { get; set; }

        /// <summary>Set only when an object-quality provider (Mosaic.Pro.Core) is installed.</summary>
        public ObjectQaReport QualityCheck { get; set; }
    }
}
#endif
