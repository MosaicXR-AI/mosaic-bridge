using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.UI
{
    public static class UIAddElementTool
    {
        [MosaicTool("ui/add_element",
                    "Adds a UI element (button, text, image, slider, toggle, dropdown, input-field) as a child of a Canvas or UI parent",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<UIAddElementResult> Execute(UIAddElementParams p)
        {
            if (string.IsNullOrEmpty(p.ElementType))
                return ToolResult<UIAddElementResult>.Fail(
                    "ElementType is required", ErrorCodes.INVALID_PARAM);

            if (p.ParentInstanceId == null && string.IsNullOrEmpty(p.ParentName))
                return ToolResult<UIAddElementResult>.Fail(
                    "Either ParentInstanceId or ParentName is required", ErrorCodes.INVALID_PARAM);

            var parent = UIToolHelpers.ResolveGameObject(p.ParentInstanceId, p.ParentName);
            if (parent == null)
                return ToolResult<UIAddElementResult>.Fail(
                    $"Parent not found (InstanceId={p.ParentInstanceId}, Name='{p.ParentName}')",
                    ErrorCodes.NOT_FOUND);

            // Ensure the parent has a Canvas somewhere in its hierarchy
            if (parent.GetComponentInParent<Canvas>() == null && parent.GetComponent<Canvas>() == null)
                return ToolResult<UIAddElementResult>.Fail(
                    $"Parent '{parent.name}' is not under a Canvas hierarchy",
                    ErrorCodes.INVALID_PARAM);

            string elementType = p.ElementType.ToLowerInvariant().Replace("-", "").Replace("_", "");
            string defaultName;
            GameObject elementGo;

            switch (elementType)
            {
                case "button":
                    defaultName = "Button";
                    elementGo = CreateButton(parent.transform, p);
                    break;
                case "text":
                    defaultName = "Text";
                    elementGo = CreateText(parent.transform, p);
                    break;
                case "image":
                    defaultName = "Image";
                    elementGo = CreateImage(parent.transform, p);
                    break;
                case "slider":
                    defaultName = "Slider";
                    elementGo = CreateSlider(parent.transform, p);
                    break;
                case "toggle":
                    defaultName = "Toggle";
                    elementGo = CreateToggle(parent.transform, p);
                    break;
                case "dropdown":
                    defaultName = "Dropdown";
                    elementGo = CreateDropdown(parent.transform, p);
                    break;
                case "inputfield":
                    defaultName = "InputField";
                    elementGo = CreateInputField(parent.transform, p);
                    break;
                default:
                    return ToolResult<UIAddElementResult>.Fail(
                        $"Unknown ElementType '{p.ElementType}'. Supported: button, text, image, slider, toggle, dropdown, input-field.",
                        ErrorCodes.INVALID_PARAM);
            }

            // Apply name (use default if not provided)
            elementGo.name = !string.IsNullOrEmpty(p.Name) ? p.Name : defaultName;

            // Apply optional anchored position and size delta
            var rect = elementGo.GetComponent<RectTransform>();
            if (rect != null)
            {
                if (p.AnchoredPosition != null && p.AnchoredPosition.Length >= 2)
                    rect.anchoredPosition = new Vector2(p.AnchoredPosition[0], p.AnchoredPosition[1]);
                if (p.SizeDelta != null && p.SizeDelta.Length >= 2)
                    rect.sizeDelta = new Vector2(p.SizeDelta[0], p.SizeDelta[1]);
            }

            Undo.RegisterCreatedObjectUndo(elementGo, "Mosaic: Add UI Element");

            // Collect component names
            var rawComponents = elementGo.GetComponents<Component>();
            var componentNames = new List<string>(rawComponents.Length);
            foreach (var c in rawComponents)
            {
                if (c != null)
                    componentNames.Add(c.GetType().Name);
            }

            return ToolResult<UIAddElementResult>.Ok(new UIAddElementResult
            {
                InstanceId    = elementGo.GetInstanceID(),
                Name          = elementGo.name,
                HierarchyPath = UIToolHelpers.GetHierarchyPath(elementGo.transform),
                ElementType   = p.ElementType,
                Components    = componentNames.ToArray()
            });
        }

        // ── Element factories ────────────────────────────────────────────────

        private static GameObject CreateButton(Transform parent, UIAddElementParams p)
        {
            var go = CreateUIGameObject("Button", parent);
            var image = go.AddComponent<Image>();
            image.color = Color.white;
            go.AddComponent<Button>();

            // Button needs a child Text to be usable
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

#if UNITY_2023_1_OR_NEWER && HAS_TMPRO
            var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "Button";
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.black;
#else
            var text = textGo.AddComponent<Text>();
            text.text = "Button";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
#endif

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 30);
            return go;
        }

        private static GameObject CreateText(Transform parent, UIAddElementParams p)
        {
            var go = CreateUIGameObject("Text", parent);

#if UNITY_2023_1_OR_NEWER && HAS_TMPRO
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "New Text";
            tmp.color = Color.black;
#else
            var text = go.AddComponent<Text>();
            text.text = "New Text";
            text.color = Color.black;
#endif

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 30);
            return go;
        }

        private static GameObject CreateImage(Transform parent, UIAddElementParams p)
        {
            var go = CreateUIGameObject("Image", parent);
            go.AddComponent<Image>();
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100, 100);
            return go;
        }

        private static GameObject CreateSlider(Transform parent, UIAddElementParams p)
        {
            var go = CreateUIGameObject("Slider", parent);

            // Background
            var bgGo = CreateUIGameObject("Background", go.transform);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.sizeDelta = Vector2.zero;

            // Fill Area
            var fillAreaGo = CreateUIGameObject("Fill Area", go.transform);
            var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.sizeDelta = Vector2.zero;

            var fillGo = CreateUIGameObject("Fill", fillAreaGo.transform);
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = Color.white;
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.sizeDelta = Vector2.zero;

            // Handle Slide Area
            var handleAreaGo = CreateUIGameObject("Handle Slide Area", go.transform);
            var handleAreaRect = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = new Vector2(-20, 0);

            var handleGo = CreateUIGameObject("Handle", handleAreaGo.transform);
            var handleImage = handleGo.AddComponent<Image>();
            handleImage.color = Color.white;
            var handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 0);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 20);
            return go;
        }

        private static GameObject CreateToggle(Transform parent, UIAddElementParams p)
        {
            var go = CreateUIGameObject("Toggle", parent);

            // Background
            var bgGo = CreateUIGameObject("Background", go.transform);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = Color.white;
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 1);
            bgRect.anchorMax = new Vector2(0, 1);
            bgRect.pivot = new Vector2(0, 1);
            bgRect.sizeDelta = new Vector2(20, 20);
            bgRect.anchoredPosition = Vector2.zero;

            // Checkmark
            var checkGo = CreateUIGameObject("Checkmark", bgGo.transform);
            var checkImage = checkGo.AddComponent<Image>();
            checkImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var checkRect = checkGo.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.sizeDelta = Vector2.zero;

            // Label
            var labelGo = CreateUIGameObject("Label", go.transform);
