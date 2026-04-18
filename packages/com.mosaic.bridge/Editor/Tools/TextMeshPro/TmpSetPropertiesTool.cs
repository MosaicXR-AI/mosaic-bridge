#if MOSAIC_HAS_TMP
using UnityEngine;
using UnityEditor;
using TMPro;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.TextMeshPro
{
    public static class TmpSetPropertiesTool
    {
        [MosaicTool("tmp/set_properties",
                    "Configures properties on a TextMeshPro component (text, font size, color, alignment, style, overflow, margins, spacing, rich text)",
                    isReadOnly: false)]
        public static ToolResult<TmpSetPropertiesResult> Execute(TmpSetPropertiesParams p)
        {
            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<TmpSetPropertiesResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found.",
                    ErrorCodes.NOT_FOUND);

            var tmp = go.GetComponent<TMP_Text>();
            if (tmp == null)
                return ToolResult<TmpSetPropertiesResult>.Fail(
                    $"No TextMeshPro component found on '{p.GameObjectName}'.",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(tmp, "Mosaic: TMP Set Properties");

            int applied = 0;

            if (p.Text != null)
            {
                tmp.text = p.Text;
                applied++;
            }

            if (p.FontSize.HasValue)
            {
                tmp.fontSize = p.FontSize.Value;
                applied++;
            }

            if (p.Color != null && p.Color.Length == 4)
            {
                tmp.color = new Color(p.Color[0], p.Color[1], p.Color[2], p.Color[3]);
                applied++;
            }

            if (!string.IsNullOrEmpty(p.Alignment))
            {
                if (TryParseAlignment(p.Alignment, out var alignment))
                {
                    tmp.alignment = alignment;
                    applied++;
                }
                else
                {
                    return ToolResult<TmpSetPropertiesResult>.Fail(
                        $"Invalid Alignment '{p.Alignment}'. Valid: left, center, right, justified.",
                        ErrorCodes.INVALID_PARAM);
                }
            }

            if (!string.IsNullOrEmpty(p.FontStyle))
            {
                if (TryParseFontStyle(p.FontStyle, out var style))
                {
                    tmp.fontStyle = style;
                    applied++;
                }
                else
                {
                    return ToolResult<TmpSetPropertiesResult>.Fail(
                        $"Invalid FontStyle '{p.FontStyle}'. Valid: normal, bold, italic.",
                        ErrorCodes.INVALID_PARAM);
                }
            }

            if (!string.IsNullOrEmpty(p.OverflowMode))
            {
                if (TryParseOverflow(p.OverflowMode, out var overflow))
                {
                    tmp.overflowMode = overflow;
                    applied++;
                }
                else
                {
                    return ToolResult<TmpSetPropertiesResult>.Fail(
                        $"Invalid OverflowMode '{p.OverflowMode}'. Valid: overflow, ellipsis, truncate, page.",
                        ErrorCodes.INVALID_PARAM);
                }
            }

            if (p.Margins != null && p.Margins.Length == 4)
            {
                tmp.margin = new Vector4(p.Margins[0], p.Margins[1], p.Margins[2], p.Margins[3]);
                applied++;
            }

            if (p.LineSpacing.HasValue)
            {
                tmp.lineSpacing = p.LineSpacing.Value;
                applied++;
            }

            if (p.CharacterSpacing.HasValue)
            {
                tmp.characterSpacing = p.CharacterSpacing.Value;
                applied++;
            }

            if (p.WordSpacing.HasValue)
            {
                tmp.wordSpacing = p.WordSpacing.Value;
                applied++;
            }

            if (p.EnableRichText.HasValue)
            {
                tmp.richText = p.EnableRichText.Value;
                applied++;
            }

            EditorUtility.SetDirty(tmp);

            return ToolResult<TmpSetPropertiesResult>.Ok(new TmpSetPropertiesResult
            {
                GameObjectName     = go.name,
                AppliedPropertyCount = applied
            });
        }

        private static bool TryParseAlignment(string value, out TextAlignmentOptions result)
        {
            switch (value.ToLowerInvariant())
            {
                case "left":      result = TextAlignmentOptions.Left;      return true;
                case "center":    result = TextAlignmentOptions.Center;    return true;
                case "right":     result = TextAlignmentOptions.Right;     return true;
                case "justified": result = TextAlignmentOptions.Justified; return true;
                default:          result = default;                        return false;
            }
        }

        private static bool TryParseFontStyle(string value, out FontStyles result)
        {
            switch (value.ToLowerInvariant())
            {
                case "normal":  result = FontStyles.Normal;  return true;
                case "bold":    result = FontStyles.Bold;    return true;
                case "italic":  result = FontStyles.Italic;  return true;
                default:        result = default;            return false;
            }
        }

        private static bool TryParseOverflow(string value, out TextOverflowModes result)
        {
            switch (value.ToLowerInvariant())
            {
                case "overflow":  result = TextOverflowModes.Overflow;  return true;
                case "ellipsis":  result = TextOverflowModes.Ellipsis;  return true;
                case "truncate":  result = TextOverflowModes.Truncate;  return true;
                case "page":      result = TextOverflowModes.Page;      return true;
                default:          result = default;                     return false;
            }
        }
    }
}
#endif
