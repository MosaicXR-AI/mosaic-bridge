#if MOSAIC_HAS_TMP
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using TMPro;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.TextMeshPro
{
    public static class TmpCreateTool
    {
        [MosaicTool("tmp/create",
                    "Creates a TextMeshPro text element — either UI (on Canvas) or world-space 3D text",
                    isReadOnly: false)]
        public static ToolResult<TmpCreateResult> Execute(TmpCreateParams p)
        {
            string context = string.IsNullOrEmpty(p.Context) ? "ui" : p.Context.ToLowerInvariant();
            if (context != "ui" && context != "world")
                return ToolResult<TmpCreateResult>.Fail(
                    $"Invalid Context '{p.Context}'. Must be 'ui' or 'world'.",
                    ErrorCodes.INVALID_PARAM);

            string goName = string.IsNullOrEmpty(p.Name) ? "TMP Text" : p.Name;

            // Resolve optional font asset
            TMP_FontAsset fontAsset = null;
            if (!string.IsNullOrEmpty(p.FontAsset))
            {
                fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p.FontAsset);
                if (fontAsset == null)
                    return ToolResult<TmpCreateResult>.Fail(
                        $"Font asset not found at '{p.FontAsset}'.",
                        ErrorCodes.NOT_FOUND);
            }

            // Resolve color
            Color color = UnityEngine.Color.white;
            if (p.Color != null && p.Color.Length == 4)
                color = new Color(p.Color[0], p.Color[1], p.Color[2], p.Color[3]);

            GameObject go;

            if (context == "ui")
                go = CreateUIText(goName, p, fontAsset, color);
            else
                go = CreateWorldText(goName, p, fontAsset, color);

            if (go == null)
                return ToolResult<TmpCreateResult>.Fail(
                    "Failed to create TMP text element.",
                    ErrorCodes.INTERNAL_ERROR);

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: TMP Create");

            return ToolResult<TmpCreateResult>.Ok(new TmpCreateResult
            {
                InstanceId   = go.GetInstanceID(),
                Name         = go.name,
                HierarchyPath = TmpToolHelpers.GetHierarchyPath(go.transform),
                ContextType  = context
            });
        }

        private static GameObject CreateUIText(string name, TmpCreateParams p, TMP_FontAsset fontAsset, Color color)
        {
            // Find or create parent Canvas
            Transform parent = null;
            if (!string.IsNullOrEmpty(p.Parent))
            {
                var parentGo = GameObject.Find(p.Parent);
                if (parentGo != null)
                    parent = parentGo.transform;
            }

            // Walk up to find a Canvas on the parent chain
            Canvas canvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;

            if (canvas == null)
            {
                // Find any Canvas in the scene, or create one
#if UNITY_2023_1_OR_NEWER
                canvas = Object.FindAnyObjectByType<Canvas>();
#else
                canvas = Object.FindObjectOfType<Canvas>();
#endif
                if (canvas == null)
                {
                    var canvasGo = new GameObject("Canvas");
                    canvas = canvasGo.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasGo.AddComponent<CanvasScaler>();
                    canvasGo.AddComponent<GraphicRaycaster>();
                    Undo.RegisterCreatedObjectUndo(canvasGo, "Mosaic: TMP Auto-Canvas");

                    // Ensure EventSystem
#if UNITY_2023_1_OR_NEWER
                    if (Object.FindAnyObjectByType<EventSystem>() == null)
#else
                    if (Object.FindObjectOfType<EventSystem>() == null)
#endif
                    {
                        var esGo = new GameObject("EventSystem");
                        esGo.AddComponent<EventSystem>();
                        esGo.AddComponent<StandaloneInputModule>();
                        Undo.RegisterCreatedObjectUndo(esGo, "Mosaic: TMP Auto-EventSystem");
                    }
                }

                if (parent == null)
                    parent = canvas.transform;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent ?? canvas.transform, worldPositionStays: false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = p.Text;
            tmp.fontSize = p.FontSize;
            tmp.color = color;
            if (fontAsset != null) tmp.font = fontAsset;

            return go;
        }

        private static GameObject CreateWorldText(string name, TmpCreateParams p, TMP_FontAsset fontAsset, Color color)
        {
            Transform parent = null;
            if (!string.IsNullOrEmpty(p.Parent))
            {
                var parentGo = GameObject.Find(p.Parent);
                if (parentGo != null)
                    parent = parentGo.transform;
            }

            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, worldPositionStays: true);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = p.Text;
            tmp.fontSize = p.FontSize;
            tmp.color = color;
            if (fontAsset != null) tmp.font = fontAsset;

            return go;
        }
    }
}
#endif
