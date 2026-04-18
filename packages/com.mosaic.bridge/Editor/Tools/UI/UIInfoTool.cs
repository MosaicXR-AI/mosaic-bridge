using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.UI
{
    public static class UIInfoTool
    {
        [MosaicTool("ui/info",
                    "Queries UI hierarchy. Returns Canvas tree with component types and key properties.",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<UIInfoResult> Execute(UIInfoParams p)
        {
            var canvasInfos = new List<UICanvasInfo>();

            // If a specific target is requested, return just that subtree
            if (p.InstanceId.HasValue || !string.IsNullOrEmpty(p.Name))
            {
                var go = UIToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
                if (go == null)
                    return ToolResult<UIInfoResult>.Fail(
                        $"GameObject not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                        ErrorCodes.NOT_FOUND);

                var canvas = go.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvasInfos.Add(BuildCanvasInfo(canvas));
                }
                else
                {
                    // Target is a UI element, find its parent Canvas
                    var parentCanvas = go.GetComponentInParent<Canvas>();
                    if (parentCanvas != null)
                    {
                        canvasInfos.Add(BuildCanvasInfo(parentCanvas));
                    }
                    else
                    {
                        return ToolResult<UIInfoResult>.Fail(
                            $"'{go.name}' is not a Canvas and is not under a Canvas hierarchy",
                            ErrorCodes.NOT_FOUND);
                    }
                }
            }
            else
            {
                // Return all canvases in the scene
#if UNITY_2023_1_OR_NEWER
                var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#else
                var allCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
#endif
                foreach (var canvas in allCanvases)
                {
                    // Only root canvases (not nested canvases)
                    if (canvas.transform.parent == null ||
                        canvas.transform.parent.GetComponentInParent<Canvas>() == null)
                    {
                        canvasInfos.Add(BuildCanvasInfo(canvas));
                    }
                }
            }

            return ToolResult<UIInfoResult>.Ok(new UIInfoResult
            {
                Canvases = canvasInfos.ToArray()
            });
        }

        private static UICanvasInfo BuildCanvasInfo(Canvas canvas)
        {
            var children = new List<UIElementInfo>();
            CollectChildren(canvas.transform, children);

            string renderModeLabel;
            switch (canvas.renderMode)
            {
                case RenderMode.ScreenSpaceCamera:
                    renderModeLabel = "Camera";
                    break;
                case RenderMode.WorldSpace:
                    renderModeLabel = "WorldSpace";
                    break;
                default:
                    renderModeLabel = "Overlay";
                    break;
            }

            return new UICanvasInfo
            {
                InstanceId = canvas.gameObject.GetInstanceID(),
                Name       = canvas.gameObject.name,
                RenderMode = renderModeLabel,
                Children   = children.ToArray()
            };
        }

        private static void CollectChildren(Transform parent, List<UIElementInfo> list)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var rect = child.GetComponent<RectTransform>();

                var rawComponents = child.GetComponents<Component>();
                var componentNames = new List<string>(rawComponents.Length);
                foreach (var c in rawComponents)
                {
                    if (c != null)
                        componentNames.Add(c.GetType().Name);
                }

                var info = new UIElementInfo
                {
                    InstanceId       = child.gameObject.GetInstanceID(),
                    Name             = child.name,
                    HierarchyPath    = UIToolHelpers.GetHierarchyPath(child),
                    Components       = componentNames.ToArray(),
                    AnchoredPosition = rect != null
                        ? new[] { rect.anchoredPosition.x, rect.anchoredPosition.y }
                        : null,
                    SizeDelta = rect != null
                        ? new[] { rect.sizeDelta.x, rect.sizeDelta.y }
                        : null,
                    ChildCount = child.childCount
                };

                list.Add(info);

                // Recurse into children
                if (child.childCount > 0)
                    CollectChildren(child, list);
            }
        }
    }
}
