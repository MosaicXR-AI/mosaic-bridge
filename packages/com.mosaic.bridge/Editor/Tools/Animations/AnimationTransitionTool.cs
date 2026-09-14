using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Animations
{
    public static class AnimationTransitionTool
    {
        private const string ValidActions = "add, remove, set-conditions";

        [MosaicTool("animation/transition",
                    "Manages animator state transitions: add, remove, set conditions",
                    isReadOnly: false)]
        public static ToolResult<AnimationTransitionResult> Execute(AnimationTransitionParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "add":            return Add(p);
                case "remove":         return Remove(p);
                case "set-conditions": return SetConditions(p);
                default:
                    return ToolResult<AnimationTransitionResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: {ValidActions}",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<AnimationTransitionResult> Add(AnimationTransitionParams p)
        {
            if (string.IsNullOrEmpty(p.SourceStateName))
                return ToolResult<AnimationTransitionResult>.Fail(
                    "SourceStateName is required for 'add' action", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.DestinationStateName))
                return ToolResult<AnimationTransitionResult>.Fail(
                    "DestinationStateName is required for 'add' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);

            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var sourceState = AnimationToolHelpers.FindState(controller, p.SourceStateName, p.LayerIndex);
            if (sourceState == null)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"Source state '{p.SourceStateName}' not found in layer {p.LayerIndex}",
                    ErrorCodes.NOT_FOUND);

            var destState = AnimationToolHelpers.FindState(controller, p.DestinationStateName, p.LayerIndex);
            if (destState == null)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"Destination state '{p.DestinationStateName}' not found in layer {p.LayerIndex}",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(sourceState, "Mosaic: Add Animator Transition");
            var transition = sourceState.AddTransition(destState);
            transition.hasExitTime = p.HasExitTime;
            transition.duration = p.TransitionDuration;

            // Conditions are optional here (a transition can legitimately have none), but if
            // supplied they must actually be applied -- this call used to read HasExitTime and
            // TransitionDuration and stop there, so a caller who supplied `conditions` on 'add'
            // got success:true and ConditionCount:0 with no error at all, indistinguishable from
            // conditions that were never asked for. An invalid mode inside that array went
            // unvalidated for the same reason: nothing ever looked at it.
            if (p.Conditions != null && p.Conditions.Length > 0)
            {
                var conditionError = ApplyConditions(transition, p.Conditions);
                if (conditionError != null) return conditionError;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationTransitionResult>.Ok(new AnimationTransitionResult
            {
                Action               = "add",
                ControllerPath       = p.ControllerPath,
                SourceStateName      = p.SourceStateName,
                DestinationStateName = p.DestinationStateName,
                LayerIndex           = p.LayerIndex,
                HasExitTime          = transition.hasExitTime,
                TransitionDuration   = transition.duration,
                ConditionCount       = transition.conditions.Length
            });
        }

        private static ToolResult<AnimationTransitionResult> Remove(AnimationTransitionParams p)
        {
            if (string.IsNullOrEmpty(p.SourceStateName))
                return ToolResult<AnimationTransitionResult>.Fail(
                    "SourceStateName is required for 'remove' action", ErrorCodes.INVALID_PARAM);

            if (!p.TransitionIndex.HasValue)
                return ToolResult<AnimationTransitionResult>.Fail(
                    "TransitionIndex is required for 'remove' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);

            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var sourceState = AnimationToolHelpers.FindState(controller, p.SourceStateName, p.LayerIndex);
            if (sourceState == null)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"Source state '{p.SourceStateName}' not found in layer {p.LayerIndex}",
                    ErrorCodes.NOT_FOUND);

            int idx = p.TransitionIndex.Value;
            if (idx < 0 || idx >= sourceState.transitions.Length)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"TransitionIndex {idx} is out of range (0..{sourceState.transitions.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var transitionToRemove = sourceState.transitions[idx];
            string destName = transitionToRemove.destinationState != null
                ? transitionToRemove.destinationState.name : null;

            Undo.RecordObject(sourceState, "Mosaic: Remove Animator Transition");
            sourceState.RemoveTransition(transitionToRemove);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationTransitionResult>.Ok(new AnimationTransitionResult
            {
                Action               = "remove",
                ControllerPath       = p.ControllerPath,
                SourceStateName      = p.SourceStateName,
                DestinationStateName = destName,
                LayerIndex           = p.LayerIndex
            });
        }

        private static ToolResult<AnimationTransitionResult> SetConditions(AnimationTransitionParams p)
        {
            if (string.IsNullOrEmpty(p.SourceStateName))
                return ToolResult<AnimationTransitionResult>.Fail(
                    "SourceStateName is required for 'set-conditions' action", ErrorCodes.INVALID_PARAM);

            if (!p.TransitionIndex.HasValue)
                return ToolResult<AnimationTransitionResult>.Fail(
                    "TransitionIndex is required for 'set-conditions' action (index of the transition on the source state)",
                    ErrorCodes.INVALID_PARAM);

            if (p.Conditions == null || p.Conditions.Length == 0)
                return ToolResult<AnimationTransitionResult>.Fail(
                    "Conditions array is required and must not be empty for 'set-conditions' action",
                    ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);

            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var sourceState = AnimationToolHelpers.FindState(controller, p.SourceStateName, p.LayerIndex);
            if (sourceState == null)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"Source state '{p.SourceStateName}' not found in layer {p.LayerIndex}",
                    ErrorCodes.NOT_FOUND);

            int idx = p.TransitionIndex.Value;
            if (idx < 0 || idx >= sourceState.transitions.Length)
                return ToolResult<AnimationTransitionResult>.Fail(
                    $"TransitionIndex {idx} is out of range (0..{sourceState.transitions.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var transition = sourceState.transitions[idx];

            Undo.RecordObject(transition, "Mosaic: Set Transition Conditions");

            // Clear existing conditions by rebuilding
            // AnimatorStateTransition doesn't have a ClearConditions; we remove all then add
            while (transition.conditions.Length > 0)
                transition.RemoveCondition(transition.conditions[0]);

            var conditionError = ApplyConditions(transition, p.Conditions);
            if (conditionError != null) return conditionError;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string destName = transition.destinationState != null
                ? transition.destinationState.name : null;

            return ToolResult<AnimationTransitionResult>.Ok(new AnimationTransitionResult
            {
                Action               = "set-conditions",
                ControllerPath       = p.ControllerPath,
                SourceStateName      = p.SourceStateName,
                DestinationStateName = destName,
                LayerIndex           = p.LayerIndex,
                HasExitTime          = transition.hasExitTime,
                TransitionDuration   = transition.duration,
                ConditionCount       = transition.conditions.Length
            });
        }

        /// <summary>
        /// Adds each of <paramref name="conditions"/> to <paramref name="transition"/>. Returns a
        /// Fail result on the first unrecognised mode (so the caller learns which entry is wrong
        /// rather than getting a result that looks correct and is not), or null once every
        /// condition applied cleanly. Shared by 'add' (conditions optional, applied at creation)
        /// and 'set-conditions' (conditions required, replacing whatever the transition already had).
        /// </summary>
        private static ToolResult<AnimationTransitionResult> ApplyConditions(
            UnityEditor.Animations.AnimatorStateTransition transition, TransitionConditionInput[] conditions)
        {
            foreach (var c in conditions)
            {
                UnityEditor.Animations.AnimatorConditionMode mode;
                switch (c.Mode?.ToLowerInvariant())
                {
                    case "if":        mode = UnityEditor.Animations.AnimatorConditionMode.If;        break;
                    case "ifnot":     mode = UnityEditor.Animations.AnimatorConditionMode.IfNot;     break;
                    case "greater":   mode = UnityEditor.Animations.AnimatorConditionMode.Greater;   break;
                    case "less":      mode = UnityEditor.Animations.AnimatorConditionMode.Less;      break;
                    case "equals":    mode = UnityEditor.Animations.AnimatorConditionMode.Equals;    break;
                    case "notequal":  mode = UnityEditor.Animations.AnimatorConditionMode.NotEqual;  break;
                    default:
                        return ToolResult<AnimationTransitionResult>.Fail(
                            $"Unknown condition mode '{c.Mode}'. Valid modes: If, IfNot, Greater, Less, Equals, NotEqual",
                            ErrorCodes.INVALID_PARAM);
                }

                transition.AddCondition(mode, c.Threshold, c.ParameterName);
            }
            return null;
        }
    }
}
