using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Constraints
{
    public static class ConstraintInfoTool
    {
        [MosaicTool("constraint/info",
                    "Queries all constraint components on a GameObject",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<ConstraintInfoResult> Info(ConstraintInfoParams p)
        {
            if (string.IsNullOrEmpty(p.Name) && !p.InstanceId.HasValue)
                return ToolResult<ConstraintInfoResult>.Fail(
                    "Either Name or InstanceId must be provided", ErrorCodes.INVALID_PARAM);

            var go = ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<ConstraintInfoResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            var constraints = go.GetComponents<IConstraint>();
            var entries = new List<ConstraintEntry>();

            foreach (var c in constraints)
            {
                var behaviour = c as Behaviour;
                var sourceNames = new List<string>();
                for (int i = 0; i < c.sourceCount; i++)
                {
                    var src = c.GetSource(i);
                    sourceNames.Add(src.sourceTransform != null ? src.sourceTransform.name : "(null)");
                }

                entries.Add(new ConstraintEntry
                {
                    Type          = GetConstraintTypeName(c),
                    ComponentType = c.GetType().Name,
                    Weight        = c.weight,
                    IsActive      = behaviour != null && behaviour.enabled,
                    SourceCount   = c.sourceCount,
                    SourceNames   = sourceNames.ToArray()
                });
            }

            return ToolResult<ConstraintInfoResult>.Ok(new ConstraintInfoResult
            {
                GameObjectName = go.name,
                Constraints    = entries.ToArray()
            });
        }

        private static string GetConstraintTypeName(IConstraint c)
        {
            if (c is PositionConstraint) return "Position";
            if (c is RotationConstraint) return "Rotation";
            if (c is ScaleConstraint)    return "Scale";
            if (c is AimConstraint)      return "Aim";
            if (c is ParentConstraint)   return "Parent";
            return c.GetType().Name;
        }

        private static GameObject ResolveGameObject(int? instanceId, string name)
        {
            GameObject go = null;
            if (instanceId.HasValue && instanceId.Value != 0)
            {
#pragma warning disable CS0618
                go = EditorUtility.InstanceIDToObject(instanceId.Value) as GameObject;
#pragma warning restore CS0618
            }
            if (go == null && !string.IsNullOrEmpty(name))
                go = GameObject.Find(name);
            return go;
        }
    }
}
