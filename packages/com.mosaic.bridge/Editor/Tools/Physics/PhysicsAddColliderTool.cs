using System;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsAddColliderTool
    {
        [MosaicTool("physics/add-collider",
                    "Adds a collider component (Box, Sphere, Capsule, or Mesh) to a GameObject",
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
                    var mesh = Undo.AddComponent<MeshCollider>(go);
                    collider = mesh;
                    break;

                default:
                    return ToolResult<PhysicsAddColliderResult>.Fail(
                        $"Unknown collider type '{p.Type}'. Use Box, Sphere, Capsule, or Mesh.",
                        ErrorCodes.INVALID_PARAM);
            }

            if (p.IsTrigger.HasValue)
                collider.isTrigger = p.IsTrigger.Value;

            // Build result with collider dimensions
            var result = new PhysicsAddColliderResult
            {
                GameObjectName = go.name,
                InstanceId     = go.GetInstanceID(),
                ColliderType   = normalizedType,
                IsTrigger      = collider.isTrigger,
                Center         = PhysicsToolHelpers.ToFloatArray(collider.bounds.center - go.transform.position),
                Size           = PhysicsToolHelpers.ToFloatArray(collider.bounds.size)
            };

            return ToolResult<PhysicsAddColliderResult>.Ok(result);
        }

        private static void AutoSizeBox(GameObject go, BoxCollider box)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var bounds = renderer.bounds;
            box.center = go.transform.InverseTransformPoint(bounds.center);
            box.size = go.transform.InverseTransformVector(bounds.size);
            // Ensure positive sizes
            box.size = new Vector3(
                Mathf.Abs(box.size.x),
                Mathf.Abs(box.size.y),
                Mathf.Abs(box.size.z));
        }

        private static void AutoSizeSphere(GameObject go, SphereCollider sphere)
        {
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
