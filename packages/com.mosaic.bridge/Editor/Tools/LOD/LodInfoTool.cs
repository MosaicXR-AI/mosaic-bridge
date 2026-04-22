using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.LOD
{
    public static class LodInfoTool
    {
        [MosaicTool("lod/info",
                    "Queries the LODGroup component on a GameObject, returning LOD levels and renderer info",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<LodInfoResult> Info(LodInfoParams p)
        {
            if (string.IsNullOrEmpty(p.Name) && !p.InstanceId.HasValue)
                return ToolResult<LodInfoResult>.Fail(
                    "Either Name or InstanceId must be provided", ErrorCodes.INVALID_PARAM);

            var go = ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<LodInfoResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            var lodGroup = go.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                return ToolResult<LodInfoResult>.Ok(new LodInfoResult
                {
                    GameObjectName = go.name,
                    HasLodGroup    = false,
                    Levels         = new LodLevelInfo[0]
                });
            }

            var lods = lodGroup.GetLODs();
            var levels = new List<LodLevelInfo>();

            for (int i = 0; i < lods.Length; i++)
            {
                var rendererNames = new List<string>();
                foreach (var r in lods[i].renderers)
                {
                    rendererNames.Add(r != null ? r.name : "(null)");
                }

                levels.Add(new LodLevelInfo
                {
                    Index                = i,
                    ScreenRelativeHeight = lods[i].screenRelativeTransitionHeight,
                    RendererCount        = lods[i].renderers.Length,
                    RendererNames        = rendererNames.ToArray()
                });
            }

            return ToolResult<LodInfoResult>.Ok(new LodInfoResult
            {
                GameObjectName = go.name,
                HasLodGroup    = true,
                Levels         = levels.ToArray()
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
