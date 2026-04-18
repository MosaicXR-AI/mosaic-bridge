using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.LOD
{
    public static class LodCreateTool
    {
        [MosaicTool("lod/create",
                    "Adds a LODGroup component to a GameObject and configures LOD levels",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<LodCreateResult> Create(LodCreateParams p)
        {
            if (string.IsNullOrEmpty(p.Name) && !p.InstanceId.HasValue)
                return ToolResult<LodCreateResult>.Fail(
                    "Either Name or InstanceId must be provided", ErrorCodes.INVALID_PARAM);

            var go = ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<LodCreateResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            if (p.Levels == null || p.Levels.Length == 0)
                return ToolResult<LodCreateResult>.Fail(
                    "At least one LOD level is required", ErrorCodes.INVALID_PARAM);

            var lodGroup = Undo.AddComponent<LODGroup>(go);

            var lods = new UnityEngine.LOD[p.Levels.Length];
            var screenHeights = new List<float>();

            for (int i = 0; i < p.Levels.Length; i++)
            {
                var level = p.Levels[i];
                screenHeights.Add(level.ScreenHeight);

                var renderers = new List<Renderer>();
                if (level.RendererPaths != null)
                {
                    foreach (var path in level.RendererPaths)
                    {
                        var child = go.transform.Find(path);
                        if (child != null)
                        {
                            var renderer = child.GetComponent<Renderer>();
                            if (renderer != null)
                                renderers.Add(renderer);
                        }
                    }
                }

                // If no renderer paths specified, use renderers on the GO itself for the first level
                if (renderers.Count == 0 && i == 0)
                {
                    var selfRenderer = go.GetComponent<Renderer>();
                    if (selfRenderer != null)
                        renderers.Add(selfRenderer);
                }

                lods[i] = new UnityEngine.LOD(level.ScreenHeight, renderers.ToArray());
            }

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            return ToolResult<LodCreateResult>.Ok(new LodCreateResult
            {
                GameObjectName = go.name,
                LodLevelCount  = p.Levels.Length,
                ScreenHeights  = screenHeights.ToArray()
            });
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
