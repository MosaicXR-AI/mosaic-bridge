using System;
using System.Linq;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsOverlapTool
    {
        [MosaicTool("physics/overlap",
                    "Executes an overlap test (sphere, box, or capsule) and returns colliders found",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<PhysicsOverlapResult> Execute(PhysicsOverlapParams p)
        {
            if (p.Position == null || p.Position.Length != 3)
                return ToolResult<PhysicsOverlapResult>.Fail(
                    "Position must be a float[3] array", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.Action))
                return ToolResult<PhysicsOverlapResult>.Fail(
                    "Action is required (sphere, box, or capsule)", ErrorCodes.INVALID_PARAM);

            var center = new Vector3(p.Position[0], p.Position[1], p.Position[2]);
            int layerMask = p.LayerMask ?? UnityEngine.Physics.DefaultRaycastLayers;

            Collider[] colliders;

            switch (p.Action.Trim().ToLowerInvariant())
            {
                case "sphere":
                    if (!p.Radius.HasValue)
                        return ToolResult<PhysicsOverlapResult>.Fail(
                            "Radius is required for sphere overlap", ErrorCodes.INVALID_PARAM);
                    colliders = UnityEngine.Physics.OverlapSphere(center, p.Radius.Value, layerMask);
                    break;

                case "box":
                    if (p.Size == null || p.Size.Length != 3)
                        return ToolResult<PhysicsOverlapResult>.Fail(
                            "Size must be a float[3] array for box overlap", ErrorCodes.INVALID_PARAM);
                    var halfExtents = new Vector3(p.Size[0] / 2f, p.Size[1] / 2f, p.Size[2] / 2f);
                    colliders = UnityEngine.Physics.OverlapBox(center, halfExtents, Quaternion.identity, layerMask);
                    break;

                case "capsule":
                    if (!p.Radius.HasValue)
                        return ToolResult<PhysicsOverlapResult>.Fail(
                            "Radius is required for capsule overlap", ErrorCodes.INVALID_PARAM);
                    float height = p.Height ?? (p.Radius.Value * 2f);
                    float halfHeight = Mathf.Max(0f, height / 2f - p.Radius.Value);
                    var point0 = center + Vector3.up * halfHeight;
                    var point1 = center - Vector3.up * halfHeight;
                    colliders = UnityEngine.Physics.OverlapCapsule(point0, point1, p.Radius.Value, layerMask);
                    break;

                default:
                    return ToolResult<PhysicsOverlapResult>.Fail(
                        $"Unknown overlap action '{p.Action}'. Use sphere, box, or capsule.",
                        ErrorCodes.INVALID_PARAM);
            }

            var hits = colliders.Select(c => new OverlapHit
            {
                ColliderName         = c.name,
                GameObjectName       = c.gameObject.name,
                GameObjectInstanceId = c.gameObject.GetInstanceID()
            }).ToArray();

            return ToolResult<PhysicsOverlapResult>.Ok(new PhysicsOverlapResult
            {
                Count     = hits.Length,
                Colliders = hits
            });
        }
    }
}
