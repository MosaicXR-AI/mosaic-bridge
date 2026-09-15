using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsSetPhysicsMaterialTool
    {
        [MosaicTool("physics/set-physics-material",
                    "Creates and assigns a PhysicsMaterial to a GameObject's collider. Omitted friction/" +
                    "bounciness values keep Unity's own defaults (0.6/0.6/0) rather than becoming 0. Set " +
                    "ReuseExisting=true with an AssetPath that already has a PhysicsMaterial to share that " +
                    "asset across multiple objects instead of creating a new one each call. " +
                    "FrictionCombine/BounceCombine control how this material blends with the other collider's " +
                    "in contact. ApplyToChildren also assigns it to every child Collider.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsSetPhysicsMaterialResult> Execute(PhysicsSetPhysicsMaterialParams p)
        {
            var go = PhysicsToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<PhysicsSetPhysicsMaterialResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found",
                    ErrorCodes.NOT_FOUND);

            var collider = go.GetComponent<Collider>();
            if (collider == null)
                return ToolResult<PhysicsSetPhysicsMaterialResult>.Fail(
                    $"GameObject '{go.name}' has no Collider component",
                    ErrorCodes.INVALID_PARAM);

            Undo.RecordObject(collider, "Mosaic: Set PhysicsMaterial");

            PhysicsMaterial mat;
            bool savedAsAsset = false;
            string assetPath = null;

            // L10: reusing an existing asset instead of always creating a new one — the earlier
            // code had no way to point two colliders at the SAME PhysicsMaterial, which is the
            // whole point of it being an asset rather than a per-collider struct.
            if (p.ReuseExisting)
            {
                if (string.IsNullOrEmpty(p.AssetPath))
                    return ToolResult<PhysicsSetPhysicsMaterialResult>.Fail(
                        "ReuseExisting requires AssetPath.", ErrorCodes.INVALID_PARAM);
                mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(p.AssetPath);
                if (mat == null)
                    return ToolResult<PhysicsSetPhysicsMaterialResult>.Fail(
                        $"No PhysicsMaterial asset found at '{p.AssetPath}' to reuse. Set ReuseExisting=false " +
                        "to create a new one there.", ErrorCodes.NOT_FOUND);
                savedAsAsset = true;
                assetPath = p.AssetPath;
            }
            else
            {
                // L10: these used to be non-nullable floats defaulting to 0 when omitted — an
                // icy floor (friction 0) by accident, when Unity's own PhysicsMaterial default is
                // 0.6/0.6/0. Omitted now means "keep Unity's real default", not "force to zero".
                mat = new PhysicsMaterial
                {
                    dynamicFriction = p.DynamicFriction ?? 0.6f,
                    staticFriction  = p.StaticFriction ?? 0.6f,
                    bounciness      = p.Bounciness ?? 0f
                };

                if (!string.IsNullOrEmpty(p.FrictionCombine))
                {
                    if (!System.Enum.TryParse<PhysicsMaterialCombine>(p.FrictionCombine, ignoreCase: true, out var combine))
                        return ToolResult<PhysicsSetPhysicsMaterialResult>.Fail(
                            $"Unknown FrictionCombine '{p.FrictionCombine}'. Valid: Average, Minimum, Multiply, Maximum",
                            ErrorCodes.INVALID_PARAM);
                    mat.frictionCombine = combine;
                }
                if (!string.IsNullOrEmpty(p.BounceCombine))
                {
                    if (!System.Enum.TryParse<PhysicsMaterialCombine>(p.BounceCombine, ignoreCase: true, out var combine))
                        return ToolResult<PhysicsSetPhysicsMaterialResult>.Fail(
                            $"Unknown BounceCombine '{p.BounceCombine}'. Valid: Average, Minimum, Multiply, Maximum",
                            ErrorCodes.INVALID_PARAM);
                    mat.bounceCombine = combine;
                }

                if (!string.IsNullOrEmpty(p.AssetPath))
                {
                    var absoluteDir = Path.GetDirectoryName(
                        Path.Combine(Application.dataPath, "..", p.AssetPath));
                    if (!string.IsNullOrEmpty(absoluteDir))
                        Directory.CreateDirectory(absoluteDir);

                    AssetDatabase.CreateAsset(mat, p.AssetPath);
                    AssetDatabase.SaveAssets();
                    savedAsAsset = true;
                    assetPath = p.AssetPath;
                }
            }

            collider.sharedMaterial = mat;

            int childrenApplied = 0;
            if (p.ApplyToChildren)
            {
                foreach (var child in go.GetComponentsInChildren<Collider>())
                {
                    if (child == collider) continue;
                    Undo.RecordObject(child, "Mosaic: Set PhysicsMaterial");
                    child.sharedMaterial = mat;
                    childrenApplied++;
                }
            }

            return ToolResult<PhysicsSetPhysicsMaterialResult>.Ok(new PhysicsSetPhysicsMaterialResult
            {
                GameObjectName  = go.name,
                InstanceId      = UnityIds.Of(go),
                DynamicFriction = mat.dynamicFriction,
                StaticFriction  = mat.staticFriction,
                Bounciness      = mat.bounciness,
                AssetPath       = assetPath,
                SavedAsAsset    = savedAsAsset,
                FrictionCombine = mat.frictionCombine.ToString(),
                BounceCombine   = mat.bounceCombine.ToString(),
                ChildrenAppliedCount = childrenApplied,
            });
        }
    }
}
