using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.EventSystems;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.UI
{
    public static class UICreateCanvasTool
    {
        [MosaicTool("ui/create_canvas",
                    "Creates a new Canvas with CanvasScaler and GraphicRaycaster. Auto-creates EventSystem if " +
                    "none exists, using InputModuleComponentFactory.AddInputModule by default (InputModule=" +
                    "'auto') — this picks StandaloneInputModule or InputSystemUIInputModule based on what's " +
                    "actually installed, since a hardcoded StandaloneInputModule silently does nothing in an " +
                    "Input System project (O4 L6). RenderMode: ScreenSpaceOverlay | ScreenSpaceCamera | " +
                    "WorldSpace (Unity-doc canonical names). Legacy aliases Overlay/Camera/WorldSpace remain " +
                    "accepted. ScaleMode/ReferenceResolution/MatchWidthOrHeight configure the CanvasScaler; " +
                    "SortingOrder/PixelPerfect/PlaneDistance/WorldCamera/WorldSize configure the Canvas itself.",
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

            if (!TryParseScaleMode(p.ScaleMode, out var scaleMode))
                return ToolResult<UICreateCanvasResult>.Fail(
                    $"Invalid ScaleMode '{p.ScaleMode}'. Valid: ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize",
                    ErrorCodes.INVALID_PARAM);
            if (!string.IsNullOrEmpty(p.InputModule) &&
                p.InputModule.ToLowerInvariant() != "auto" && p.InputModule.ToLowerInvariant() != "standalone" &&
                p.InputModule.ToLowerInvariant() != "inputsystem")
                return ToolResult<UICreateCanvasResult>.Fail(
                    $"Invalid InputModule '{p.InputModule}'. Valid: auto, standalone, inputsystem", ErrorCodes.INVALID_PARAM);

            Camera worldCamera = null;
            if (!string.IsNullOrEmpty(p.WorldCamera))
            {
                var camGo = GameObject.Find(p.WorldCamera);
                if (camGo == null)
                    return ToolResult<UICreateCanvasResult>.Fail($"Camera GameObject '{p.WorldCamera}' not found", ErrorCodes.NOT_FOUND);
                worldCamera = camGo.GetComponent<Camera>();
                if (worldCamera == null)
                    return ToolResult<UICreateCanvasResult>.Fail($"GameObject '{p.WorldCamera}' has no Camera component", ErrorCodes.INVALID_PARAM);
            }

            // 2. Create Canvas GameObject
            string canvasName = string.IsNullOrEmpty(p.Name) ? "Canvas" : p.Name;
            var canvasGo = new GameObject(canvasName);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = renderMode;
            if (p.SortingOrder.HasValue) canvas.sortingOrder = p.SortingOrder.Value;
            if (p.PixelPerfect.HasValue) canvas.pixelPerfect = p.PixelPerfect.Value;
            if (p.PlaneDistance.HasValue) canvas.planeDistance = p.PlaneDistance.Value;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = scaleMode;
            if (p.ReferenceResolution != null)
            {
                if (p.ReferenceResolution.Length != 2)
                    return ToolResult<UICreateCanvasResult>.Fail("ReferenceResolution requires exactly [width, height]", ErrorCodes.INVALID_PARAM);
                scaler.referenceResolution = new Vector2(p.ReferenceResolution[0], p.ReferenceResolution[1]);
            }
            if (p.MatchWidthOrHeight.HasValue) scaler.matchWidthOrHeight = p.MatchWidthOrHeight.Value;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Camera/WorldSpace canvases need a camera; explicit WorldCamera wins, otherwise fall
            // back to Camera.main.
            if (renderMode != UnityEngine.RenderMode.ScreenSpaceOverlay)
                canvas.worldCamera = worldCamera != null ? worldCamera : Camera.main;

            if (renderMode == UnityEngine.RenderMode.WorldSpace && p.WorldSize != null)
            {
                if (p.WorldSize.Length != 2)
                    return ToolResult<UICreateCanvasResult>.Fail("WorldSize requires exactly [width, height]", ErrorCodes.INVALID_PARAM);
                canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(p.WorldSize[0], p.WorldSize[1]);
            }

            if (!string.IsNullOrEmpty(p.Parent))
            {
                var parentGo = GameObject.Find(p.Parent);
                if (parentGo == null)
                    return ToolResult<UICreateCanvasResult>.Fail($"Parent GameObject '{p.Parent}' not found", ErrorCodes.NOT_FOUND);
                canvasGo.transform.SetParent(parentGo.transform, worldPositionStays: false);
            }

            // 3. Ensure EventSystem exists
            bool eventSystemCreated = false;
            string inputModuleType = null;
            var existingEventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existingEventSystem == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<EventSystem>();

                var inputModuleChoice = string.IsNullOrEmpty(p.InputModule) ? "auto" : p.InputModule.ToLowerInvariant();
                if (inputModuleChoice == "standalone")
                {
                    eventSystemGo.AddComponent<StandaloneInputModule>();
                    inputModuleType = nameof(StandaloneInputModule);
                }
                else if (inputModuleChoice == "inputsystem")
                {
                    if (!TryAddInputSystemUIInputModule(eventSystemGo, out inputModuleType, out var inputSystemError))
                    {
                        UnityEngine.Object.DestroyImmediate(eventSystemGo);
                        return ToolResult<UICreateCanvasResult>.Fail(inputSystemError, ErrorCodes.INVALID_PARAM);
                    }
                }
                else
                {
                    // O4 L6: the public factory, not a hardcoded StandaloneInputModule — it picks
                    // InputSystemUIInputModule automatically when the Input System package has
                    // registered its own override, and falls back to StandaloneInputModule otherwise.
                    var added = InputModuleComponentFactory.AddInputModule(eventSystemGo);
                    inputModuleType = added != null ? added.GetType().Name : null;
                }

                Undo.RegisterCreatedObjectUndo(eventSystemGo, "Mosaic: Create EventSystem");
                eventSystemCreated = true;
            }

            // 4. Register undo after full setup
            Undo.RegisterCreatedObjectUndo(canvasGo, "Mosaic: Create Canvas");

            return ToolResult<UICreateCanvasResult>.Ok(new UICreateCanvasResult
            {
                InstanceId         = UnityIds.Of(canvasGo),
                Name               = canvasGo.name,
                HierarchyPath      = UIToolHelpers.GetHierarchyPath(canvasGo.transform),
                RenderMode         = renderModeLabel,
                EventSystemCreated = eventSystemCreated,
                InputModuleType    = inputModuleType,
            });
        }

        private static bool TryParseScaleMode(string value, out CanvasScaler.ScaleMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "constantpixelsize":     result = CanvasScaler.ScaleMode.ConstantPixelSize;     return true;
                case "scalewithscreensize":   result = CanvasScaler.ScaleMode.ScaleWithScreenSize;   return true;
                case "constantphysicalsize":  result = CanvasScaler.ScaleMode.ConstantPhysicalSize;  return true;
                default:                      result = CanvasScaler.ScaleMode.ConstantPixelSize;     return false;
            }
        }

        /// <summary>MOSAIC_HAS_INPUT_SYSTEM-gated so this compiles and works whether or not the
        /// project has the Input System package — fails with a clear message rather than a stack
        /// trace when forced but absent.</summary>
        private static bool TryAddInputSystemUIInputModule(GameObject go, out string typeName, out string error)
        {
#if MOSAIC_HAS_INPUT_SYSTEM
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
            typeName = nameof(UnityEngine.InputSystem.UI.InputSystemUIInputModule);
            error = null;
            return true;
#else
            typeName = null;
            error = "InputModule='inputsystem' requires the com.unity.inputsystem package, which is not installed in this project.";
            return false;
#endif
        }
    }
}
