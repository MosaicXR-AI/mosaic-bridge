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
        private const string ValidActions = "create, info, set-children, set-thresholds, add-child-tree";

        [MosaicTool("animation/blend-tree",
                    "Manages blend trees: create in a state, inspect, set child motions, set automatic-threshold " +
                    "spread, add a nested blend tree as a child. set-children's Children[] entries take " +
                    "DirectBlendParameter (each child blends independently by its own parameter — Direct blend " +
                    "type only) and Mirror (Humanoid rigs).",
                    isReadOnly: false)]
        public static ToolResult<AnimationBlendTreeResult> Execute(AnimationBlendTreeParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "create":         return Create(p);
                case "info":           return Info(p);
                case "set-children":   return SetChildren(p);
                case "set-thresholds": return SetThresholds(p);
                case "add-child-tree": return AddChildTree(p);
                default:
                    return ToolResult<AnimationBlendTreeResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: {ValidActions}",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        /// <summary>Shared by Create/Info/SetChildren/SetThresholds/AddChildTree: loads the
        /// controller, validates LayerIndex, finds the state, and returns its BlendTree motion
        /// (null with an error if the state has none — required for every action but 'create').</summary>
        private static bool TryResolveBlendTree(AnimationBlendTreeParams p, bool requireExisting,
            out AnimatorController controller, out BlendTree blendTree, out string error)
        {
            controller = null; blendTree = null; error = null;
            if (string.IsNullOrEmpty(p.StateName)) { error = "StateName is required"; return false; }

            controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null) { error = $"AnimatorController not found at '{p.ControllerPath}'"; return false; }
            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
            {
                error = $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})";
                return false;
            }

            var state = AnimationToolHelpers.FindState(controller, p.StateName, p.LayerIndex);
            if (state == null) { error = $"State '{p.StateName}' not found in layer {p.LayerIndex}"; return false; }

            blendTree = state.motion as BlendTree;
            if (blendTree == null && requireExisting)
            {
                error = $"State '{p.StateName}' does not have a BlendTree as its motion. Create one first.";
                return false;
            }
            return true;
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
                ClipPath  = c.motion != null && !(c.motion is BlendTree) ? AssetDatabase.GetAssetPath(c.motion) : null,
                Threshold = c.threshold,
                PositionX = c.position.x,
                PositionY = c.position.y,
                TimeScale = c.timeScale,
                DirectBlendParameter = c.directBlendParameter,
                Mirror = c.mirror,
                IsNestedBlendTree = c.motion is BlendTree,
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
                Children        = children,
                UseAutomaticThresholds = blendTree.useAutomaticThresholds,
                MinThreshold           = blendTree.minThreshold,
                MaxThreshold           = blendTree.maxThreshold,
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

                // Update position, time scale, and the O4 P2 additions (direct-blend parameter,
                // mirror) on the last added child.
                var childArray = blendTree.children;
                if (childArray.Length > 0)
                {
                    var lastChild = childArray[childArray.Length - 1];
                    lastChild.position = new Vector2(child.PositionX, child.PositionY);
                    lastChild.timeScale = child.TimeScale;
                    lastChild.directBlendParameter = child.DirectBlendParameter;
                    lastChild.mirror = child.Mirror;
                    childArray[childArray.Length - 1] = lastChild;
                    blendTree.children = childArray;
                }
            }

            // blendTree is a sub-asset with its own serialized data (added via AddObjectToAsset
            // in Create) — dirtying only the controller does not reliably flush a mutation made
            // directly to the sub-asset's own fields.
            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            var resultChildren = blendTree.children.Select(c => new BlendTreeChildInfo
            {
                ClipName  = c.motion != null ? c.motion.name : null,
                ClipPath  = c.motion != null && !(c.motion is BlendTree) ? AssetDatabase.GetAssetPath(c.motion) : null,
                Threshold = c.threshold,
                PositionX = c.position.x,
                PositionY = c.position.y,
                TimeScale = c.timeScale,
                DirectBlendParameter = c.directBlendParameter,
                Mirror = c.mirror,
                IsNestedBlendTree = c.motion is BlendTree,
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

        private static ToolResult<AnimationBlendTreeResult> SetThresholds(AnimationBlendTreeParams p)
        {
            if (!TryResolveBlendTree(p, requireExisting: true, out var controller, out var blendTree, out var error))
                return ToolResult<AnimationBlendTreeResult>.Fail(error, ErrorCodes.NOT_FOUND);

            Undo.RecordObject(blendTree, "Mosaic: Set BlendTree Thresholds");
            if (p.UseAutomaticThresholds.HasValue) blendTree.useAutomaticThresholds = p.UseAutomaticThresholds.Value;
            if (p.MinThreshold.HasValue) blendTree.minThreshold = p.MinThreshold.Value;
            if (p.MaxThreshold.HasValue) blendTree.maxThreshold = p.MaxThreshold.Value;
            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationBlendTreeResult>.Ok(new AnimationBlendTreeResult
            {
                Action = "set-thresholds",
                ControllerPath = p.ControllerPath,
                StateName = p.StateName,
                LayerIndex = p.LayerIndex,
                UseAutomaticThresholds = blendTree.useAutomaticThresholds,
                MinThreshold = blendTree.minThreshold,
                MaxThreshold = blendTree.maxThreshold,
            });
        }

        private static ToolResult<AnimationBlendTreeResult> AddChildTree(AnimationBlendTreeParams p)
        {
            if (!TryResolveBlendTree(p, requireExisting: true, out var controller, out var blendTree, out var error))
                return ToolResult<AnimationBlendTreeResult>.Fail(error, ErrorCodes.NOT_FOUND);

            var childBlendType = ResolveBlendType(p.ChildBlendType);
            if (!childBlendType.HasValue)
                return ToolResult<AnimationBlendTreeResult>.Fail(
                    $"Unknown ChildBlendType '{p.ChildBlendType}'. Valid: Simple1D, SimpleDirectional2D, " +
                    "FreeformDirectional2D, FreeformCartesian2D, Direct", ErrorCodes.INVALID_PARAM);

            Undo.RecordObject(blendTree, "Mosaic: Add Nested BlendTree");
            // CreateBlendTreeChild is overloaded on threshold (1D) vs position (2D) — the parent's
            // own blend type decides which placement actually matters, so pass both; whichever the
            // parent ignores is simply unused.
            var isParent1D = blendTree.blendType == BlendTreeType.Simple1D || blendTree.blendType == BlendTreeType.Direct;
            var child = isParent1D
                ? blendTree.CreateBlendTreeChild(p.Threshold)
                : blendTree.CreateBlendTreeChild(new Vector2(p.PositionX, p.PositionY));

            child.name = p.StateName + " Nested BlendTree";
            // Set the nested tree's OWN blend type/parameters before touching the parent's
            // ChildMotion entry for it — assigning blendType on the child can itself trigger
            // Unity's internal threshold/position bookkeeping on the parent, so anything the
            // parent-side patch below is meant to guarantee must run AFTER these, not before.
            child.blendType = childBlendType.Value;
            if (!string.IsNullOrEmpty(p.ChildBlendParameter)) child.blendParameter = p.ChildBlendParameter;
            if (!string.IsNullOrEmpty(p.ChildBlendParameterY)) child.blendParameterY = p.ChildBlendParameterY;

            // ChildMotion is a struct — CreateBlendTreeChild's own threshold/position parameter
            // did not stick without also reading the whole children array back, patching the
            // just-added entry, and reassigning the whole array, same as AddChild needs above.
            var childArray = blendTree.children;
            if (childArray.Length > 0)
            {
                var lastChild = childArray[childArray.Length - 1];
                if (isParent1D)
                {
                    lastChild.threshold = p.Threshold;
                    // A freshly-created BlendTree defaults useAutomaticThresholds to true, which
                    // silently recomputes every child's threshold from an even 0..1 spread —
                    // exactly the "explicit value we just set gets thrown away" symptom a real
                    // test caught (a lone child's auto-spread threshold is 0 regardless of what
                    // was requested). An explicit Threshold means the caller wants manual control.
                    blendTree.useAutomaticThresholds = false;
                }
                else
                {
                    lastChild.position = new Vector2(p.PositionX, p.PositionY);
                }
                childArray[childArray.Length - 1] = lastChild;
                blendTree.children = childArray;
            }

            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationBlendTreeResult>.Ok(new AnimationBlendTreeResult
            {
                Action = "add-child-tree",
                ControllerPath = p.ControllerPath,
                StateName = p.StateName,
                LayerIndex = p.LayerIndex,
                ChildTreeName = child.name,
                ChildCount = blendTree.children.Length,
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
