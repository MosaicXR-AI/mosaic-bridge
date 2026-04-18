using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Animations
{
    public static class AnimationStateTool
    {
        private const string ValidActions = "add, remove, set-motion, info";

        [MosaicTool("animation/state",
                    "Manages animator states: add, remove, set motion clip, inspect state info",
                    isReadOnly: false)]
        public static ToolResult<AnimationStateResult> Execute(AnimationStateParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "add":        return Add(p);
                case "remove":     return Remove(p);
                case "set-motion": return SetMotion(p);
                case "info":       return Info(p);
                default:
                    return ToolResult<AnimationStateResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: {ValidActions}",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<AnimationStateResult> Add(AnimationStateParams p)
        {
            if (string.IsNullOrEmpty(p.StateName))
                return ToolResult<AnimationStateResult>.Fail(
                    "StateName is required for 'add' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return ToolResult<AnimationStateResult>.Fail(
                    $"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);

            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return ToolResult<AnimationStateResult>.Fail(
                    $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var stateMachine = controller.layers[p.LayerIndex].stateMachine;

            Undo.RecordObject(stateMachine, "Mosaic: Add Animator State");
            var state = stateMachine.AddState(p.StateName);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationStateResult>.Ok(new AnimationStateResult
            {
                Action         = "add",
                ControllerPath = p.ControllerPath,
                StateName      = state.name,
                LayerIndex     = p.LayerIndex,
                PositionX      = 0f,
                PositionY      = 0f
            });
        }

        private static ToolResult<AnimationStateResult> Remove(AnimationStateParams p)
        {
            if (string.IsNullOrEmpty(p.StateName))
                return ToolResult<AnimationStateResult>.Fail(
                    "StateName is required for 'remove' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return ToolResult<AnimationStateResult>.Fail(
                    $"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);

            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return ToolResult<AnimationStateResult>.Fail(
                    $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var stateMachine = controller.layers[p.LayerIndex].stateMachine;
            var state = AnimationToolHelpers.FindStateInMachine(stateMachine, p.StateName);
            if (state == null)
                return ToolResult<AnimationStateResult>.Fail(
                    $"State '{p.StateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(stateMachine, "Mosaic: Remove Animator State");
            stateMachine.RemoveState(state);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationStateResult>.Ok(new AnimationStateResult
            {
                Action         = "remove",
                ControllerPath = p.ControllerPath,
                StateName      = p.StateName,
                LayerIndex     = p.LayerIndex
            });
        }

        private static ToolResult<AnimationStateResult> SetMotion(AnimationStateParams p)
        {
            if (string.IsNullOrEmpty(p.StateName))
                return ToolResult<AnimationStateResult>.Fail(
                    "StateName is required for 'set-motion' action", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.ClipPath))
                return ToolResult<AnimationStateResult>.Fail(
                    "ClipPath is required for 'set-motion' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return ToolResult<AnimationStateResult>.Fail(
                    $"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);

            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return ToolResult<AnimationStateResult>.Fail(
                    $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var state = AnimationToolHelpers.FindState(controller, p.StateName, p.LayerIndex);
            if (state == null)
                return ToolResult<AnimationStateResult>.Fail(
                    $"State '{p.StateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);

            var clip = AnimationToolHelpers.LoadClip(p.ClipPath);
            if (clip == null)
                return ToolResult<AnimationStateResult>.Fail(
                    $"AnimationClip not found at '{p.ClipPath}'", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(state, "Mosaic: Set State Motion");
            state.motion = clip;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationStateResult>.Ok(new AnimationStateResult
            {
                Action         = "set-motion",
                ControllerPath = p.ControllerPath,
                StateName      = p.StateName,
                LayerIndex     = p.LayerIndex,
                MotionName     = clip.name,
                MotionPath     = p.ClipPath
            });
        }

        private static ToolResult<AnimationStateResult> Info(AnimationStateParams p)
        {
            if (string.IsNullOrEmpty(p.StateName))
                return ToolResult<AnimationStateResult>.Fail(
                    "StateName is required for 'info' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return ToolResult<AnimationStateResult>.Fail(
                    $"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);

            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return ToolResult<AnimationStateResult>.Fail(
                    $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var state = AnimationToolHelpers.FindState(controller, p.StateName, p.LayerIndex);
            if (state == null)
                return ToolResult<AnimationStateResult>.Fail(
                    $"State '{p.StateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);

            var defaultState = controller.layers[p.LayerIndex].stateMachine.defaultState;
            string motionPath = null;
            if (state.motion != null)
                motionPath = AssetDatabase.GetAssetPath(state.motion);

            return ToolResult<AnimationStateResult>.Ok(new AnimationStateResult
            {
                Action          = "info",
                ControllerPath  = p.ControllerPath,
                StateName       = state.name,
                LayerIndex      = p.LayerIndex,
                MotionName      = state.motion != null ? state.motion.name : null,
                MotionPath      = motionPath,
                Speed           = state.speed,
                Tag             = state.tag,
                TransitionCount = state.transitions.Length,
                IsDefault       = state == defaultState
            });
        }
    }
}
