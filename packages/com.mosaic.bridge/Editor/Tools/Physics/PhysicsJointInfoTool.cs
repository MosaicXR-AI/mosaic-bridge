using System.Linq;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsJointInfoTool
    {
        [MosaicTool("physics/joint-info",
                    "Reports every Joint (Hinge/Fixed/Spring/Character/Configurable) on a GameObject: type, " +
                    "connected body, anchors, axis, break thresholds, and current force/torque (0 outside Play Mode).",
                    isReadOnly: true)]
        public static ToolResult<PhysicsJointInfoResult> Execute(PhysicsJointInfoParams p)
        {
            var go = PhysicsToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<PhysicsJointInfoResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            var joints = go.GetComponents<Joint>().Select(j => new PhysicsJointInfo
            {
                JointType         = j.GetType().Name,
                ConnectedBodyName = j.connectedBody != null ? j.connectedBody.name : null,
                Anchor            = PhysicsToolHelpers.ToFloatArray(j.anchor),
                ConnectedAnchor   = PhysicsToolHelpers.ToFloatArray(j.connectedAnchor),
                Axis              = PhysicsToolHelpers.ToFloatArray(j.axis),
                BreakForce        = j.breakForce,
                BreakTorque       = j.breakTorque,
                EnableCollision   = j.enableCollision,
                CurrentForce      = j.currentForce.magnitude,
                CurrentTorque     = j.currentTorque.magnitude,
            }).ToArray();

            return ToolResult<PhysicsJointInfoResult>.Ok(new PhysicsJointInfoResult
            {
                GameObjectName = go.name,
                InstanceId     = UnityIds.Of(go),
                Joints         = joints,
            });
        }
    }
}
