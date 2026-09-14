using UnityEditor;
using UnityEditor.Events;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.UI
{
    public static class UIRemoveListenerTool
    {
        [MosaicTool("ui/remove_listener",
                    "Removes a persistent listener from a UnityEvent by index (as reported by ui/add_listener's " +
                    "own ListenerIndex).",
                    isReadOnly: false)]
        public static ToolResult<UIRemoveListenerResult> Execute(UIRemoveListenerParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<UIRemoveListenerResult>.Fail(
                    "Either InstanceId or GameObjectName is required", ErrorCodes.INVALID_PARAM);
            if (!p.Index.HasValue)
                return ToolResult<UIRemoveListenerResult>.Fail("Index is required", ErrorCodes.INVALID_PARAM);

            var go = UIToolHelpers.ResolveGameObject(p.InstanceId, p.GameObjectName);
            if (go == null)
                return ToolResult<UIRemoveListenerResult>.Fail(
                    $"GameObject not found (InstanceId={p.InstanceId}, GameObjectName='{p.GameObjectName}')", ErrorCodes.NOT_FOUND);
            var componentType = UIToolHelpers.ResolveComponentType(p.ComponentType);
            if (componentType == null)
                return ToolResult<UIRemoveListenerResult>.Fail($"Component type '{p.ComponentType}' not found", ErrorCodes.NOT_FOUND);
            var component = go.GetComponent(componentType);
            if (component == null)
                return ToolResult<UIRemoveListenerResult>.Fail(
                    $"Component '{p.ComponentType}' not found on '{go.name}'", ErrorCodes.NOT_FOUND);

            if (!UIEventListenerHelpers.TryResolveEvent(component, p.EventName, out var evt, out var eventError))
                return ToolResult<UIRemoveListenerResult>.Fail(eventError, ErrorCodes.NOT_FOUND);

            if (p.Index.Value < 0 || p.Index.Value >= evt.GetPersistentEventCount())
                return ToolResult<UIRemoveListenerResult>.Fail(
                    $"Index {p.Index.Value} is out of range — '{p.EventName}' has {evt.GetPersistentEventCount()} listener(s).",
                    ErrorCodes.INVALID_PARAM);

            Undo.RecordObject(component, "Mosaic: Remove Listener");
            UnityEventTools.RemovePersistentListener(evt, p.Index.Value);
            EditorUtility.SetDirty(component);

            return ToolResult<UIRemoveListenerResult>.Ok(new UIRemoveListenerResult
            {
                GameObjectName = go.name,
                EventName = p.EventName,
                RemovedIndex = p.Index.Value,
                RemainingListenerCount = evt.GetPersistentEventCount(),
            });
        }
    }
}
