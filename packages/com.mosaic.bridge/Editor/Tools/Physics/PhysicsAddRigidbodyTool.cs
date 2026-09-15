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
                    "Adds a Rigidbody component to a GameObject with optional physics properties, or updates " +
                    "an existing one (L9: no longer fails/conflicts if a Rigidbody is already present). " +
                    "Interpolation/CollisionDetection are enum names; FreezePosition[XYZ]/FreezeRotation[XYZ] " +
                    "combine into Rigidbody.constraints; IncludeLayers/ExcludeLayers are comma-separated " +
                    "layer names.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsAddRigidbodyResult> Execute(PhysicsAddRigidbodyParams p)
        {
            var go = PhysicsToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<PhysicsAddRigidbodyResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found",
                    ErrorCodes.NOT_FOUND);

            var existing = go.GetComponent<Rigidbody>();
            bool wasExisting = existing != null;
            var rb = existing != null ? existing : Undo.AddComponent<Rigidbody>(go);
            if (wasExisting)
                Undo.RecordObject(rb, "Mosaic: Update Rigidbody");

            if (p.Mass.HasValue)       rb.mass        = p.Mass.Value;
            if (p.Drag.HasValue)       rb.linearDamping         = p.Drag.Value;
            if (p.AngularDrag.HasValue) rb.angularDamping  = p.AngularDrag.Value;
            if (p.UseGravity.HasValue) rb.useGravity   = p.UseGravity.Value;
            if (p.IsKinematic.HasValue) rb.isKinematic  = p.IsKinematic.Value;

            if (!string.IsNullOrEmpty(p.Interpolation))
            {
                if (!System.Enum.TryParse<RigidbodyInterpolation>(p.Interpolation, ignoreCase: true, out var interp))
                    return ToolResult<PhysicsAddRigidbodyResult>.Fail(
                        $"Unknown Interpolation '{p.Interpolation}'. Valid: None, Interpolate, Extrapolate",
                        ErrorCodes.INVALID_PARAM);
                rb.interpolation = interp;
            }

            if (!string.IsNullOrEmpty(p.CollisionDetection))
            {
                if (!System.Enum.TryParse<CollisionDetectionMode>(p.CollisionDetection, ignoreCase: true, out var mode))
                    return ToolResult<PhysicsAddRigidbodyResult>.Fail(
                        $"Unknown CollisionDetection '{p.CollisionDetection}'. Valid: Discrete, Continuous, " +
                        "ContinuousDynamic, ContinuousSpeculative", ErrorCodes.INVALID_PARAM);
                rb.collisionDetectionMode = mode;
            }

            if (HasAnyConstraintField(p))
            {
                var constraints = RigidbodyConstraints.None;
                if (p.FreezePositionX == true) constraints |= RigidbodyConstraints.FreezePositionX;
                if (p.FreezePositionY == true) constraints |= RigidbodyConstraints.FreezePositionY;
                if (p.FreezePositionZ == true) constraints |= RigidbodyConstraints.FreezePositionZ;
                if (p.FreezeRotationX == true) constraints |= RigidbodyConstraints.FreezeRotationX;
                if (p.FreezeRotationY == true) constraints |= RigidbodyConstraints.FreezeRotationY;
                if (p.FreezeRotationZ == true) constraints |= RigidbodyConstraints.FreezeRotationZ;
                rb.constraints = constraints;
            }

            if (p.CenterOfMass != null)
            {
                if (p.CenterOfMass.Length != 3)
                    return ToolResult<PhysicsAddRigidbodyResult>.Fail(
                        "CenterOfMass requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                rb.centerOfMass = new Vector3(p.CenterOfMass[0], p.CenterOfMass[1], p.CenterOfMass[2]);
            }
            if (p.MaxAngularVelocity.HasValue) rb.maxAngularVelocity = p.MaxAngularVelocity.Value;

            if (!string.IsNullOrEmpty(p.IncludeLayers))
                rb.includeLayers = LayerMask.GetMask(SplitLayerNames(p.IncludeLayers));
            if (!string.IsNullOrEmpty(p.ExcludeLayers))
                rb.excludeLayers = LayerMask.GetMask(SplitLayerNames(p.ExcludeLayers));

            return ToolResult<PhysicsAddRigidbodyResult>.Ok(new PhysicsAddRigidbodyResult
            {
                GameObjectName = go.name,
                InstanceId     = UnityIds.Of(go),
                Mass           = rb.mass,
                Drag           = rb.linearDamping,
                AngularDrag    = rb.angularDamping,
                UseGravity     = rb.useGravity,
                IsKinematic    = rb.isKinematic,
                WasExisting    = wasExisting,
                Interpolation  = rb.interpolation.ToString(),
                CollisionDetection = rb.collisionDetectionMode.ToString(),
                Constraints    = rb.constraints.ToString(),
                CenterOfMass   = new[] { rb.centerOfMass.x, rb.centerOfMass.y, rb.centerOfMass.z },
                MaxAngularVelocity = rb.maxAngularVelocity,
            });
        }

        private static bool HasAnyConstraintField(PhysicsAddRigidbodyParams p) =>
            p.FreezePositionX.HasValue || p.FreezePositionY.HasValue || p.FreezePositionZ.HasValue ||
            p.FreezeRotationX.HasValue || p.FreezeRotationY.HasValue || p.FreezeRotationZ.HasValue;

        private static string[] SplitLayerNames(string commaSeparated) =>
            System.Array.ConvertAll(commaSeparated.Split(','), s => s.Trim());
    }
}
