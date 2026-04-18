using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.UI
{
    public static class UISetRectTransformTool
    {
        [MosaicTool("ui/set_rect_transform",
                    "Sets RectTransform properties (anchors, pivot, size, position) on a UI element",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<UISetRectTransformResult> Execute(UISetRectTransformParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.Name))
                return ToolResult<UISetRectTransformResult>.Fail(
                    "Either InstanceId or Name is required", ErrorCodes.INVALID_PARAM);

            var go = UIToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<UISetRectTransformResult>.Fail(
                    $"GameObject not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                    ErrorCodes.NOT_FOUND);

            var rect = go.GetComponent<RectTransform>();
            if (rect == null)
                return ToolResult<UISetRectTransformResult>.Fail(
                    $"'{go.name}' does not have a RectTransform component",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(rect, "Mosaic: Set RectTransform");

            if (p.AnchorMin != null && p.AnchorMin.Length >= 2)
                rect.anchorMin = new Vector2(p.AnchorMin[0], p.AnchorMin[1]);

            if (p.AnchorMax != null && p.AnchorMax.Length >= 2)
                rect.anchorMax = new Vector2(p.AnchorMax[0], p.AnchorMax[1]);

            if (p.Pivot != null && p.Pivot.Length >= 2)
                rect.pivot = new Vector2(p.Pivot[0], p.Pivot[1]);

            if (p.SizeDelta != null && p.SizeDelta.Length >= 2)
                rect.sizeDelta = new Vector2(p.SizeDelta[0], p.SizeDelta[1]);

            if (p.AnchoredPosition != null && p.AnchoredPosition.Length >= 2)
                rect.anchoredPosition = new Vector2(p.AnchoredPosition[0], p.AnchoredPosition[1]);

            EditorUtility.SetDirty(rect);

            return ToolResult<UISetRectTransformResult>.Ok(new UISetRectTransformResult
            {
                InstanceId       = go.GetInstanceID(),
                Name             = go.name,
                AnchorMin        = new[] { rect.anchorMin.x, rect.anchorMin.y },
                AnchorMax        = new[] { rect.anchorMax.x, rect.anchorMax.y },
                Pivot            = new[] { rect.pivot.x, rect.pivot.y },
                SizeDelta        = new[] { rect.sizeDelta.x, rect.sizeDelta.y },
                AnchoredPosition = new[] { rect.anchoredPosition.x, rect.anchoredPosition.y }
            });
        }
    }
}
