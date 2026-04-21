using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.UI
{
    public static class UICreateCanvasTool
    {
        [MosaicTool("ui/create_canvas",
                    "Creates a new Canvas with CanvasScaler and GraphicRaycaster. Auto-creates EventSystem if none exists. RenderMode: ScreenSpaceOverlay | ScreenSpaceCamera | WorldSpace (Unity-doc canonical names). Legacy aliases Overlay/Camera/WorldSpace remain accepted.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<UICreateCanvasResult> Execute(UICreateCanvasParams p)
        {
            // 1. Parse render mode
            RenderMode renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            string renderModeLabel = "ScreenSpaceOverlay";

            if (!string.IsNullOrEmpty(p.RenderMode))
            {
                // Accept both canonical Unity-doc names (ScreenSpaceOverlay,
                // ScreenSpaceCamera, WorldSpace) AND the short aliases
                // (Overlay, Camera, WorldSpace) that were the beta.1-6 surface.
                // Keeps existing callers working while aligning with the Unity
                // Manual's enum vocabulary.
                switch (p.RenderMode.ToLowerInvariant())
                {
                    case "screenspaceoverlay":
                    case "overlay":
                        renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
                        renderModeLabel = "ScreenSpaceOverlay";
                        break;
                    case "screenspacecamera":
                    case "camera":
                        renderMode = UnityEngine.RenderMode.ScreenSpaceCamera;
                        renderModeLabel = "ScreenSpaceCamera";
                        break;
                    case "worldspace":
                        renderMode = UnityEngine.RenderMode.WorldSpace;
                        renderModeLabel = "WorldSpace";
                        break;
                    default:
                        return ToolResult<UICreateCanvasResult>.Fail(
                            $"Invalid RenderMode '{p.RenderMode}'. Must be ScreenSpaceOverlay, ScreenSpaceCamera, or WorldSpace.",
                            ErrorCodes.INVALID_PARAM);
                }
            }

            // 2. Create Canvas GameObject
            string canvasName = string.IsNullOrEmpty(p.Name) ? "Canvas" : p.Name;
            var canvasGo = new GameObject(canvasName);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = renderMode;

            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // 3. Ensure EventSystem exists
            bool eventSystemCreated = false;
#if UNITY_2023_1_OR_NEWER
            var existingEventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
#else
            var existingEventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
            if (existingEventSystem == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<EventSystem>();
                eventSystemGo.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(eventSystemGo, "Mosaic: Create EventSystem");
                eventSystemCreated = true;
            }

            // 4. Register undo after full setup
            Undo.RegisterCreatedObjectUndo(canvasGo, "Mosaic: Create Canvas");

            return ToolResult<UICreateCanvasResult>.Ok(new UICreateCanvasResult
            {
                InstanceId         = canvasGo.GetInstanceID(),
                Name               = canvasGo.name,
                HierarchyPath      = UIToolHelpers.GetHierarchyPath(canvasGo.transform),
                RenderMode         = renderModeLabel,
                EventSystemCreated = eventSystemCreated
            });
        }
    }
}
