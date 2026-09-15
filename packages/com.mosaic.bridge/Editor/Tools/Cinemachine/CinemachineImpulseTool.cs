#if MOSAIC_HAS_CINEMACHINE
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineImpulseTool
    {
        [MosaicTool("cinemachine/impulse",
                    "add-source: adds a CinemachineImpulseSource to TargetName (ImpulseShape: Bump, " +
                    "Custom, Explosion, Recoil, Rumble; ImpulseDuration/Channel; DefaultVelocity " +
                    "[x,y,z]) — 'camera shake on landing'. add-collision-source: adds a " +
                    "CinemachineCollisionImpulseSource instead, which auto-generates the impulse from " +
                    "physics collisions on CollisionLayerMask (IgnoreTag, ScaleImpactWithSpeed/Mass, " +
                    "UseImpactDirection). GenerateImpulse itself only fires at runtime, not from this tool.",
                    isReadOnly: false)]
        public static ToolResult<CinemachineImpulseResult> Execute(CinemachineImpulseParams p)
        {
            var go = GameObject.Find(p.TargetName);
            if (go == null)
                return ToolResult<CinemachineImpulseResult>.Fail(
                    $"GameObject '{p.TargetName}' not found", ErrorCodes.NOT_FOUND);

            CinemachineImpulseSource source;
            switch (p.Action?.ToLowerInvariant())
            {
                case "add-source":
                    source = go.AddComponent<CinemachineImpulseSource>();
                    break;
                case "add-collision-source":
                {
                    var collisionSource = go.AddComponent<CinemachineCollisionImpulseSource>();
                    if (!string.IsNullOrEmpty(p.CollisionLayerMask))
                        collisionSource.LayerMask = LayerMask.GetMask(
                            System.Array.ConvertAll(p.CollisionLayerMask.Split(','), s => s.Trim()));
                    if (!string.IsNullOrEmpty(p.IgnoreTag)) collisionSource.IgnoreTag = p.IgnoreTag;
                    if (p.ScaleImpactWithSpeed.HasValue) collisionSource.ScaleImpactWithSpeed = p.ScaleImpactWithSpeed.Value;
                    if (p.ScaleImpactWithMass.HasValue) collisionSource.ScaleImpactWithMass = p.ScaleImpactWithMass.Value;
                    if (p.UseImpactDirection.HasValue) collisionSource.UseImpactDirection = p.UseImpactDirection.Value;
                    source = collisionSource;
                    break;
                }
                default:
                    return ToolResult<CinemachineImpulseResult>.Fail(
                        $"Unknown Action '{p.Action}'. Valid: add-source, add-collision-source", ErrorCodes.INVALID_PARAM);
            }

            var definition = source.ImpulseDefinition;
            if (!string.IsNullOrEmpty(p.ImpulseShape))
            {
                if (!TryParseImpulseShape(p.ImpulseShape, out var shape))
                    return ToolResult<CinemachineImpulseResult>.Fail(
                        $"Unknown ImpulseShape '{p.ImpulseShape}'. Valid: Bump, Custom, Explosion, Recoil, Rumble",
                        ErrorCodes.INVALID_PARAM);
                definition.ImpulseShape = shape;
            }
            if (p.ImpulseDuration.HasValue) definition.ImpulseDuration = p.ImpulseDuration.Value;
            if (p.ImpulseChannel.HasValue) definition.ImpulseChannel = p.ImpulseChannel.Value;
            source.ImpulseDefinition = definition;

            if (p.DefaultVelocity != null)
            {
                if (p.DefaultVelocity.Length != 3)
                    return ToolResult<CinemachineImpulseResult>.Fail(
                        "DefaultVelocity requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                source.DefaultVelocity = new Vector3(p.DefaultVelocity[0], p.DefaultVelocity[1], p.DefaultVelocity[2]);
            }

            EditorUtility.SetDirty(go);

            return ToolResult<CinemachineImpulseResult>.Ok(new CinemachineImpulseResult
            {
                Action = p.Action,
                TargetName = go.name,
                ImpulseShape = source.ImpulseDefinition.ImpulseShape.ToString(),
                ImpulseDuration = source.ImpulseDefinition.ImpulseDuration,
                ImpulseChannel = source.ImpulseDefinition.ImpulseChannel,
            });
        }

        private static bool TryParseImpulseShape(string value, out CinemachineImpulseDefinition.ImpulseShapes result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "bump":      result = CinemachineImpulseDefinition.ImpulseShapes.Bump;      return true;
                case "custom":    result = CinemachineImpulseDefinition.ImpulseShapes.Custom;    return true;
                case "explosion": result = CinemachineImpulseDefinition.ImpulseShapes.Explosion; return true;
                case "recoil":    result = CinemachineImpulseDefinition.ImpulseShapes.Recoil;    return true;
                case "rumble":    result = CinemachineImpulseDefinition.ImpulseShapes.Rumble;    return true;
                default:          result = CinemachineImpulseDefinition.ImpulseShapes.Bump;      return false;
            }
        }
    }
}
#endif
