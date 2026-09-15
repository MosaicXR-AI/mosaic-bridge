using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Animations
{
    public static class AnimationStateTool
    {
        private const string ValidActions = "add, remove, set-motion, info, add-sub-machine, set-default, set-settings";

        [MosaicTool("animation/state",
                    "Manages animator states: add, remove, set motion clip, inspect state info, add nested " +
                    "sub-state-machines, set the default state, and set state settings (speed/mirror/cycleOffset/" +
                    "IK/writeDefaultValues/tag). ParentStateMachinePath (e.g. 'Combat/Melee') places a new state/" +
                    "sub-machine inside a specific nested machine instead of always the layer root, where every " +
                    "generated state used to land — Animator captures of anything but a flat state machine were " +
                    "unreadable without this. set-motion: for a multi-clip FBX (several takes embedded in one " +
                    "imported file), ClipPath alone always resolves to the first embedded clip — pass ClipName " +
                    "to pick a specific take by its own name.",
                    isReadOnly: false)]
        public static ToolResult<AnimationStateResult> Execute(AnimationStateParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "add":             return Add(p);
                case "remove":          return Remove(p);
                case "set-motion":      return SetMotion(p);
                case "info":            return Info(p);
                case "add-sub-machine": return AddSubMachine(p);
                case "set-default":     return SetDefault(p);
                case "set-settings":    return SetSettings(p);
                default:
                    return Fail($"Unknown action '{p.Action}'. Valid actions: {ValidActions}");
            }
        }

        private static ToolResult<AnimationStateResult> Fail(string message, string code = ErrorCodes.INVALID_PARAM) =>
            ToolResult<AnimationStateResult>.Fail(message, code);

        private static Vector3 PositionOf(AnimationStateParams p) =>
            p.Position != null && p.Position.Length == 2 ? new Vector3(p.Position[0], p.Position[1], 0f) : Vector3.zero;

        /// <summary>Loads the controller, validates LayerIndex, and resolves ParentStateMachinePath
        /// (defaulting to the layer's own root) — the shared setup every action needs.</summary>
        private static bool TrySetup(AnimationStateParams p, out AnimatorController controller,
            out AnimatorStateMachine machine, out string error)
        {
            controller = null; machine = null; error = null;
            controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null) { error = $"AnimatorController not found at '{p.ControllerPath}'"; return false; }
            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
            {
                error = $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})";
                return false;
            }
            var root = controller.layers[p.LayerIndex].stateMachine;
            machine = AnimationToolHelpers.ResolveStateMachineByPath(root, p.ParentStateMachinePath, out error);
            return machine != null;
        }

        private static ToolResult<AnimationStateResult> Add(AnimationStateParams p)
        {
            if (string.IsNullOrEmpty(p.StateName))
                return Fail("StateName is required for 'add' action");
            if (!TrySetup(p, out var controller, out var machine, out var setupError))
                return Fail(setupError, ErrorCodes.NOT_FOUND);

            var position = PositionOf(p);
            Undo.RecordObject(machine, "Mosaic: Add Animator State");
            var state = machine.AddState(p.StateName, position);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationStateResult>.Ok(new AnimationStateResult
            {
                Action                 = "add",
                ControllerPath         = p.ControllerPath,
                StateName              = state.name,
                LayerIndex             = p.LayerIndex,
                ParentStateMachinePath = p.ParentStateMachinePath,
                PositionX              = position.x,
                PositionY              = position.y
            });
        }

        private static ToolResult<AnimationStateResult> Remove(AnimationStateParams p)
        {
            if (string.IsNullOrEmpty(p.StateName))
                return Fail("StateName is required for 'remove' action");

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return Fail($"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);
            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return Fail($"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})", ErrorCodes.OUT_OF_RANGE);

            var root = controller.layers[p.LayerIndex].stateMachine;
            var searchRoot = root;
            if (!string.IsNullOrEmpty(p.ParentStateMachinePath))
            {
                searchRoot = AnimationToolHelpers.ResolveStateMachineByPath(root, p.ParentStateMachinePath, out var pathError);
                if (searchRoot == null) return Fail(pathError, ErrorCodes.NOT_FOUND);
            }

            var state = AnimationToolHelpers.FindStateInMachine(searchRoot, p.StateName);
            if (state == null)
                return Fail($"State '{p.StateName}' not found in layer {p.LayerIndex}" +
                            (p.ParentStateMachinePath != null ? $" under '{p.ParentStateMachinePath}'" : ""), ErrorCodes.NOT_FOUND);

            var owner = AnimationToolHelpers.FindOwningMachine(searchRoot, state);
            Undo.RecordObject(owner, "Mosaic: Remove Animator State");
            owner.RemoveState(state);
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
                return Fail("StateName is required for 'set-motion' action");
            if (string.IsNullOrEmpty(p.ClipPath))
                return Fail("ClipPath is required for 'set-motion' action");

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return Fail($"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);
            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return Fail($"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})", ErrorCodes.OUT_OF_RANGE);

            var state = AnimationToolHelpers.FindState(controller, p.StateName, p.LayerIndex);
            if (state == null)
                return Fail($"State '{p.StateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);

            // L19: without ClipName, a multi-clip FBX (several takes embedded in one imported
            // file) always resolves to the FIRST embedded clip regardless of which take the
            // caller actually wants.
            var clip = AnimationToolHelpers.LoadClip(p.ClipPath, p.ClipName);
            if (clip == null)
            {
                if (!string.IsNullOrEmpty(p.ClipName))
                {
                    var names = AnimationToolHelpers.ListClipNames(p.ClipPath);
                    return Fail(names.Length == 0
                        ? $"No AnimationClip found at '{p.ClipPath}'."
                        : $"No clip named '{p.ClipName}' at '{p.ClipPath}'. Available: {string.Join(", ", names)}",
                        ErrorCodes.NOT_FOUND);
                }
                return Fail($"AnimationClip not found at '{p.ClipPath}'", ErrorCodes.NOT_FOUND);
            }

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
                return Fail("StateName is required for 'info' action");

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return Fail($"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);
            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return Fail($"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})", ErrorCodes.OUT_OF_RANGE);

            var state = AnimationToolHelpers.FindState(controller, p.StateName, p.LayerIndex);
            if (state == null)
                return Fail($"State '{p.StateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);

            var root = controller.layers[p.LayerIndex].stateMachine;
            var owner = AnimationToolHelpers.FindOwningMachine(root, state);
            var defaultState = owner != null ? owner.defaultState : root.defaultState;
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
                IsDefault       = state == defaultState,
                SpeedParameter             = state.speedParameter,
                SpeedParameterActive       = state.speedParameterActive,
                CycleOffset                = state.cycleOffset,
                CycleOffsetParameter       = state.cycleOffsetParameter,
                CycleOffsetParameterActive = state.cycleOffsetParameterActive,
                Mirror                     = state.mirror,
                MirrorParameter            = state.mirrorParameter,
                MirrorParameterActive      = state.mirrorParameterActive,
                IKOnFeet                   = state.iKOnFeet,
                WriteDefaultValues         = state.writeDefaultValues,
            });
        }

        private static ToolResult<AnimationStateResult> AddSubMachine(AnimationStateParams p)
        {
            if (string.IsNullOrEmpty(p.SubMachineName))
                return Fail("SubMachineName is required for 'add-sub-machine' action");
            if (!TrySetup(p, out var controller, out var machine, out var setupError))
                return Fail(setupError, ErrorCodes.NOT_FOUND);

            var position = PositionOf(p);
            Undo.RecordObject(machine, "Mosaic: Add Animator Sub-State-Machine");
            var sub = machine.AddStateMachine(p.SubMachineName, position);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationStateResult>.Ok(new AnimationStateResult
            {
                Action                 = "add-sub-machine",
                ControllerPath         = p.ControllerPath,
                LayerIndex             = p.LayerIndex,
                ParentStateMachinePath = p.ParentStateMachinePath,
                SubMachineName         = sub.name,
                PositionX              = position.x,
                PositionY              = position.y
            });
        }

        private static ToolResult<AnimationStateResult> SetDefault(AnimationStateParams p)
        {
            if (string.IsNullOrEmpty(p.StateName))
                return Fail("StateName is required for 'set-default' action");
            if (!TrySetup(p, out var controller, out var machine, out var setupError))
                return Fail(setupError, ErrorCodes.NOT_FOUND);

            // A state's default only makes sense within its OWN immediate parent machine — search
            // scoped to the resolved machine (which recurses into deeper sub-machines too), then
            // set the default on whichever machine actually owns it.
            var state = AnimationToolHelpers.FindStateInMachine(machine, p.StateName);
            if (state == null)
                return Fail($"State '{p.StateName}' not found" +
                            (p.ParentStateMachinePath != null ? $" under '{p.ParentStateMachinePath}'" : ""), ErrorCodes.NOT_FOUND);
            var owner = AnimationToolHelpers.FindOwningMachine(machine, state);

            Undo.RecordObject(owner, "Mosaic: Set Default Animator State");
            owner.defaultState = state;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationStateResult>.Ok(new AnimationStateResult
            {
                Action         = "set-default",
                ControllerPath = p.ControllerPath,
                StateName      = state.name,
                LayerIndex     = p.LayerIndex,
                IsDefault      = true,
            });
        }

        private static ToolResult<AnimationStateResult> SetSettings(AnimationStateParams p)
        {
            if (string.IsNullOrEmpty(p.StateName))
                return Fail("StateName is required for 'set-settings' action");

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return Fail($"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);
            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return Fail($"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})", ErrorCodes.OUT_OF_RANGE);

            var state = AnimationToolHelpers.FindState(controller, p.StateName, p.LayerIndex);
            if (state == null)
                return Fail($"State '{p.StateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(state, "Mosaic: Set Animator State Settings");
            if (p.Speed.HasValue) state.speed = p.Speed.Value;
            if (p.SpeedParameter != null) state.speedParameter = p.SpeedParameter;
            if (p.SpeedParameterActive.HasValue) state.speedParameterActive = p.SpeedParameterActive.Value;
            if (p.CycleOffset.HasValue) state.cycleOffset = p.CycleOffset.Value;
            if (p.CycleOffsetParameter != null) state.cycleOffsetParameter = p.CycleOffsetParameter;
            if (p.CycleOffsetParameterActive.HasValue) state.cycleOffsetParameterActive = p.CycleOffsetParameterActive.Value;
            if (p.Mirror.HasValue) state.mirror = p.Mirror.Value;
            if (p.MirrorParameter != null) state.mirrorParameter = p.MirrorParameter;
            if (p.MirrorParameterActive.HasValue) state.mirrorParameterActive = p.MirrorParameterActive.Value;
            if (p.IKOnFeet.HasValue) state.iKOnFeet = p.IKOnFeet.Value;
            if (p.WriteDefaultValues.HasValue) state.writeDefaultValues = p.WriteDefaultValues.Value;
            if (p.Tag != null) state.tag = p.Tag;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationStateResult>.Ok(new AnimationStateResult
            {
                Action         = "set-settings",
                ControllerPath = p.ControllerPath,
                StateName      = state.name,
                LayerIndex     = p.LayerIndex,
                Speed          = state.speed,
                Tag            = state.tag,
                SpeedParameter             = state.speedParameter,
                SpeedParameterActive       = state.speedParameterActive,
                CycleOffset                = state.cycleOffset,
                CycleOffsetParameter       = state.cycleOffsetParameter,
                CycleOffsetParameterActive = state.cycleOffsetParameterActive,
                Mirror                     = state.mirror,
                MirrorParameter            = state.mirrorParameter,
                MirrorParameterActive      = state.mirrorParameterActive,
                IKOnFeet                   = state.iKOnFeet,
                WriteDefaultValues         = state.writeDefaultValues,
            });
        }
    }
}
