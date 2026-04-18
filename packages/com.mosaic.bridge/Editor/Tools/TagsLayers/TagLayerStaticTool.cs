using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.TagsLayers
{
    public static class TagLayerStaticTool
    {
        [MosaicTool("taglayer/static",
                    "Manages static editor flags on a GameObject: get or set flags like BatchingStatic, OccludeeStatic, NavigationStatic, etc.",
                    isReadOnly: false)]
        public static ToolResult<TagLayerStaticResult> Execute(TagLayerStaticParams p)
        {
            switch (p.Action.ToLowerInvariant())
            {
                case "get":
                    return GetFlags(p);
                case "set":
                    return SetFlags(p);
                default:
                    return ToolResult<TagLayerStaticResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: get, set",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<TagLayerStaticResult> GetFlags(TagLayerStaticParams p)
        {
            var go = TagLayerHelpers.FindGameObject(p.InstanceId, p.GameObjectName);
            if (go == null)
                return ToolResult<TagLayerStaticResult>.Fail(
                    "GameObject not found. Provide a valid InstanceId or GameObjectName",
                    ErrorCodes.NOT_FOUND);

            var flags = GameObjectUtility.GetStaticEditorFlags(go);

            return ToolResult<TagLayerStaticResult>.Ok(new TagLayerStaticResult
            {
                GameObjectName = go.name,
                Flags = TagLayerHelpers.StaticFlagsToString(flags),
                RawValue = (int)flags
            });
        }

        private static ToolResult<TagLayerStaticResult> SetFlags(TagLayerStaticParams p)
        {
            if (string.IsNullOrEmpty(p.Flags))
                return ToolResult<TagLayerStaticResult>.Fail(
                    "Flags is required for 'set' action. Use comma-separated flag names (e.g. 'BatchingStatic, OccludeeStatic') or 'Everything'/'Nothing'",
                    ErrorCodes.INVALID_PARAM);

            var go = TagLayerHelpers.FindGameObject(p.InstanceId, p.GameObjectName);
            if (go == null)
                return ToolResult<TagLayerStaticResult>.Fail(
                    "GameObject not found. Provide a valid InstanceId or GameObjectName",
                    ErrorCodes.NOT_FOUND);

            if (!TagLayerHelpers.TryParseStaticFlags(p.Flags, out var parsedFlags))
                return ToolResult<TagLayerStaticResult>.Fail(
                    $"Invalid static flags '{p.Flags}'. Valid flags: Everything, Nothing, BatchingStatic, OccludeeStatic, OccluderStatic, NavigationStatic, OffMeshLinkGeneration, ReflectionProbeStatic",
                    ErrorCodes.INVALID_PARAM);

            Undo.RecordObject(go, "Mosaic: Set Static Flags");
            GameObjectUtility.SetStaticEditorFlags(go, parsedFlags);

            var resultFlags = GameObjectUtility.GetStaticEditorFlags(go);
            return ToolResult<TagLayerStaticResult>.Ok(new TagLayerStaticResult
            {
                GameObjectName = go.name,
                Flags = TagLayerHelpers.StaticFlagsToString(resultFlags),
                RawValue = (int)resultFlags
            });
        }
    }
}