#if UNITY_2023_1_OR_NEWER && HAS_TMPRO
            var tmp = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "Toggle";
            tmp.color = Color.black;
#else
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = "Toggle";
            labelText.color = Color.black;
#endif
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = new Vector2(-28, 0);
            labelRect.anchoredPosition = new Vector2(14, 0);

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            toggle.isOn = true;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 20);
            return go;
        }

        private static GameObject CreateDropdown(Transform parent, UIAddElementParams p)
        {
            var go = CreateUIGameObject("Dropdown", parent);
            var image = go.AddComponent<Image>();
            image.color = Color.white;

            // Label
            var labelGo = CreateUIGameObject("Label", go.transform);
#if UNITY_2023_1_OR_NEWER && HAS_TMPRO
            var tmp = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "Option A";
            tmp.color = Color.black;
#else
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = "Option A";
            labelText.color = Color.black;
#endif
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = new Vector2(-20, 0);
            labelRect.anchoredPosition = new Vector2(-5, 0);

            var dropdown = go.AddComponent<Dropdown>();
            dropdown.targetGraphic = image;

#if UNITY_2023_1_OR_NEWER && HAS_TMPRO
            // TMP dropdown captionText assignment would require TMP_Dropdown
#else
            dropdown.captionText = labelText;
#endif

            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData("Option A"));
            dropdown.options.Add(new Dropdown.OptionData("Option B"));
            dropdown.options.Add(new Dropdown.OptionData("Option C"));

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 30);
            return go;
        }

        private static GameObject CreateInputField(Transform parent, UIAddElementParams p)
        {
            var go = CreateUIGameObject("InputField", parent);
            var image = go.AddComponent<Image>();
            image.color = Color.white;

            // Placeholder
            var placeholderGo = CreateUIGameObject("Placeholder", go.transform);
#if UNITY_2023_1_OR_NEWER && HAS_TMPRO
            var phTmp = placeholderGo.AddComponent<TMPro.TextMeshProUGUI>();
            phTmp.text = "Enter text...";
            phTmp.fontStyle = TMPro.FontStyles.Italic;
            phTmp.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
#else
            var phText = placeholderGo.AddComponent<Text>();
            phText.text = "Enter text...";
            phText.fontStyle = FontStyle.Italic;
            phText.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
#endif
            var phRect = placeholderGo.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = new Vector2(-10, 0);

            // Text child
            var textGo = CreateUIGameObject("Text", go.transform);
#if UNITY_2023_1_OR_NEWER && HAS_TMPRO
            var txtTmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            txtTmp.text = "";
            txtTmp.color = Color.black;
#else
            var txtText = textGo.AddComponent<Text>();
            txtText.text = "";
            txtText.color = Color.black;
            txtText.supportRichText = false;
#endif
            var txtRect = textGo.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = new Vector2(-10, 0);

            var inputField = go.AddComponent<InputField>();
            inputField.targetGraphic = image;

#if UNITY_2023_1_OR_NEWER && HAS_TMPRO
            // TMP input field would require TMP_InputField
#else
            inputField.textComponent = txtText;
            inputField.placeholder = phText;
#endif

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 30);
            return go;
        }

        /// <summary>
        /// Creates a new GameObject with a RectTransform (required for all UI elements)
        /// and parents it under the given transform.
        /// </summary>
        private static GameObject CreateUIGameObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
