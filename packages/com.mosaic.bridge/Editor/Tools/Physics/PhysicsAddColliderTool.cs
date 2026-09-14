using System;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsAddColliderTool
    {
        [MosaicTool("physics/add-collider",
                    "Adds a collider component (Box, Sphere, Capsule, or Mesh) to a GameObject. " +
                    "Set AddRigidbody=true to also add a Rigidbody (skipped if one is already present). " +
                    "Mesh colliders: set Convex=true when the GameObject has (or will have) a non-kinematic " +
                    "Rigidbody — Unity rejects a concave MeshCollider there at runtime with no editor-time error.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsAddColliderResult> Execute(PhysicsAddColliderParams p)
        {
            var go = PhysicsToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<PhysicsAddColliderResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found",
                    ErrorCodes.NOT_FOUND);

            if (string.IsNullOrEmpty(p.Type))
                return ToolResult<PhysicsAddColliderResult>.Fail(
                    "Type is required (Box, Sphere, Capsule, or Mesh)",
                    ErrorCodes.INVALID_PARAM);

            Collider collider;
            string normalizedType = p.Type.Trim();

            switch (normalizedType.ToLowerInvariant())
            {
                case "box":
                    var box = Undo.AddComponent<BoxCollider>(go);
                    AutoSizeBox(go, box);
                    if (p.Center != null && p.Center.Length == 3)
                        box.center = new Vector3(p.Center[0], p.Center[1], p.Center[2]);
                    if (p.Size != null && p.Size.Length == 3)
                        box.size = new Vector3(p.Size[0], p.Size[1], p.Size[2]);
                    collider = box;
                    break;

                case "sphere":
                    var sphere = Undo.AddComponent<SphereCollider>(go);
                    AutoSizeSphere(go, sphere);
                    if (p.Center != null && p.Center.Length == 3)
                        sphere.center = new Vector3(p.Center[0], p.Center[1], p.Center[2]);
                    collider = sphere;
                    break;

                case "capsule":
                    var capsule = Undo.AddComponent<CapsuleCollider>(go);
                    AutoSizeCapsule(go, capsule);
                    if (p.Center != null && p.Center.Length == 3)
                        capsule.center = new Vector3(p.Center[0], p.Center[1], p.Center[2]);
                    collider = capsule;
                    break;

                case "mesh":
                    var meshFilter = go.GetComponent<MeshFilter>();
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                        return ToolResult<PhysicsAddColliderResult>.Fail(
                            "MeshCollider requires a MeshFilter with a valid mesh",
                            ErrorCodes.INVALID_PARAM);

                    // L11: a concave (non-convex) MeshCollider on a GameObject with a
                    // non-kinematic Rigidbody is invalid — Unity accepts it silently at edit
                    // time and then produces no collision at runtime, with no error anywhere.
                    // Reject the combination up front instead of letting it ship broken.
                    var existingRb = go.GetComponent<Rigidbody>();
                    bool willHaveDynamicRigidbody =
                        (existingRb != null && !existingRb.isKinematic) ||
                        (p.AddRigidbody == true && existingRb == null);
                    if (willHaveDynamicRigidbody && !p.Convex)
                        return ToolResult<PhysicsAddColliderResult>.Fail(
                            "A concave (non-convex) MeshCollider on a dynamic (non-kinematic) Rigidbody is " +
                            "invalid in Unity and silently produces no collision. Set Convex=true, or IsTrigger " +
                            "may also require Convex depending on your Unity version.",
                            ErrorCodes.INVALID_PARAM);

                    var mesh = Undo.AddComponent<MeshCollider>(go);
                    mesh.convex = p.Convex;
                    collider = mesh;
                    break;

                default:
                    return ToolResult<PhysicsAddColliderResult>.Fail(
                        $"Unknown collider type '{p.Type}'. Use Box, Sphere, Capsule, or Mesh.",
                        ErrorCodes.INVALID_PARAM);
            }

            if (p.IsTrigger.HasValue)
                collider.isTrigger = p.IsTrigger.Value;

            bool rigidbodyAdded = false;
            if (p.AddRigidbody == true && go.GetComponent<Rigidbody>() == null)
            {
                Undo.AddComponent<Rigidbody>(go);
                rigidbodyAdded = true;
            }

            // Build result with collider dimensions
            var result = new PhysicsAddColliderResult
            {
                GameObjectName = go.name,
                InstanceId     = UnityIds.Of(go),
                ColliderType   = normalizedType,
                IsTrigger      = collider.isTrigger,
                Center         = PhysicsToolHelpers.ToFloatArray(collider.bounds.center - go.transform.position),
                Size           = PhysicsToolHelpers.ToFloatArray(collider.bounds.size),
                RigidbodyAdded = rigidbodyAdded,
                Convex         = collider is MeshCollider mc ? mc.convex : (bool?)null
            };

            return ToolResult<PhysicsAddColliderResult>.Ok(result);
        }

        // L11: renderer.bounds is a WORLD-SPACE axis-aligned box — already inflated for any
        // rotated object (a rotated cube's world AABB is bigger than the cube). Mapping that
        // already-inflated size back through the inverse rotation does not undo the inflation,
        // so a rotated object got an oversized collider. MeshFilter.sharedMesh.bounds is the
        // mesh's own LOCAL-space bounds — unaffected by the object's rotation — and is what a
        // collider's local center/size should actually be measured against.
        private static bool TryGetLocalMeshBounds(GameObject go, out Bounds bounds)
        {
            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                bounds = meshFilter.sharedMesh.bounds;
                return true;
            }
            var skinned = go.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null && skinned.sharedMesh != null)
            {
                bounds = skinned.sharedMesh.bounds;
                return true;
            }
            bounds = default;
            return false;
        }

        private static void AutoSizeBox(GameObject go, BoxCollider box)
        {
            if (TryGetLocalMeshBounds(go, out var local))
            {
                box.center = local.center;
                box.size = local.size;
                return;
            }
            // No mesh (e.g. a sprite-only renderer) — fall back to the world-AABB approximation.
            // Still approximate for a rotated object, but no worse than before for this case.
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var bounds = renderer.bounds;
            box.center = go.transform.InverseTransformPoint(bounds.center);
            box.size = go.transform.InverseTransformVector(bounds.size);
            box.size = new Vector3(
                Mathf.Abs(box.size.x),
                Mathf.Abs(box.size.y),
                Mathf.Abs(box.size.z));
        }

        private static void AutoSizeSphere(GameObject go, SphereCollider sphere)
        {
            if (TryGetLocalMeshBounds(go, out var local))
            {
                sphere.center = local.center;
                sphere.radius = Mathf.Max(local.extents.x, Mathf.Max(local.extents.y, local.extents.z));
                return;
            }
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var bounds = renderer.bounds;
            sphere.center = go.transform.InverseTransformPoint(bounds.center);
            var extents = bounds.extents;
            sphere.radius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z))
                          / Mathf.Max(go.transform.lossyScale.x,
                                      Mathf.Max(go.transform.lossyScale.y,
                                                go.transform.lossyScale.z));
        }

        private static void AutoSizeCapsule(GameObject go, CapsuleCollider capsule)
        {
            if (TryGetLocalMeshBounds(go, out var local))
            {
                capsule.center = local.center;
                capsule.height = local.size.y;
                capsule.radius = Mathf.Max(local.extents.x, local.extents.z);
                return;
            }
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var bounds = renderer.bounds;
            capsule.center = go.transform.InverseTransformPoint(bounds.center);
            var extents = bounds.extents;
            capsule.height = extents.y * 2f / go.transform.lossyScale.y;
            capsule.radius = Mathf.Max(extents.x, extents.z)
                           / Mathf.Max(go.transform.lossyScale.x, go.transform.lossyScale.z);
        }
    }
}
