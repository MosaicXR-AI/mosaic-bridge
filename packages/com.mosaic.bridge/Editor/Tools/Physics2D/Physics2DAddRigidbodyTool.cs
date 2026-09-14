using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Physics2D
{
    public static class Physics2DAddRigidbodyTool
    {
        // O4 §4.1 (G1): mirror of physics/add-rigidbody for the 2D physics engine. FreezeRotation +
        // CollisionDetectionMode=Continuous are called out by name in the field report as "in every
        // platformer tutorial" — exposed directly rather than only via a generic Constraints param.
        [MosaicTool("physics2d/add-rigidbody",
                    "Adds a Rigidbody2D to a GameObject. FreezeRotation is the platformer-controller shorthand " +
                    "for RigidbodyConstraints2D.FreezeRotation. CollisionDetectionMode=Continuous prevents a " +
                    "fast-moving body from tunnelling through a thin collider.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<Physics2DAddRigidbodyResult> Execute(Physics2DAddRigidbodyParams p)
        {
            var go = Physics2DToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<Physics2DAddRigidbodyResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            if (!TryParseBodyType(p.BodyType, out var bodyType))
                return ToolResult<Physics2DAddRigidbodyResult>.Fail(
                    $"Unknown BodyType '{p.BodyType}'. Valid: Dynamic, Kinematic, Static", ErrorCodes.INVALID_PARAM);
            if (!TryParseInterpolation(p.Interpolation, out var interpolation))
                return ToolResult<Physics2DAddRigidbodyResult>.Fail(
                    $"Unknown Interpolation '{p.Interpolation}'. Valid: None, Interpolate, Extrapolate", ErrorCodes.INVALID_PARAM);
            if (!TryParseCollisionDetection(p.CollisionDetectionMode, out var collisionMode))
                return ToolResult<Physics2DAddRigidbodyResult>.Fail(
                    $"Unknown CollisionDetectionMode '{p.CollisionDetectionMode}'. Valid: Discrete, Continuous",
                    ErrorCodes.INVALID_PARAM);

            PhysicsMaterial2D sharedMaterial = null;
            if (!string.IsNullOrEmpty(p.SharedMaterialPath))
            {
                sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(p.SharedMaterialPath);
                if (sharedMaterial == null)
                    return ToolResult<Physics2DAddRigidbodyResult>.Fail(
                        $"No PhysicsMaterial2D found at '{p.SharedMaterialPath}'", ErrorCodes.NOT_FOUND);
            }

            var rb = Undo.AddComponent<Rigidbody2D>(go);
            rb.bodyType = bodyType;
            if (p.Mass.HasValue) rb.mass = p.Mass.Value;
            if (p.GravityScale.HasValue) rb.gravityScale = p.GravityScale.Value;
            if (p.LinearDamping.HasValue) rb.linearDamping = p.LinearDamping.Value;
            if (p.AngularDamping.HasValue) rb.angularDamping = p.AngularDamping.Value;
            if (p.FreezeRotation == true) rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = interpolation;
            rb.collisionDetectionMode = collisionMode;
            if (sharedMaterial != null) rb.sharedMaterial = sharedMaterial;

            return ToolResult<Physics2DAddRigidbodyResult>.Ok(new Physics2DAddRigidbodyResult
            {
                GameObjectName = go.name,
                InstanceId = UnityIds.Of(go),
                BodyType = rb.bodyType.ToString(),
                Mass = rb.mass,
                GravityScale = rb.gravityScale,
                FreezeRotation = rb.constraints == RigidbodyConstraints2D.FreezeRotation,
                Interpolation = rb.interpolation.ToString(),
                CollisionDetectionMode = rb.collisionDetectionMode.ToString(),
            });
        }

        private static bool TryParseBodyType(string value, out RigidbodyType2D result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "dynamic":   result = RigidbodyType2D.Dynamic;   return true;
                case "kinematic": result = RigidbodyType2D.Kinematic; return true;
                case "static":    result = RigidbodyType2D.Static;    return true;
                default:          result = RigidbodyType2D.Dynamic;   return false;
            }
        }

        private static bool TryParseInterpolation(string value, out RigidbodyInterpolation2D result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "none":        result = RigidbodyInterpolation2D.None;        return true;
                case "interpolate": result = RigidbodyInterpolation2D.Interpolate; return true;
                case "extrapolate": result = RigidbodyInterpolation2D.Extrapolate; return true;
                default:            result = RigidbodyInterpolation2D.None;        return false;
            }
        }

        private static bool TryParseCollisionDetection(string value, out CollisionDetectionMode2D result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "discrete":   result = CollisionDetectionMode2D.Discrete;   return true;
                case "continuous": result = CollisionDetectionMode2D.Continuous; return true;
                default:           result = CollisionDetectionMode2D.Discrete;   return false;
            }
        }
    }
}
