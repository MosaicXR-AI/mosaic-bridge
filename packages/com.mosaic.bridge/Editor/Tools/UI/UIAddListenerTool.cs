using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.UI
{
    public static class UIAddListenerTool
    {
        // O4 §4.6: "the course wired GameHUD 'via SerializedObject' in a hand-written editor
        // script" — a persistent listener (the Inspector "+"-button kind, serialized into the
        // scene/prefab) needs UnityEditor.Events.UnityEventTools, not UnityEvent.AddListener
        // (which is runtime-only and never survives a save). Generic over any UnityEvent-typed
        // property/field and any public 0-or-1-arg method — not hardcoded to Button.onClick.
        [MosaicTool("ui/add_listener",
                    "Adds a PERSISTENT listener (the kind that shows in the Inspector and is saved with the " +
                    "scene/prefab, unlike UnityEvent.AddListener) to a UnityEvent. EventName is the event's " +
                    "property/field name (e.g. 'onClick' on Button, 'onValueChanged' on Toggle/Slider). The " +
                    "target method must be public with 0 or 1 parameters (float/int/bool/string/Object) — pass " +
                    "the matching *Arg. CallState defaults to RuntimeOnly.",
                    isReadOnly: false)]
        public static ToolResult<UIAddListenerResult> Execute(UIAddListenerParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<UIAddListenerResult>.Fail(
                    "Either InstanceId or GameObjectName is required", ErrorCodes.INVALID_PARAM);
            if (p.TargetInstanceId == null && string.IsNullOrEmpty(p.TargetGameObjectName))
                return ToolResult<UIAddListenerResult>.Fail(
                    "Either TargetInstanceId or TargetGameObjectName is required", ErrorCodes.INVALID_PARAM);

            var go = UIToolHelpers.ResolveGameObject(p.InstanceId, p.GameObjectName);
            if (go == null)
                return ToolResult<UIAddListenerResult>.Fail(
                    $"GameObject not found (InstanceId={p.InstanceId}, GameObjectName='{p.GameObjectName}')", ErrorCodes.NOT_FOUND);
            var componentType = UIToolHelpers.ResolveComponentType(p.ComponentType);
            if (componentType == null)
                return ToolResult<UIAddListenerResult>.Fail($"Component type '{p.ComponentType}' not found", ErrorCodes.NOT_FOUND);
            var component = go.GetComponent(componentType);
            if (component == null)
                return ToolResult<UIAddListenerResult>.Fail(
                    $"Component '{p.ComponentType}' not found on '{go.name}'", ErrorCodes.NOT_FOUND);

            var targetGo = UIToolHelpers.ResolveGameObject(p.TargetInstanceId, p.TargetGameObjectName);
            if (targetGo == null)
                return ToolResult<UIAddListenerResult>.Fail(
                    $"Target GameObject not found (TargetInstanceId={p.TargetInstanceId}, TargetGameObjectName='{p.TargetGameObjectName}')",
                    ErrorCodes.NOT_FOUND);
            var targetComponentType = UIToolHelpers.ResolveComponentType(p.TargetComponentType);
            if (targetComponentType == null)
                return ToolResult<UIAddListenerResult>.Fail($"Component type '{p.TargetComponentType}' not found", ErrorCodes.NOT_FOUND);
            var targetComponent = targetGo.GetComponent(targetComponentType);
            if (targetComponent == null)
                return ToolResult<UIAddListenerResult>.Fail(
                    $"Component '{p.TargetComponentType}' not found on '{targetGo.name}'", ErrorCodes.NOT_FOUND);

            if (!UIEventListenerHelpers.TryResolveEvent(component, p.EventName, out var evt, out var eventError))
                return ToolResult<UIAddListenerResult>.Fail(eventError, ErrorCodes.NOT_FOUND);

            if (!UIEventListenerHelpers.TryResolveMethod(targetComponent, p.MethodName, out var method, out var argType, out var methodError))
                return ToolResult<UIAddListenerResult>.Fail(methodError, ErrorCodes.NOT_FOUND);

            if (!UIEventListenerHelpers.TryParseCallState(p.CallState, out var callState))
                return ToolResult<UIAddListenerResult>.Fail(
                    $"Invalid CallState '{p.CallState}'. Valid: RuntimeOnly, EditorAndRuntime, Off", ErrorCodes.INVALID_PARAM);

            Undo.RecordObject(component, "Mosaic: Add Listener");
            if (!UIEventListenerHelpers.TryAddPersistentListener(
                    evt, targetComponent, method, argType,
                    p.FloatArg, p.IntArg, p.BoolArg, p.StringArg, p.ObjectArg,
                    callState, out var index, out var addError))
                return ToolResult<UIAddListenerResult>.Fail(addError, ErrorCodes.INVALID_PARAM);

            EditorUtility.SetDirty(component);

            return ToolResult<UIAddListenerResult>.Ok(new UIAddListenerResult
            {
                GameObjectName = go.name,
                EventName = p.EventName,
                TargetGameObjectName = targetGo.name,
                MethodName = p.MethodName,
                ListenerIndex = index,
                CallState = callState.ToString(),
            });
        }
    }
}
