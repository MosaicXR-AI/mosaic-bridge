using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsRaycastTool
    {
        [MosaicTool("physics/raycast",
                    "Executes a Physics.Raycast and returns hit information",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<PhysicsRaycastResult> Execute(PhysicsRaycastParams p)
        {
            if (p.Origin == null || p.Origin.Length != 3)
                return ToolResult<PhysicsRaycastResult>.Fail(
                    "Origin must be a float[3] array", ErrorCodes.INVALID_PARAM);

            if (p.Direction == null || p.Direction.Length != 3)
                return ToolResult<PhysicsRaycastResult>.Fail(
                    "Direction must be a float[3] array", ErrorCodes.INVALID_PARAM);

            var origin    = new Vector3(p.Origin[0], p.Origin[1], p.Origin[2]);
            var direction = new Vector3(p.Direction[0], p.Direction[1], p.Direction[2]);
            float maxDist = p.MaxDistance ?? Mathf.Infinity;
            int layerMask = p.LayerMask ?? UnityEngine.Physics.DefaultRaycastLayers;

            RaycastHit hit;
            bool didHit = UnityEngine.Physics.Raycast(origin, direction, out hit, maxDist, layerMask);

            if (!didHit)
            {
                return ToolResult<PhysicsRaycastResult>.Ok(new PhysicsRaycastResult
                {
                    Hit = false
                });
            }

            return ToolResult<PhysicsRaycastResult>.Ok(new PhysicsRaycastResult
            {
                Hit                  = true,
                Point                = PhysicsToolHelpers.ToFloatArray(hit.point),
                Normal               = PhysicsToolHelpers.ToFloatArray(hit.normal),
                Distance             = hit.distance,
                ColliderName         = hit.collider.name,
                GameObjectName       = hit.collider.gameObject.name,
                GameObjectInstanceId = hit.collider.gameObject.GetInstanceID()
            });
        }
    }
}
