using System;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Lighting
{
    public static class LightingCreateTool
    {
        [MosaicTool("lighting/create",
                    "Creates a new Light in the currently open scene",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<LightingCreateResult> Execute(LightingCreateParams p)
        {
            if (!Enum.TryParse<LightType>(p.Type, true, out var lightType))
                return ToolResult<LightingCreateResult>.Fail(
                    $"Invalid light type '{p.Type}'. Valid types: Directional, Point, Spot, Area",
                    ErrorCodes.INVALID_PARAM);

            string name = !string.IsNullOrEmpty(p.Name) ? p.Name : p.Type + " Light";
            var go = new GameObject(name);

            var light = go.AddComponent<Light>();
            light.type = lightType;

            if (p.Color != null)
            {
                var color = LightingToolHelpers.ParseColor(p.Color);
                if (color.HasValue)
                    light.color = color.Value;
            }

            if (p.Intensity.HasValue)
                light.intensity = p.Intensity.Value;

            if (p.Range.HasValue)
                light.range = p.Range.Value;

            if (p.SpotAngle.HasValue)
                light.spotAngle = p.SpotAngle.Value;

            if (p.Position != null && p.Position.Length == 3)
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            if (p.Rotation != null && p.Rotation.Length == 3)
                go.transform.eulerAngles = new Vector3(p.Rotation[0], p.Rotation[1], p.Rotation[2]);

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create Light");

            return ToolResult<LightingCreateResult>.Ok(new LightingCreateResult
            {
                InstanceId    = go.GetInstanceID(),
                Name          = go.name,
                HierarchyPath = LightingToolHelpers.GetHierarchyPath(go.transform),
                LightType     = lightType.ToString(),
                Intensity     = light.intensity
            });
        }
    }
}
