using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Constraints
{
    public static class ConstraintAddTool
    {
        [MosaicTool("constraint/add",
                    "Adds a constraint component (Position, Rotation, Scale, Aim, or Parent) to a GameObject",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ConstraintAddResult> Add(ConstraintAddParams p)
        {
            if (string.IsNullOrEmpty(p.Name) && !p.InstanceId.HasValue)
                return ToolResult<ConstraintAddResult>.Fail(
                    "Either Name or InstanceId must be provided", ErrorCodes.INVALID_PARAM);

            var go = ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<ConstraintAddResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            var constraintType = p.Type?.Trim().ToLowerInvariant();
            Type componentType;
            switch (constraintType)
            {
                case "position":    componentType = typeof(PositionConstraint); break;
                case "rotation":    componentType = typeof(RotationConstraint); break;
                case "scale":       componentType = typeof(ScaleConstraint);    break;
                case "aim":         componentType = typeof(AimConstraint);      break;
                case "parent":      componentType = typeof(ParentConstraint);   break;
                default:
                    return ToolResult<ConstraintAddResult>.Fail(
                        $"Unknown constraint type '{p.Type}'. Valid types: Position, Rotation, Scale, Aim, Parent",
                        ErrorCodes.INVALID_PARAM);
            }

            var component = Undo.AddComponent(go, componentType) as IConstraint;

            bool sourceAssigned = false;
            if (p.SourceInstanceId.HasValue && p.SourceInstanceId.Value != 0)
            {
#pragma warning disable CS0618
                var sourceGo = UnityEngine.Resources.EntityIdToObject(p.SourceInstanceId.Value) as GameObject;
#pragma warning restore CS0618
                if (sourceGo != null)
                {
                    var source = new ConstraintSource
                    {
                        sourceTransform = sourceGo.transform,
                        weight = 1f
                    };
                    component.AddSource(source);
                    sourceAssigned = true;
                }
            }

            return ToolResult<ConstraintAddResult>.Ok(new ConstraintAddResult
            {
                GameObjectName = go.name,
                ConstraintType = p.Type,
                ComponentType  = componentType.Name,
                SourceAssigned = sourceAssigned
            });
        }

        private static GameObject ResolveGameObject(int? instanceId, string name)
        {
            GameObject go = null;
            if (instanceId.HasValue && instanceId.Value != 0)
            {
#pragma warning disable CS0618
                go = UnityEngine.Resources.EntityIdToObject(instanceId.Value) as GameObject;
#pragma warning restore CS0618
            }
            if (go == null && !string.IsNullOrEmpty(name))
                go = GameObject.Find(name);
            return go;
        }
    }
}
