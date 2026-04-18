using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.UI
{
    public static class UISetPropertiesTool
    {
        [MosaicTool("ui/set_properties",
                    "Sets element-specific properties on a UI component (Text, Image, Button, etc.). Auto-detects component type.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<UISetPropertiesResult> Execute(UISetPropertiesParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.Name))
                return ToolResult<UISetPropertiesResult>.Fail(
                    "Either InstanceId or Name is required", ErrorCodes.INVALID_PARAM);

            var go = UIToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<UISetPropertiesResult>.Fail(
                    $"GameObject not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                    ErrorCodes.NOT_FOUND);

            var modified = new List<string>();
            string detectedType = "Unknown";

            // Detect and apply to Text / TMP_Text
            var textComponent = go.GetComponent<Text>();

#if UNITY_2023_1_OR_NEWER && HAS_TMPRO
            var tmpComponent = go.GetComponent<TMPro.TMP_Text>();
            if (tmpComponent != null)
            {
                detectedType = tmpComponent.GetType().Name;
                Undo.RecordObject(tmpComponent, "Mosaic: Set UI Properties");

                if (p.Text != null)
                {
                    tmpComponent.text = p.Text;
                    modified.Add("Text");
                }
                if (p.FontSize.HasValue)
                {
                    tmpComponent.fontSize = p.FontSize.Value;
                    modified.Add("FontSize");
                }
                if (p.Color != null && p.Color.Length >= 4)
                {
                    tmpComponent.color = new Color(p.Color[0], p.Color[1], p.Color[2], p.Color[3]);
                    modified.Add("Color");
                }

                EditorUtility.SetDirty(tmpComponent);
            }
            else
#endif
            if (textComponent != null)
            {
                detectedType = "Text";
                Undo.RecordObject(textComponent, "Mosaic: Set UI Properties");

                if (p.Text != null)
                {
                    textComponent.text = p.Text;
                    modified.Add("Text");
                }
                if (p.FontSize.HasValue)
                {
                    textComponent.fontSize = p.FontSize.Value;
                    modified.Add("FontSize");
                }
                if (p.Color != null && p.Color.Length >= 4)
                {
                    textComponent.color = new Color(p.Color[0], p.Color[1], p.Color[2], p.Color[3]);
                    modified.Add("Color");
                }

                EditorUtility.SetDirty(textComponent);
            }

            // Detect and apply to Image
            var imageComponent = go.GetComponent<Image>();
            if (imageComponent != null)
            {
                if (detectedType == "Unknown")
                    detectedType = "Image";

                Undo.RecordObject(imageComponent, "Mosaic: Set UI Properties");

                if (p.Color != null && p.Color.Length >= 4 && textComponent == null)
                {
                    imageComponent.color = new Color(p.Color[0], p.Color[1], p.Color[2], p.Color[3]);
                    if (!modified.Contains("Color"))
                        modified.Add("Color");
                }

                if (!string.IsNullOrEmpty(p.Sprite))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p.Sprite);
                    if (sprite != null)
                    {
                        imageComponent.sprite = sprite;
                        modified.Add("Sprite");
                    }
                    else
                    {
                        return ToolResult<UISetPropertiesResult>.Fail(
                            $"Sprite not found at path '{p.Sprite}'", ErrorCodes.NOT_FOUND);
                    }
                }

                EditorUtility.SetDirty(imageComponent);
            }

            // Detect and apply Interactable to Selectable-derived components
            if (p.Interactable.HasValue)
            {
                var selectable = go.GetComponent<Selectable>();
                if (selectable != null)
                {
                    if (detectedType == "Unknown")
                        detectedType = selectable.GetType().Name;

                    Undo.RecordObject(selectable, "Mosaic: Set UI Properties");
                    selectable.interactable = p.Interactable.Value;
                    modified.Add("Interactable");
                    EditorUtility.SetDirty(selectable);
                }
            }

            // Detect Button for type label
            var button = go.GetComponent<Button>();
            if (button != null && detectedType == "Unknown")
                detectedType = "Button";

            // Apply Text to InputField
            var inputField = go.GetComponent<InputField>();
            if (inputField != null && p.Text != null && textComponent == null)
            {
                if (detectedType == "Unknown")
                    detectedType = "InputField";

                Undo.RecordObject(inputField, "Mosaic: Set UI Properties");
                inputField.text = p.Text;
                if (!modified.Contains("Text"))
                    modified.Add("Text");
                EditorUtility.SetDirty(inputField);
            }

            if (modified.Count == 0)
                return ToolResult<UISetPropertiesResult>.Fail(
                    "No applicable UI components found or no properties to set",
                    ErrorCodes.NOT_FOUND);

            return ToolResult<UISetPropertiesResult>.Ok(new UISetPropertiesResult
            {
                InstanceId            = go.GetInstanceID(),
                Name                  = go.name,
                ModifiedProperties    = modified.ToArray(),
                DetectedComponentType = detectedType
            });
        }
    }
}
