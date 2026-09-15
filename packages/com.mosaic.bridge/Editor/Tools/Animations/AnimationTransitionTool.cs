using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Animations
{
    public static class AnimationTransitionTool
    {
        private const string ValidActions = "add, remove, set-conditions, set-settings, info";
        private const string ValidSourceKinds = "state, anyState, entry";

        [MosaicTool("animation/transition",
                    "Manages animator state transitions: add, remove, set conditions/settings, inspect. " +
                    "SourceKind (add / remove / set-conditions / set-settings / info): 'state' (default, a " +
                    "named state's own transitions), 'anyState' (AnimatorStateMachine.AddAnyStateTransition — " +
                    "fires from any state in the layer; hit/death 'any state to stagger' pattern), or 'entry' " +
                    "(fires once when the layer becomes active — Unity's AnimatorTransition, not " +
                    "AnimatorStateTransition, so it has no ExitTime/Duration/interruption settings, only " +
                    "Conditions). add's DestinationKind='exit' creates a transition out of the state machine " +
                    "itself via AnimatorState.AddExitTransition (only valid when SourceKind is 'state').",
                    isReadOnly: false)]
        public static ToolResult<AnimationTransitionResult> Execute(AnimationTransitionParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "add":            return Add(p);
                case "remove":         return Remove(p);
                case "set-conditions": return SetConditions(p);
                case "set-settings":   return SetSettings(p);
                case "info":           return Info(p);
                default:
                    return Fail($"Unknown action '{p.Action}'. Valid actions: {ValidActions}");
            }
        }

        private static ToolResult<AnimationTransitionResult> Fail(string message, string code = ErrorCodes.INVALID_PARAM) =>
            ToolResult<AnimationTransitionResult>.Fail(message, code);

        private static string NormalizeKind(string raw) => string.IsNullOrEmpty(raw) ? "state" : raw.ToLowerInvariant();

        /// <summary>Maps the lowercase-normalized internal key back to the documented casing
        /// ("anystate" -> "anyState") for anything echoed back in a result.</summary>
        private static string CanonicalKind(string normalized) => normalized == "anystate" ? "anyState" : normalized;

        // ── Add ──────────────────────────────────────────────────────────────

        private static ToolResult<AnimationTransitionResult> Add(AnimationTransitionParams p)
        {
            var sourceKind = NormalizeKind(p.SourceKind);
            if (sourceKind != "state" && sourceKind != "anystate" && sourceKind != "entry")
                return Fail($"Unknown SourceKind '{p.SourceKind}'. Valid: {ValidSourceKinds}");

            var destinationKind = string.IsNullOrEmpty(p.DestinationKind) ? "state" : p.DestinationKind.ToLowerInvariant();
            if (destinationKind != "state" && destinationKind != "exit")
                return Fail($"Unknown DestinationKind '{p.DestinationKind}'. Valid: state, exit");
            if (destinationKind == "exit" && sourceKind != "state")
                return Fail("DestinationKind 'exit' is only valid when SourceKind is 'state' " +
                             "(a normal state's own AddExitTransition).");

            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return Fail($"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);
            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return Fail($"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})", ErrorCodes.OUT_OF_RANGE);
            var stateMachine = controller.layers[p.LayerIndex].stateMachine;

            AnimatorState destState = null;
            if (destinationKind == "state")
            {
                if (string.IsNullOrEmpty(p.DestinationStateName))
                    return Fail("DestinationStateName is required when DestinationKind is 'state'.");
                destState = AnimationToolHelpers.FindState(controller, p.DestinationStateName, p.LayerIndex);
                if (destState == null)
                    return Fail($"Destination state '{p.DestinationStateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);
            }

            AnimatorTransitionBase transition;
            string sourceStateName = null;

            if (sourceKind == "state")
            {
                if (string.IsNullOrEmpty(p.SourceStateName))
                    return Fail("SourceStateName is required for SourceKind 'state'.");
                var sourceState = AnimationToolHelpers.FindState(controller, p.SourceStateName, p.LayerIndex);
                if (sourceState == null)
                    return Fail($"Source state '{p.SourceStateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);
                sourceStateName = sourceState.name;
                Undo.RecordObject(sourceState, "Mosaic: Add Animator Transition");
                transition = destinationKind == "exit" ? sourceState.AddExitTransition() : sourceState.AddTransition(destState);
            }
            else if (sourceKind == "anystate")
            {
                Undo.RecordObject(stateMachine, "Mosaic: Add AnyState Transition");
                transition = stateMachine.AddAnyStateTransition(destState);
            }
            else // entry
            {
                Undo.RecordObject(stateMachine, "Mosaic: Add Entry Transition");
                transition = stateMachine.AddEntryTransition(destState);
            }

            ApplySettings(transition, p);

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

            return ToolResult<AnimationTransitionResult>.Ok(ToResult("add", p.ControllerPath, p.LayerIndex,
                sourceKind, sourceStateName, transition));
        }

        // ── Remove ───────────────────────────────────────────────────────────

        private static ToolResult<AnimationTransitionResult> Remove(AnimationTransitionParams p)
        {
            if (!p.TransitionIndex.HasValue)
                return Fail("TransitionIndex is required for 'remove' action");

            if (!TryResolveOwner(p, out var controller, out var stateMachine, out var sourceState,
                    out var sourceKind, out var error))
                return Fail(error, ErrorCodes.NOT_FOUND);

            var transitions = TransitionsFor(sourceKind, sourceState, stateMachine);
            int idx = p.TransitionIndex.Value;
            if (idx < 0 || idx >= transitions.Length)
                return Fail($"TransitionIndex {idx} is out of range (0..{transitions.Length - 1})", ErrorCodes.OUT_OF_RANGE);

            var transition = transitions[idx];
            string destName = transition.destinationState != null ? transition.destinationState.name : null;

            if (sourceKind == "state")
            {
                Undo.RecordObject(sourceState, "Mosaic: Remove Animator Transition");
                sourceState.RemoveTransition((AnimatorStateTransition)transition);
            }
            else if (sourceKind == "anystate")
            {
                Undo.RecordObject(stateMachine, "Mosaic: Remove AnyState Transition");
                stateMachine.RemoveAnyStateTransition((AnimatorStateTransition)transition);
            }
            else
            {
                Undo.RecordObject(stateMachine, "Mosaic: Remove Entry Transition");
                stateMachine.RemoveEntryTransition((AnimatorTransition)transition);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationTransitionResult>.Ok(new AnimationTransitionResult
            {
                Action = "remove",
                ControllerPath = p.ControllerPath,
                SourceStateName = sourceState?.name,
                DestinationStateName = destName,
                LayerIndex = p.LayerIndex,
                SourceKind = CanonicalKind(sourceKind),
            });
        }

        // ── Set conditions ───────────────────────────────────────────────────

        private static ToolResult<AnimationTransitionResult> SetConditions(AnimationTransitionParams p)
        {
            if (!p.TransitionIndex.HasValue)
                return Fail("TransitionIndex is required for 'set-conditions' action");
            if (p.Conditions == null || p.Conditions.Length == 0)
                return Fail("Conditions array is required and must not be empty for 'set-conditions' action");

            if (!TryResolveOwner(p, out var controller, out var stateMachine, out var sourceState,
                    out var sourceKind, out var error))
                return Fail(error, ErrorCodes.NOT_FOUND);

            var transitions = TransitionsFor(sourceKind, sourceState, stateMachine);
            int idx = p.TransitionIndex.Value;
            if (idx < 0 || idx >= transitions.Length)
                return Fail($"TransitionIndex {idx} is out of range (0..{transitions.Length - 1})", ErrorCodes.OUT_OF_RANGE);

            var transition = transitions[idx];
            Undo.RecordObject(transition, "Mosaic: Set Transition Conditions");

            // AnimatorTransitionBase has no ClearConditions; remove all then add.
            while (transition.conditions.Length > 0)
                transition.RemoveCondition(transition.conditions[0]);

            var conditionError = ApplyConditions(transition, p.Conditions);
            if (conditionError != null) return conditionError;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationTransitionResult>.Ok(ToResult("set-conditions", p.ControllerPath, p.LayerIndex,
                sourceKind, sourceState?.name, transition));
        }

        // ── Set settings ─────────────────────────────────────────────────────

        private static ToolResult<AnimationTransitionResult> SetSettings(AnimationTransitionParams p)
        {
            if (!p.TransitionIndex.HasValue)
                return Fail("TransitionIndex is required for 'set-settings' action");

            if (!TryResolveOwner(p, out var controller, out var stateMachine, out var sourceState,
                    out var sourceKind, out var error))
                return Fail(error, ErrorCodes.NOT_FOUND);

            var transitions = TransitionsFor(sourceKind, sourceState, stateMachine);
            int idx = p.TransitionIndex.Value;
            if (idx < 0 || idx >= transitions.Length)
                return Fail($"TransitionIndex {idx} is out of range (0..{transitions.Length - 1})", ErrorCodes.OUT_OF_RANGE);

            var transition = transitions[idx];
            if (sourceKind == "entry" && (p.ExitTime.HasValue || p.HasFixedDuration.HasValue || p.Offset.HasValue ||
                    !string.IsNullOrEmpty(p.InterruptionSource) || p.OrderedInterruption.HasValue || p.CanTransitionToSelf.HasValue))
                return Fail("An entry transition (Unity's AnimatorTransition) has no ExitTime/Duration/" +
                            "interruption settings — only Conditions and Mute/Solo apply.");

            Undo.RecordObject(transition, "Mosaic: Set Transition Settings");
            if (!ApplySettingsPartial(transition, p, out var settingsError))
                return Fail(settingsError);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationTransitionResult>.Ok(ToResult("set-settings", p.ControllerPath, p.LayerIndex,
                sourceKind, sourceState?.name, transition));
        }

        // ── Info ─────────────────────────────────────────────────────────────

        private static ToolResult<AnimationTransitionResult> Info(AnimationTransitionParams p)
        {
            var controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
                return Fail($"AnimatorController not found at '{p.ControllerPath}'", ErrorCodes.NOT_FOUND);
            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
                return Fail($"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})", ErrorCodes.OUT_OF_RANGE);
            var stateMachine = controller.layers[p.LayerIndex].stateMachine;

            AnimatorState sourceState = null;
            var requestedKind = string.IsNullOrEmpty(p.SourceKind) ? null : NormalizeKind(p.SourceKind);
            if (requestedKind == null || requestedKind == "state")
            {
                if (!string.IsNullOrEmpty(p.SourceStateName))
                {
                    sourceState = AnimationToolHelpers.FindState(controller, p.SourceStateName, p.LayerIndex);
                    if (sourceState == null)
                        return Fail($"Source state '{p.SourceStateName}' not found in layer {p.LayerIndex}", ErrorCodes.NOT_FOUND);
                }
            }

            var infos = new System.Collections.Generic.List<AnimationTransitionInfo>();
            void AddInfos(string kind, AnimatorTransitionBase[] arr)
            {
                for (int i = 0; i < arr.Length; i++)
                    infos.Add(ToInfo(kind, i, arr[i]));
            }

            if (requestedKind == null || requestedKind == "state")
            {
                if (sourceState != null)
                    AddInfos("state", sourceState.transitions.Cast<AnimatorTransitionBase>().ToArray());
            }
            if (requestedKind == null || requestedKind == "anystate")
                AddInfos("anyState", stateMachine.anyStateTransitions.Cast<AnimatorTransitionBase>().ToArray());
            if (requestedKind == null || requestedKind == "entry")
                AddInfos("entry", stateMachine.entryTransitions.Cast<AnimatorTransitionBase>().ToArray());

            return ToolResult<AnimationTransitionResult>.Ok(new AnimationTransitionResult
            {
                Action = "info",
                ControllerPath = p.ControllerPath,
                SourceStateName = sourceState?.name,
                LayerIndex = p.LayerIndex,
                Transitions = infos.ToArray(),
            });
        }

        // ── Shared resolution ────────────────────────────────────────────────

        private static bool TryResolveOwner(AnimationTransitionParams p, out AnimatorController controller,
            out AnimatorStateMachine stateMachine, out AnimatorState sourceState, out string sourceKind, out string error)
        {
            controller = null; stateMachine = null; sourceState = null; error = null;
            sourceKind = NormalizeKind(p.SourceKind);
            if (sourceKind != "state" && sourceKind != "anystate" && sourceKind != "entry")
            {
                error = $"Unknown SourceKind '{p.SourceKind}'. Valid: {ValidSourceKinds}";
                return false;
            }

            controller = AnimationToolHelpers.LoadController(p.ControllerPath);
            if (controller == null)
            {
                error = $"AnimatorController not found at '{p.ControllerPath}'";
                return false;
            }
            if (p.LayerIndex < 0 || p.LayerIndex >= controller.layers.Length)
            {
                error = $"LayerIndex {p.LayerIndex} is out of range (0..{controller.layers.Length - 1})";
                return false;
            }
            stateMachine = controller.layers[p.LayerIndex].stateMachine;

            if (sourceKind == "state")
            {
                if (string.IsNullOrEmpty(p.SourceStateName))
                {
                    error = "SourceStateName is required for SourceKind 'state'.";
                    return false;
                }
                sourceState = AnimationToolHelpers.FindState(controller, p.SourceStateName, p.LayerIndex);
                if (sourceState == null)
                {
                    error = $"Source state '{p.SourceStateName}' not found in layer {p.LayerIndex}";
                    return false;
                }
            }
            return true;
        }

        private static AnimatorTransitionBase[] TransitionsFor(string sourceKind, AnimatorState sourceState, AnimatorStateMachine stateMachine)
        {
            switch (sourceKind)
            {
                case "state":    return sourceState.transitions.Cast<AnimatorTransitionBase>().ToArray();
                case "anystate": return stateMachine.anyStateTransitions.Cast<AnimatorTransitionBase>().ToArray();
                default:         return stateMachine.entryTransitions.Cast<AnimatorTransitionBase>().ToArray();
            }
        }

        /// <summary>Applies every settings field unconditionally on 'add' (HasExitTime/TransitionDuration
        /// always; the rest only when explicitly provided). No-ops for a non-AnimatorStateTransition
        /// (an "entry" AnimatorTransition) beyond Mute/Solo, which are on the shared base class.</summary>
        private static void ApplySettings(AnimatorTransitionBase transition, AnimationTransitionParams p)
        {
            transition.mute = p.Mute ?? transition.mute;
            transition.solo = p.Solo ?? transition.solo;

            if (transition is AnimatorStateTransition st)
            {
                st.hasExitTime = p.HasExitTime;
                st.duration = p.TransitionDuration;
                if (p.ExitTime.HasValue) st.exitTime = p.ExitTime.Value;
                if (p.HasFixedDuration.HasValue) st.hasFixedDuration = p.HasFixedDuration.Value;
                if (p.Offset.HasValue) st.offset = p.Offset.Value;
                if (p.OrderedInterruption.HasValue) st.orderedInterruption = p.OrderedInterruption.Value;
                if (p.CanTransitionToSelf.HasValue) st.canTransitionToSelf = p.CanTransitionToSelf.Value;
                if (!string.IsNullOrEmpty(p.InterruptionSource) &&
                    System.Enum.TryParse<TransitionInterruptionSource>(p.InterruptionSource, true, out var src))
                    st.interruptionSource = src;
            }
        }

        /// <summary>set-settings: only touches fields the caller actually supplied (no HasExitTime/
        /// TransitionDuration defaults to fall back on, unlike 'add'). Returns false with an error
        /// message if InterruptionSource is provided but not a valid enum name.</summary>
        private static bool ApplySettingsPartial(AnimatorTransitionBase transition, AnimationTransitionParams p, out string error)
        {
            error = null;
            if (p.Mute.HasValue) transition.mute = p.Mute.Value;
            if (p.Solo.HasValue) transition.solo = p.Solo.Value;

            if (transition is AnimatorStateTransition st)
            {
                if (p.ExitTime.HasValue) { st.hasExitTime = true; st.exitTime = p.ExitTime.Value; }
                if (p.HasFixedDuration.HasValue) st.hasFixedDuration = p.HasFixedDuration.Value;
                if (p.Duration.HasValue) st.duration = p.Duration.Value;
                if (p.Offset.HasValue) st.offset = p.Offset.Value;
                if (p.OrderedInterruption.HasValue) st.orderedInterruption = p.OrderedInterruption.Value;
                if (p.CanTransitionToSelf.HasValue) st.canTransitionToSelf = p.CanTransitionToSelf.Value;
                if (!string.IsNullOrEmpty(p.InterruptionSource))
                {
                    if (!System.Enum.TryParse<TransitionInterruptionSource>(p.InterruptionSource, true, out var src))
                    {
                        error = $"Unknown InterruptionSource '{p.InterruptionSource}'. Valid: None, Source, " +
                                "Destination, SourceThenDestination, DestinationThenSource";
                        return false;
                    }
                    st.interruptionSource = src;
                }
            }
            return true;
        }

        private static AnimationTransitionResult ToResult(string action, string controllerPath, int layerIndex,
            string sourceKind, string sourceStateName, AnimatorTransitionBase transition)
        {
            var result = new AnimationTransitionResult
            {
                Action = action,
                ControllerPath = controllerPath,
                SourceStateName = sourceStateName,
                DestinationStateName = transition.destinationState != null ? transition.destinationState.name : null,
                LayerIndex = layerIndex,
                SourceKind = CanonicalKind(sourceKind),
                IsExit = transition.isExit,
                Mute = transition.mute,
                Solo = transition.solo,
                ConditionCount = transition.conditions.Length,
            };
            if (transition is AnimatorStateTransition st)
            {
                result.HasExitTime = st.hasExitTime;
                result.TransitionDuration = st.duration;
                result.ExitTime = st.exitTime;
                result.HasFixedDuration = st.hasFixedDuration;
                result.Offset = st.offset;
                result.InterruptionSource = st.interruptionSource.ToString();
                result.OrderedInterruption = st.orderedInterruption;
                result.CanTransitionToSelf = st.canTransitionToSelf;
            }
            return result;
        }

        private static AnimationTransitionInfo ToInfo(string sourceKind, int index, AnimatorTransitionBase transition)
        {
            var info = new AnimationTransitionInfo
            {
                SourceKind = sourceKind,
                TransitionIndex = index,
                DestinationStateName = transition.destinationState != null ? transition.destinationState.name : null,
                IsExit = transition.isExit,
                Mute = transition.mute,
                Solo = transition.solo,
                ConditionCount = transition.conditions.Length,
            };
            if (transition is AnimatorStateTransition st)
            {
                info.HasExitTime = st.hasExitTime;
                info.ExitTime = st.exitTime;
                info.TransitionDuration = st.duration;
                info.HasFixedDuration = st.hasFixedDuration;
                info.Offset = st.offset;
                info.InterruptionSource = st.interruptionSource.ToString();
            }
            return info;
        }

        /// <summary>
        /// Adds each of <paramref name="conditions"/> to <paramref name="transition"/>. Returns a
        /// Fail result on the first unrecognised mode (so the caller learns which entry is wrong
        /// rather than getting a result that looks correct and is not), or null once every
        /// condition applied cleanly. AnimatorTransitionBase covers both AnimatorStateTransition
        /// (state/anyState/exit) and AnimatorTransition (entry) — AddCondition/RemoveCondition are
        /// declared on the shared base.
        /// </summary>
        private static ToolResult<AnimationTransitionResult> ApplyConditions(
            AnimatorTransitionBase transition, TransitionConditionInput[] conditions)
        {
            foreach (var c in conditions)
            {
                AnimatorConditionMode mode;
                switch (c.Mode?.ToLowerInvariant())
                {
                    case "if":        mode = AnimatorConditionMode.If;        break;
                    case "ifnot":     mode = AnimatorConditionMode.IfNot;     break;
                    case "greater":   mode = AnimatorConditionMode.Greater;   break;
                    case "less":      mode = AnimatorConditionMode.Less;      break;
                    case "equals":    mode = AnimatorConditionMode.Equals;    break;
                    case "notequal":  mode = AnimatorConditionMode.NotEqual;  break;
                    default:
                        return Fail($"Unknown condition mode '{c.Mode}'. Valid modes: If, IfNot, Greater, Less, Equals, NotEqual");
                }

                transition.AddCondition(mode, c.Threshold, c.ParameterName);
            }
            return null;
        }
    }
}
