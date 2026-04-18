using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Animations
{
    public static class AnimationBlendTreeTool
    {
        private const string ValidActions = "create, info, set-children";

        [MosaicTool("animation/blend-tree",
                    "Manages blend trees: create in a state, inspect, set child motions",
                    isReadOnly: false)]
        public static ToolResult<AnimationBlendTreeResult> Execute(AnimationBlendTreeParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "create":       return Create(p);
                case "info":         return Info(p);
                case "set-children": return SetChildren(p);
                default:
                    return ToolResult<AnimationBlendTreeResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: {ValidActions}",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<AnimationBlendTreeResult> Create(AnimationBlendTreeParams p)
        {
            if (string.IsNullOrEmpty(p.StateName))
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    "StateName is required for 'create' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);

            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var state = AnimationToolHelpers.FindState(controller, p.StateName, p.LayerIndex);
            if (state == null)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"State '{p.StateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);

            var blendType = ResolveBlendType(p.BlendType);
            if (!blendType.HasValue)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"Unknown blend type '{p.BlendType}'. Valid: Simple1D, SimpleDirectional2D, FreeformDirectional2D, FreeformCartesian2D, Direct",
                    ErrorCodes.INVALID_PARAM);

            var blendTree = new BlendTree();
            blendTree.blendType = blendType.Value;

            if (!string.IsNullOrEmpty(p.BlendParameter))
                blendTree.blendParameter = p.BlendParameter;

            if (!string.IsNullOrEmpty(p.BlendParameterY))
                blendTree.blendParameterY = p.BlendParameterY;

            blendTree.name = p.StateName + " BlendTree";

            // Add the blend tree as a sub-asset of the controller
            AssetDatabase.AddObjectToAsset(blendTree, controller);

            Undo.RecordObject(state, "Mosaic: Create BlendTree");
            state.motion = blendTree;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationBlendTreeResult>.Ok(new AnimationBlendTreeResult
            {
                Action          = "create",
                ControllerPath  = p.ControllerPath,
                StateName       = p.StateName,
                LayerIndex      = p.LayerIndex,
                BlendType       = blendType.Value.ToString(),
                BlendParameter  = blendTree.blendParameter,
                BlendParameterY = blendTree.blendParameterY,
                ChildCount      = 0
            });
        }

        private static ToolResult<AnimationBlendTreeResult> Info(AnimationBlendTreeParams p)
        {
            if (string.IsNullOrEmpty(p.StateName))
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    "StateName is required for 'info' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);

            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var state = AnimationToolHelpers.FindState(controller, p.StateName, p.LayerIndex);
            if (state == null)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"State '{p.StateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);

            var blendTree = state.motion as BlendTree;
            if (blendTree == null)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"State '{p.StateName}' does not have a BlendTree as its motion",
                    ErrorCodes.NOT_FOUND);

            var children = blendTree.children.Select(c => new BlendTreeChildInfo
            {
                ClipName  = c.motion != null ? c.motion.name : null,
                ClipPath  = c.motion != null ? AssetDatabase.GetAssetPath(c.motion) : null,
                Threshold = c.threshold,
                PositionX = c.position.x,
                PositionY = c.position.y,
                TimeScale = c.timeScale
            }).ToArray();

            return ToolResult<AnimationBlendTreeResult>.Ok(new AnimationBlendTreeResult
            {
                Action          = "info",
                ControllerPath  = p.ControllerPath,
                StateName       = p.StateName,
                LayerIndex      = p.LayerIndex,
                BlendType       = blendTree.blendType.ToString(),
                BlendParameter  = blendTree.blendParameter,
                BlendParameterY = blendTree.blendParameterY,
                ChildCount      = children.Length,
                Children        = children
            });
        }

        private static ToolResult<AnimationBlendTreeResult> SetChildren(AnimationBlendTreeParams p)
        {
            if (string.IsNullOrEmpty(p.StateName))
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    "StateName is required for 'set-children' action", ErrorCodes.INVALID_PARAM);

            if (p.Children == null || p.Children.Length == 0)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    "Children array is required and must not be empty for 'set-children' action",
                    ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);

            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var state = AnimationToolHelpers.FindState(controller, p.StateName, p.LayerIndex);
            if (state == null)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"State '{p.StateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);

            var blendTree = state.motion as BlendTree;
            if (blendTree == null)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"State '{p.StateName}' does not have a BlendTree as its motion. Create one first.",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(blendTree, "Mosaic: Set BlendTree Children");

            // Clear existing children by setting to empty array
            blendTree.children = new ChildMotion[0];

            // Add each child
            foreach (var child in p.Children)
            {
                AnimationClip clip = null;
                if (!string.IsNullOrEmpty(child.ClipPath))
                {
                    clip = AnimationToolHelpers.LoadClip(child.ClipPath);
                    if (clip == null)
                        return ToolResult<AnimationBlendTreeResult>.Fail(
                            $"AnimationClip not found at '{child.ClipPath}'", ErrorCodes.NOT_FOUND);
                }

                blendTree.AddChild(clip, child.Threshold);

                // Update position and time scale on the last added child
                var childArray = blendTree.children;
                if (childArray.Length > 0)
                {
                    var lastChild = childArray[childArray.Length - 1];
                    lastChild.position = new Vector2(child.PositionX, child.PositionY);
                    lastChild.timeScale = child.TimeScale;
                    childArray[childArray.Length - 1] = lastChild;
                    blendTree.children = childArray;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            var resultChildren = blendTree.children.Select(c => new BlendTreeChildInfo
            {
                ClipName  = c.motion != null ? c.motion.name : null,
                ClipPath  = c.motion != null ? AssetDatabase.GetAssetPath(c.motion) : null,
                Threshold = c.threshold,
                PositionX = c.position.x,
                PositionY = c.position.y,
                TimeScale = c.timeScale
            }).ToArray();

            return ToolResult<AnimationBlendTreeResult>.Ok(new AnimationBlendTreeResult
            {
                Action          = "set-children",
                ControllerPath  = p.ControllerPath,
                StateName       = p.StateName,
                LayerIndex      = p.LayerIndex,
                BlendType       = blendTree.blendType.ToString(),
                BlendParameter  = blendTree.blendParameter,
                BlendParameterY = blendTree.blendParameterY,
                ChildCount      = resultChildren.Length,
                Children        = resultChildren
            });
        }

        private static BlendTreeType? ResolveBlendType(string blendType)
        {
            if (string.IsNullOrEmpty(blendType))
                return BlendTreeType.Simple1D;

            switch (blendType.ToLowerInvariant())
            {
                case "simple1d":                return BlendTreeType.Simple1D;
                case "simpledirectional2d":     return BlendTreeType.SimpleDirectional2D;
                case "freeformdirectional2d":   return BlendTreeType.FreeformDirectional2D;
                case "freeformcartesian2d":     return BlendTreeType.FreeformCartesian2D;
                case "direct":                  return BlendTreeType.Direct;
                default:                        return null;
            }
        }
    }
}
