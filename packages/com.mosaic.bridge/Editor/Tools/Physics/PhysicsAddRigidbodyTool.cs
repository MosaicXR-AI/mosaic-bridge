using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsAddRigidbodyTool
    {
        [MosaicTool("physics/add-rigidbody",
                    "Adds a Rigidbody component to a GameObject with optional physics properties",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsAddRigidbodyResult> Execute(PhysicsAddRigidbodyParams p)
        {
            var go = PhysicsToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<PhysicsAddRigidbodyResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found",
                    ErrorCodes.NOT_FOUND);

            // L9: Undo.AddComponent<T> returns null when the GameObject already has a component
            // of that type — every field access below would have thrown NullReferenceException.
            if (go.GetComponent<Rigidbody>() != null)
                return ToolResult<PhysicsAddRigidbodyResult>.Fail(
                    $"GameObject '{go.name}' already has a Rigidbody component.", ErrorCodes.CONFLICT);

            var rb = Undo.AddComponent<Rigidbody>(go);

            if (p.Mass.HasValue)       rb.mass        = p.Mass.Value;
            if (p.Drag.HasValue)       rb.linearDamping         = p.Drag.Value;
            if (p.AngularDrag.HasValue) rb.angularDamping  = p.AngularDrag.Value;
            if (p.UseGravity.HasValue) rb.useGravity   = p.UseGravity.Value;
            if (p.IsKinematic.HasValue) rb.isKinematic  = p.IsKinematic.Value;

            return ToolResult<PhysicsAddRigidbodyResult>.Ok(new PhysicsAddRigidbodyResult
            {
                GameObjectName = go.name,
                InstanceId     = UnityIds.Of(go),
                Mass           = rb.mass,
                Drag           = rb.linearDamping,
                AngularDrag    = rb.angularDamping,
                UseGravity     = rb.useGravity,
                IsKinematic    = rb.isKinematic
            });
        }
    }
}
