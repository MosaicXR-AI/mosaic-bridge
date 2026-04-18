using System;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.TagsLayers
{
    public static class TagLayerTagTool
    {
        [MosaicTool("taglayer/tag",
                    "Manages Unity tags: list all tags, add a new tag, or set a GameObject's tag",
                    isReadOnly: false)]
        public static ToolResult<TagLayerTagResult> Execute(TagLayerTagParams p)
        {
            switch (p.Action.ToLowerInvariant())
            {
                case "list":
                    return ListTags();
                case "add":
                    return AddTag(p);
                case "set":
                    return SetTag(p);
                default:
                    return ToolResult<TagLayerTagResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: list, add, set",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<TagLayerTagResult> ListTags()
        {
            string[] tags = InternalEditorUtility.tags;
            return ToolResult<TagLayerTagResult>.Ok(new TagLayerTagResult
            {
                Tags = tags
            });
        }

        private static ToolResult<TagLayerTagResult> AddTag(TagLayerTagParams p)
        {
            if (string.IsNullOrEmpty(p.TagName))
                return ToolResult<TagLayerTagResult>.Fail(
                    "TagName is required for 'add' action", ErrorCodes.INVALID_PARAM);

            // Check if tag already exists
            string[] existingTags = InternalEditorUtility.tags;
            foreach (string tag in existingTags)
            {
                if (string.Equals(tag, p.TagName, StringComparison.Ordinal))
                    return ToolResult<TagLayerTagResult>.Fail(
                        $"Tag '{p.TagName}' already exists", ErrorCodes.CONFLICT);
            }

            // Add tag via TagManager SerializedObject
            var tagManager = new SerializedObject(
                AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
            var tagsProp = tagManager.FindProperty("tags");

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = p.TagName;
            tagManager.ApplyModifiedProperties();

            return ToolResult<TagLayerTagResult>.Ok(new TagLayerTagResult
            {
                AddedTag = p.TagName
            });
        }

        private static ToolResult<TagLayerTagResult> SetTag(TagLayerTagParams p)
        {
            if (string.IsNullOrEmpty(p.TagName))
                return ToolResult<TagLayerTagResult>.Fail(
                    "TagName is required for 'set' action", ErrorCodes.INVALID_PARAM);

            var go = TagLayerHelpers.FindGameObject(p.InstanceId, p.GameObjectName);
            if (go == null)
                return ToolResult<TagLayerTagResult>.Fail(
                    "GameObject not found. Provide a valid InstanceId or GameObjectName",
                    ErrorCodes.NOT_FOUND);

            // Verify tag exists
            bool tagExists = false;
            string[] existingTags = InternalEditorUtility.tags;
            foreach (string tag in existingTags)
            {
                if (string.Equals(tag, p.TagName, StringComparison.Ordinal))
                {
                    tagExists = true;
                    break;
                }
            }

            if (!tagExists)
                return ToolResult<TagLayerTagResult>.Fail(
                    $"Tag '{p.TagName}' does not exist. Use action 'add' to create it first",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(go, "Mosaic: Set Tag");
            go.tag = p.TagName;

            return ToolResult<TagLayerTagResult>.Ok(new TagLayerTagResult
            {
                GameObjectName = go.name,
                AssignedTag = go.tag
            });
        }
    }
}
