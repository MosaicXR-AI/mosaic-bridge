using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Materials
{
    public static class MaterialSetPropertyTool
    {
        [MosaicTool("material/set-property",
                    "Sets a shader property value on a material asset. ValueType: float | int | color | vector | texture | bool | keyword. bool supports material flags (enableInstancing, doubleSidedGI). keyword toggles shader keywords (_EMISSION, _NORMALMAP, _ALPHATEST_ON, etc).",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<MaterialSetPropertyResult> Execute(MaterialSetPropertyParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<MaterialSetPropertyResult>.Fail(
                    "Path is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.Property))
                return ToolResult<MaterialSetPropertyResult>.Fail(
                    "Property is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.ValueType))
                return ToolResult<MaterialSetPropertyResult>.Fail(
                    "ValueType is required", ErrorCodes.INVALID_PARAM);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(p.Path);
            if (mat == null)
                return ToolResult<MaterialSetPropertyResult>.Fail(
                    $"Material not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            // Material-level flags + keyword ops bypass HasProperty because they
            // aren't shader properties — they're flags on the Material object itself.
            var valueType = p.ValueType.ToLowerInvariant();
            var isMaterialFlag = valueType == "bool" && IsKnownMaterialFlag(p.Property);
            var isKeywordOp    = valueType == "keyword";

            if (!isMaterialFlag && !isKeywordOp && !mat.HasProperty(p.Property))
                return ToolResult<MaterialSetPropertyResult>.Fail(
                    $"Material shader does not have property '{p.Property}'", ErrorCodes.NOT_FOUND);

            switch (valueType)
            {
                case "float":
                    mat.SetFloat(p.Property, p.FloatValue);
                    break;

                case "int":
                    mat.SetInt(p.Property, p.IntValue);
                    break;

                case "bool":
                    // Material flags (enableInstancing, doubleSidedGI). Fall through
                    // to generic shader property if not a known flag (some custom
                    // shader-graph bools may live as floats).
                    if (!ApplyMaterialFlag(mat, p.Property, p.BoolValue))
                    {
                        // Fallback: write 1/0 to a float shader property.
                        mat.SetFloat(p.Property, p.BoolValue ? 1f : 0f);
                    }
                    break;

                case "keyword":
                    if (p.BoolValue) mat.EnableKeyword(p.Property);
                    else             mat.DisableKeyword(p.Property);
                    break;

                case "color":
                    if (p.ColorValue == null || p.ColorValue.Length < 3)
                        return ToolResult<MaterialSetPropertyResult>.Fail(
                            "ColorValue must have at least 3 elements [r,g,b] or 4 [r,g,b,a]",
                            ErrorCodes.INVALID_PARAM);
                    var color = new Color(
                        p.ColorValue[0],
                        p.ColorValue[1],
                        p.ColorValue[2],
                        p.ColorValue.Length >= 4 ? p.ColorValue[3] : 1f);
                    mat.SetColor(p.Property, color);
                    break;

                case "vector":
                    if (p.VectorValue == null || p.VectorValue.Length < 2)
                        return ToolResult<MaterialSetPropertyResult>.Fail(
                            "VectorValue must have at least 2 elements",
                            ErrorCodes.INVALID_PARAM);
                    var vector = new Vector4(
                        p.VectorValue.Length >= 1 ? p.VectorValue[0] : 0f,
                        p.VectorValue.Length >= 2 ? p.VectorValue[1] : 0f,
                        p.VectorValue.Length >= 3 ? p.VectorValue[2] : 0f,
                        p.VectorValue.Length >= 4 ? p.VectorValue[3] : 0f);
                    mat.SetVector(p.Property, vector);
                    break;

                case "texture":
                    if (string.IsNullOrEmpty(p.TexturePath))
                        return ToolResult<MaterialSetPropertyResult>.Fail(
                            "TexturePath is required for ValueType 'texture'",
                            ErrorCodes.INVALID_PARAM);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture>(p.TexturePath);
                    if (texture == null)
                        return ToolResult<MaterialSetPropertyResult>.Fail(
                            $"Texture not found at '{p.TexturePath}'", ErrorCodes.NOT_FOUND);
                    mat.SetTexture(p.Property, texture);
                    break;

                default:
                    return ToolResult<MaterialSetPropertyResult>.Fail(
                        $"Unknown ValueType '{p.ValueType}'. Expected: float, int, color, vector, texture, bool, keyword",
                        ErrorCodes.INVALID_PARAM);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            return ToolResult<MaterialSetPropertyResult>.Ok(new MaterialSetPropertyResult
            {
                Path      = p.Path,
                Property  = p.Property,
                ValueType = p.ValueType,
                Applied   = true
            });
        }

        private static bool IsKnownMaterialFlag(string propertyName)
        {
            switch (propertyName)
            {
                case "enableInstancing":
                case "doubleSidedGI":
                    return true;
                default:
                    return false;
            }
        }

        private static bool ApplyMaterialFlag(Material mat, string propertyName, bool value)
        {
            switch (propertyName)
            {
                case "enableInstancing":
                    mat.enableInstancing = value;
                    return true;
                case "doubleSidedGI":
                    mat.doubleSidedGI = value;
                    return true;
                default:
                    return false;
            }
        }
    }
}
