using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Particles
{
    public static class ParticleSetShapeTool
    {
        private static readonly Dictionary<string, ParticleSystemShapeType> ShapeMap =
            new Dictionary<string, ParticleSystemShapeType>(StringComparer.OrdinalIgnoreCase)
            {
                { "Sphere",     ParticleSystemShapeType.Sphere },
                { "Hemisphere", ParticleSystemShapeType.Hemisphere },
                { "Cone",       ParticleSystemShapeType.Cone },
                { "Box",        ParticleSystemShapeType.Box },
                { "Mesh",       ParticleSystemShapeType.Mesh },
                { "Edge",       ParticleSystemShapeType.SingleSidedEdge }
            };

        [MosaicTool("particle/set-shape",
                    "Sets shape module properties on a ParticleSystem",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ParticleSetShapeResult> Execute(ParticleSetShapeParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.Name))
                return ToolResult<ParticleSetShapeResult>.Fail(
                    "Either InstanceId or Name is required", ErrorCodes.INVALID_PARAM);

            var ps = ParticleToolHelpers.Resolve(p.InstanceId, p.Name);
            if (ps == null)
                return ToolResult<ParticleSetShapeResult>.Fail(
                    $"ParticleSystem not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(ps, "Mosaic: Set ParticleSystem Shape");

            var shape = ps.shape;

            if (!string.IsNullOrEmpty(p.Shape))
            {
                if (!ShapeMap.TryGetValue(p.Shape, out var shapeType))
                    return ToolResult<ParticleSetShapeResult>.Fail(
                        $"Unknown shape '{p.Shape}'. Valid values: Sphere, Hemisphere, Cone, Box, Mesh, Edge",
                        ErrorCodes.INVALID_PARAM);
                shape.shapeType = shapeType;
            }

            if (p.Radius.HasValue)
                shape.radius = p.Radius.Value;
            if (p.Angle.HasValue)
                shape.angle = p.Angle.Value;
            if (p.Arc.HasValue)
                shape.arc = p.Arc.Value;

            EditorUtility.SetDirty(ps);

            return ToolResult<ParticleSetShapeResult>.Ok(new ParticleSetShapeResult
            {
                InstanceId = ps.gameObject.GetInstanceID(),
                Name       = ps.gameObject.name,
                Shape      = shape.shapeType.ToString(),
                Radius     = shape.radius,
                Angle      = shape.angle,
                Arc        = shape.arc
            });
        }
    }
}
