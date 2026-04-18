using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.TagsLayers
{
    public static class TagLayerLayerTool
    {
        [MosaicTool("taglayer/layer",
                    "Manages Unity layers: list all 32 layers or set a GameObject's layer",
                    isReadOnly: false)]
        public static ToolResult<TagLayerLayerResult> Execute(TagLayerLayerParams p)
        {
            switch (p.Action.ToLowerInvariant())
            {
                case "list":
                    return ListLayers();
                case "set":
                    return SetLayer(p);
                default:
                    return ToolResult<TagLayerLayerResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: list, set",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<TagLayerLayerResult> ListLayers()
        {
            var entries = new LayerEntry[32];
            for (int i = 0; i < 32; i++)
            {
                entries[i] = new LayerEntry
                {
                    Index = i,
                    Name = LayerMask.LayerToName(i)
                };
            }

            return ToolResult<TagLayerLayerResult>.Ok(new TagLayerLayerResult
            {
                Layers = entries
            });
        }

        private static ToolResult<TagLayerLayerResult> SetLayer(TagLayerLayerParams p)
        {
            var go = TagLayerHelpers.FindGameObject(p.InstanceId, p.GameObjectName);
            if (go == null)
                return ToolResult<TagLayerLayerResult>.Fail(
                    "GameObject not found. Provide a valid InstanceId or GameObjectName",
                    ErrorCodes.NOT_FOUND);

            int layerIndex;

            if (p.LayerIndex.HasValue)
            {
                layerIndex = p.LayerIndex.Value;
                if (layerIndex < 0 || layerIndex > 31)
                    return ToolResult<TagLayerLayerResult>.Fail(
                        $"LayerIndex {layerIndex} is out of range (0-31)",
                        ErrorCodes.OUT_OF_RANGE);
            }
            else if (!string.IsNullOrEmpty(p.LayerName))
            {
                layerIndex = LayerMask.NameToLayer(p.LayerName);
                if (layerIndex < 0)
                    return ToolResult<TagLayerLayerResult>.Fail(
                        $"Layer '{p.LayerName}' not found",
                        ErrorCodes.NOT_FOUND);
            }
            else
            {
                return ToolResult<TagLayerLayerResult>.Fail(
                    "Either LayerName or LayerIndex is required for 'set' action",
                    ErrorCodes.INVALID_PARAM);
            }

            Undo.RecordObject(go, "Mosaic: Set Layer");
            go.layer = layerIndex;

            return ToolResult<TagLayerLayerResult>.Ok(new TagLayerLayerResult
            {
                GameObjectName = go.name,
                AssignedLayerIndex = go.layer,
                AssignedLayerName = LayerMask.LayerToName(go.layer)
            });
        }
    }
}
