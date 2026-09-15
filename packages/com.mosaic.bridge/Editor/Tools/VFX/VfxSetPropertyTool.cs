using UnityEngine;
using UnityEngine.VFX;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.VFX
{
    public static class VfxSetPropertyTool
    {
        [MosaicTool("vfx/set-property",
                    "Sets an exposed VisualEffect graph property by name. ValueType selects which value " +
                    "field is used: float/int/bool/vector2/vector3/vector4 (FloatValue/IntValue/BoolValue/" +
                    "VectorValue), texture (TexturePath), mesh (MeshPath), gradient " +
                    "(GradientColorKeyTimes/Colors + GradientAlphaKeyTimes/Values), curve " +
                    "(CurveKeyTimes/Values). Fails with a clear message if the property doesn't exist " +
                    "on the graph rather than silently doing nothing.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<VfxSetPropertyResult> Execute(VfxSetPropertyParams p)
        {
            var go = VfxToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<VfxSetPropertyResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            var vfx = go.GetComponent<VisualEffect>();
            if (vfx == null)
                return ToolResult<VfxSetPropertyResult>.Fail(
                    $"No VisualEffect component on '{go.name}'", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(vfx, "Mosaic: Set VFX Property");
            string valueType = p.ValueType?.ToLowerInvariant();

            switch (valueType)
            {
                case "float":
                    if (!p.FloatValue.HasValue) return Missing<float>("FloatValue");
                    if (!vfx.HasFloat(p.PropertyName)) return NoSuchProperty("float");
                    vfx.SetFloat(p.PropertyName, p.FloatValue.Value);
                    break;

                case "int":
                    if (!p.IntValue.HasValue) return Missing<int>("IntValue");
                    if (!vfx.HasInt(p.PropertyName)) return NoSuchProperty("int");
                    vfx.SetInt(p.PropertyName, p.IntValue.Value);
                    break;

                case "bool":
                    if (!p.BoolValue.HasValue) return Missing<bool>("BoolValue");
                    if (!vfx.HasBool(p.PropertyName)) return NoSuchProperty("bool");
                    vfx.SetBool(p.PropertyName, p.BoolValue.Value);
                    break;

                case "vector2":
                    if (p.VectorValue == null || p.VectorValue.Length != 2)
                        return ToolResult<VfxSetPropertyResult>.Fail("VectorValue requires exactly [x, y]", ErrorCodes.INVALID_PARAM);
                    if (!vfx.HasVector2(p.PropertyName)) return NoSuchProperty("vector2");
                    vfx.SetVector2(p.PropertyName, new Vector2(p.VectorValue[0], p.VectorValue[1]));
                    break;

                case "vector3":
                    if (p.VectorValue == null || p.VectorValue.Length != 3)
                        return ToolResult<VfxSetPropertyResult>.Fail("VectorValue requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                    if (!vfx.HasVector3(p.PropertyName)) return NoSuchProperty("vector3");
                    vfx.SetVector3(p.PropertyName, new Vector3(p.VectorValue[0], p.VectorValue[1], p.VectorValue[2]));
                    break;

                case "vector4":
                    if (p.VectorValue == null || p.VectorValue.Length != 4)
                        return ToolResult<VfxSetPropertyResult>.Fail("VectorValue requires exactly [x, y, z, w]", ErrorCodes.INVALID_PARAM);
                    if (!vfx.HasVector4(p.PropertyName)) return NoSuchProperty("vector4");
                    vfx.SetVector4(p.PropertyName, new Vector4(p.VectorValue[0], p.VectorValue[1], p.VectorValue[2], p.VectorValue[3]));
                    break;

                case "texture":
                    if (string.IsNullOrEmpty(p.TexturePath))
                        return ToolResult<VfxSetPropertyResult>.Fail("TexturePath is required for texture", ErrorCodes.INVALID_PARAM);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture>(p.TexturePath);
                    if (texture == null)
                        return ToolResult<VfxSetPropertyResult>.Fail($"Texture not found at '{p.TexturePath}'", ErrorCodes.NOT_FOUND);
                    if (!vfx.HasTexture(p.PropertyName)) return NoSuchProperty("texture");
                    vfx.SetTexture(p.PropertyName, texture);
                    break;

                case "mesh":
                    if (string.IsNullOrEmpty(p.MeshPath))
                        return ToolResult<VfxSetPropertyResult>.Fail("MeshPath is required for mesh", ErrorCodes.INVALID_PARAM);
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(p.MeshPath);
                    if (mesh == null)
                        return ToolResult<VfxSetPropertyResult>.Fail($"Mesh not found at '{p.MeshPath}'", ErrorCodes.NOT_FOUND);
                    if (!vfx.HasMesh(p.PropertyName)) return NoSuchProperty("mesh");
                    vfx.SetMesh(p.PropertyName, mesh);
                    break;

                case "gradient":
                    if (!vfx.HasGradient(p.PropertyName)) return NoSuchProperty("gradient");
                    vfx.SetGradient(p.PropertyName, BuildGradient(p));
                    break;

                case "curve":
                    if (p.CurveKeyTimes == null || p.CurveKeyValues == null ||
                        p.CurveKeyTimes.Length != p.CurveKeyValues.Length || p.CurveKeyTimes.Length == 0)
                        return ToolResult<VfxSetPropertyResult>.Fail(
                            "CurveKeyTimes/CurveKeyValues must be non-empty and the same length", ErrorCodes.INVALID_PARAM);
                    if (!vfx.HasAnimationCurve(p.PropertyName)) return NoSuchProperty("curve");
                    var keys = new Keyframe[p.CurveKeyTimes.Length];
                    for (int i = 0; i < keys.Length; i++)
                        keys[i] = new Keyframe(p.CurveKeyTimes[i], p.CurveKeyValues[i]);
                    vfx.SetAnimationCurve(p.PropertyName, new AnimationCurve(keys));
                    break;

                default:
                    return ToolResult<VfxSetPropertyResult>.Fail(
                        $"Unknown ValueType '{p.ValueType}'. Valid: float, int, bool, vector2, vector3, " +
                        "vector4, texture, mesh, gradient, curve", ErrorCodes.INVALID_PARAM);
            }

            return ToolResult<VfxSetPropertyResult>.Ok(new VfxSetPropertyResult
            {
                GameObjectName = go.name,
                InstanceId     = UnityIds.Of(go),
                PropertyName   = p.PropertyName,
                ValueType      = p.ValueType,
                Message        = $"Set '{p.PropertyName}' ({p.ValueType}) on '{go.name}'.",
            });

            ToolResult<VfxSetPropertyResult> NoSuchProperty(string kind) =>
                ToolResult<VfxSetPropertyResult>.Fail(
                    $"'{go.name}'s VisualEffect graph has no exposed {kind} property named '{p.PropertyName}'.",
                    ErrorCodes.NOT_FOUND);

            ToolResult<VfxSetPropertyResult> Missing<T>(string field) =>
                ToolResult<VfxSetPropertyResult>.Fail($"{field} is required for ValueType={p.ValueType}", ErrorCodes.INVALID_PARAM);
        }

        private static Gradient BuildGradient(VfxSetPropertyParams p)
        {
            var gradient = new Gradient();
            int colorCount = p.GradientColorKeyTimes?.Length ?? 0;
            var colorKeys = new GradientColorKey[colorCount > 0 ? colorCount : 2];
            if (colorCount > 0)
            {
                for (int i = 0; i < colorCount; i++)
                    colorKeys[i] = new GradientColorKey(
                        new Color(p.GradientColorKeyColors[i * 3], p.GradientColorKeyColors[i * 3 + 1], p.GradientColorKeyColors[i * 3 + 2]),
                        p.GradientColorKeyTimes[i]);
            }
            else
            {
                colorKeys[0] = new GradientColorKey(Color.white, 0f);
                colorKeys[1] = new GradientColorKey(Color.white, 1f);
            }

            int alphaCount = p.GradientAlphaKeyTimes?.Length ?? 0;
            var alphaKeys = new GradientAlphaKey[alphaCount > 0 ? alphaCount : 2];
            if (alphaCount > 0)
            {
                for (int i = 0; i < alphaCount; i++)
                    alphaKeys[i] = new GradientAlphaKey(p.GradientAlphaKeyValues[i], p.GradientAlphaKeyTimes[i]);
            }
            else
            {
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            }

            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }
    }
}
