using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Core.Assets;

namespace Mosaic.Bridge.Core.SerializedValues
{
    /// <summary>
    /// O4 §3.2: the one place that turns a JSON value into a SerializedProperty assignment and
    /// back. Before this, component/set_property rejected enums and LayerMask outright
    /// (TYPE_MISMATCH), had no array case at all (so LightProbeGroup.probePositions, LineRenderer
    /// positions, RuleTile.m_TilingRules were unreachable), and neither setter supported
    /// AnimationCurve or Gradient. Struct-valued members (ColorBlock, JointLimits, JointDrive,
    /// RectOffset, ...) could not be assigned atomically — every sub-field needed its own call.
    /// This codec is recursive precisely so a struct field's own children (which may themselves be
    /// arrays, enums, or nested structs) go through the exact same rules as a top-level property.
    /// </summary>
    public static class SerializedPropertyCodec
    {
        /// <summary>Assigns `value` to `prop`, recursing into struct fields and array elements.
        /// Returns false with a specific, field-qualified `error` on any mismatch — never assigns
        /// a partial or wrong-typed value and reports success.</summary>
        public static bool TrySet(SerializedProperty prop, JToken value, Func<string, Type> lookupType, out string error)
        {
            error = null;
            if (prop == null) { error = "Property is null"; return false; }

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:
                    return TryConvert(value, v => prop.floatValue = (float)v, out error);
                case SerializedPropertyType.Integer:
                    return TryConvert(value, v => prop.intValue = (int)v, out error);
                case SerializedPropertyType.Boolean:
                    return TryConvert(value, v => prop.boolValue = (bool)v, out error);
                case SerializedPropertyType.String:
                    return TryConvert(value, v => prop.stringValue = (string)v, out error);
                case SerializedPropertyType.Vector2:
                    return TrySetFloatN(prop, value, 2, (p, a) => p.vector2Value = new Vector2(a[0], a[1]), out error);
                case SerializedPropertyType.Vector3:
                    return TrySetFloatN(prop, value, 3, (p, a) => p.vector3Value = new Vector3(a[0], a[1], a[2]), out error);
                case SerializedPropertyType.Vector4:
                    return TrySetFloatN(prop, value, 4, (p, a) => p.vector4Value = new Vector4(a[0], a[1], a[2], a[3]), out error);
                case SerializedPropertyType.Quaternion:
                    return TrySetFloatN(prop, value, 4, (p, a) => p.quaternionValue = new Quaternion(a[0], a[1], a[2], a[3]), out error);
                case SerializedPropertyType.Color:
                    return TrySetColor(prop, value, out error);
                case SerializedPropertyType.ObjectReference:
                    return TrySetObjectReference(prop, value, lookupType, out error);
                case SerializedPropertyType.Enum:
                    return TrySetEnum(prop, value, out error);
                case SerializedPropertyType.LayerMask:
                    return TrySetLayerMask(prop, value, out error);
                case SerializedPropertyType.Gradient:
                    return TrySetGradient(prop, value, out error);
                case SerializedPropertyType.AnimationCurve:
                    return TrySetAnimationCurve(prop, value, out error);
                case SerializedPropertyType.Generic:
                    return prop.isArray
                        ? TrySetArray(prop, value, lookupType, out error)
                        : TrySetStruct(prop, value, lookupType, out error);
                default:
                    error = $"Unsupported property type: {prop.propertyType}";
                    return false;
            }
        }

        /// <summary>Full read-back mirror of TrySet — used directly by callers that want the new
        /// types (Enum/LayerMask/Gradient/AnimationCurve/struct/array) and recursively by
        /// StructToJson/ArrayToJson so a struct's own primitive fields read back as real values,
        /// not just their type name.</summary>
        public static object Get(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:   return prop.floatValue;
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.String:  return prop.stringValue;
                case SerializedPropertyType.Vector2:
                    return new float[] { prop.vector2Value.x, prop.vector2Value.y };
                case SerializedPropertyType.Vector3:
                    return new float[] { prop.vector3Value.x, prop.vector3Value.y, prop.vector3Value.z };
                case SerializedPropertyType.Vector4:
                    var v4 = prop.vector4Value;
                    return new float[] { v4.x, v4.y, v4.z, v4.w };
                case SerializedPropertyType.Quaternion:
                    var q = prop.quaternionValue;
                    return new float[] { q.x, q.y, q.z, q.w };
                case SerializedPropertyType.Color:
                    var col = prop.colorValue;
                    return new float[] { col.r, col.g, col.b, col.a };
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? AssetDatabase.GetAssetPath(prop.objectReferenceValue) : null;
                case SerializedPropertyType.Enum:
                    return prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length
                        ? prop.enumNames[prop.enumValueIndex]
                        : (object)prop.enumValueIndex;
                case SerializedPropertyType.LayerMask:
                    return prop.intValue;
                case SerializedPropertyType.Gradient:
                    return GradientToJson(prop.gradientValue);
                case SerializedPropertyType.AnimationCurve:
                    return AnimationCurveToJson(prop.animationCurveValue);
                case SerializedPropertyType.Generic:
                    return prop.isArray ? (object)ArrayToJson(prop) : StructToJson(prop);
                default:
                    return prop.type;
            }
        }

        // ── Primitives ───────────────────────────────────────────────────────

        private static bool TryConvert(JToken value, Action<JToken> assign, out string error)
        {
            error = null;
            try { assign(value); return true; }
            catch (Exception e) { error = $"Could not convert '{value}': {e.Message}"; return false; }
        }

        private static bool TrySetFloatN(SerializedProperty prop, JToken value, int n, Action<SerializedProperty, float[]> assign, out string error)
        {
            error = null;
            var arr = value as JArray;
            if (arr == null || arr.Count < n)
            {
                error = $"Property '{prop.propertyPath}' ({prop.propertyType}) requires a {n}-element float array";
                return false;
            }
            var floats = new float[n];
            for (int i = 0; i < n; i++) floats[i] = arr[i].Value<float>();
            assign(prop, floats);
            return true;
        }

        private static bool TrySetColor(SerializedProperty prop, JToken value, out string error)
        {
            error = null;
            var arr = value as JArray;
            if (arr == null || arr.Count < 3)
            {
                error = $"Property '{prop.propertyPath}' (Color) requires a [r, g, b] or [r, g, b, a] float array";
                return false;
            }
            prop.colorValue = new Color(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>(),
                arr.Count >= 4 ? arr[3].Value<float>() : 1f);
            return true;
        }

        // ── Object reference ─────────────────────────────────────────────────

        private static bool TrySetObjectReference(SerializedProperty prop, JToken value, Func<string, Type> lookupType, out string error)
        {
            error = null;
            var path = value?.Type == JTokenType.String ? value.Value<string>() : null;
            if (string.IsNullOrEmpty(path)) return true; // no-op, matches the previous behavior for an empty target
            var fieldType = ObjectReferenceResolver.ResolveFieldType(prop, lookupType);
            if (!ObjectReferenceResolver.TryResolveAsset(path, fieldType, out var resolved, out error)) return false;
            prop.objectReferenceValue = resolved;
            return true;
        }

        // ── Enum ─────────────────────────────────────────────────────────────

        private static bool TrySetEnum(SerializedProperty prop, JToken value, out string error)
        {
            error = null;
            if (value?.Type == JTokenType.String)
            {
                var name = value.Value<string>();
                var idx = Array.FindIndex(prop.enumNames, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                if (idx < 0)
                {
                    error = $"Enum value '{name}' not valid for '{prop.propertyPath}'. Valid names: {string.Join(", ", prop.enumNames)}";
                    return false;
                }
                prop.enumValueIndex = idx;
                return true;
            }
            if (value?.Type == JTokenType.Integer)
            {
                var idx = value.Value<int>();
                if (idx < 0 || idx >= prop.enumNames.Length)
                {
                    error = $"Enum index {idx} out of range for '{prop.propertyPath}'. Valid names: {string.Join(", ", prop.enumNames)}";
                    return false;
                }
                prop.enumValueIndex = idx;
                return true;
            }
            error = $"Enum property '{prop.propertyPath}' requires a member name string or an index int. Valid names: {string.Join(", ", prop.enumNames)}";
            return false;
        }

        // ── LayerMask ────────────────────────────────────────────────────────

        private static bool TrySetLayerMask(SerializedProperty prop, JToken value, out string error)
        {
            error = null;
            if (value?.Type == JTokenType.Integer)
            {
                prop.intValue = value.Value<int>();
                return true;
            }
            var arr = value as JArray;
            if (arr == null)
            {
                error = $"LayerMask property '{prop.propertyPath}' requires an int bitmask or an array of layer names/indices";
                return false;
            }
            int mask = 0;
            foreach (var item in arr)
            {
                int layer;
                if (item.Type == JTokenType.String)
                {
                    layer = LayerMask.NameToLayer(item.Value<string>());
                    if (layer < 0)
                    {
                        error = $"Unknown layer name '{item.Value<string>()}' for '{prop.propertyPath}'";
                        return false;
                    }
                }
                else
                {
                    layer = item.Value<int>();
                }
                if (layer < 0 || layer > 31)
                {
                    error = $"Layer index {layer} out of range (0-31) for '{prop.propertyPath}'";
                    return false;
                }
                mask |= 1 << layer;
            }
            prop.intValue = mask;
            return true;
        }

        // ── Gradient / AnimationCurve ────────────────────────────────────────

        private static bool TrySetGradient(SerializedProperty prop, JToken value, out string error)
        {
            error = null;
            var obj = value as JObject;
            if (obj == null)
            {
                error = $"Gradient property '{prop.propertyPath}' requires {{colorKeys:[{{color:[r,g,b,a],time}}], alphaKeys:[{{alpha,time}}]}}";
                return false;
            }
            var colorKeysJson = obj["colorKeys"] as JArray;
            var alphaKeysJson = obj["alphaKeys"] as JArray;
            if (colorKeysJson == null || colorKeysJson.Count < 2)
            {
                // Confirmed against a real Gradient at runtime: Unity silently pads a single color
                // key up to two (duplicating it) rather than keeping the gradient flat at one —
                // failing loudly here beats reporting success for a gradient that isn't what was asked for.
                error = $"Gradient property '{prop.propertyPath}' requires at least 2 'colorKeys' " +
                        "(Unity pads a single key to two rather than keeping a flat gradient, so fewer than 2 " +
                        "would silently produce a different result than requested)";
                return false;
            }
            var colorKeys = new GradientColorKey[colorKeysJson.Count];
            for (int i = 0; i < colorKeysJson.Count; i++)
            {
                var k = colorKeysJson[i];
                var c = k["color"] as JArray;
                if (c == null || c.Count < 3)
                {
                    error = $"colorKeys[{i}].color must be a [r, g, b] or [r, g, b, a] float array";
                    return false;
                }
                colorKeys[i] = new GradientColorKey(
                    new Color(c[0].Value<float>(), c[1].Value<float>(), c[2].Value<float>(), c.Count >= 4 ? c[3].Value<float>() : 1f),
                    k["time"]?.Value<float>() ?? 0f);
            }
            GradientAlphaKey[] alphaKeys;
            if (alphaKeysJson == null || alphaKeysJson.Count == 0)
            {
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
            }
            else
            {
                alphaKeys = new GradientAlphaKey[alphaKeysJson.Count];
                for (int i = 0; i < alphaKeysJson.Count; i++)
                {
                    var k = alphaKeysJson[i];
                    alphaKeys[i] = new GradientAlphaKey(k["alpha"]?.Value<float>() ?? 1f, k["time"]?.Value<float>() ?? 0f);
                }
            }
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            prop.gradientValue = gradient;
            return true;
        }

        private static bool TrySetAnimationCurve(SerializedProperty prop, JToken value, out string error)
        {
            error = null;
            var arr = value as JArray;
            if (arr == null || arr.Count == 0)
            {
                error = $"AnimationCurve property '{prop.propertyPath}' requires a non-empty array of " +
                         "{time, value, inTangent?, outTangent?} keyframes";
                return false;
            }
            var keys = new Keyframe[arr.Count];
            for (int i = 0; i < arr.Count; i++)
            {
                var k = arr[i];
                if (k["time"] == null || k["value"] == null)
                {
                    error = $"keys[{i}] must have 'time' and 'value'";
                    return false;
                }
                keys[i] = new Keyframe(
                    k["time"].Value<float>(), k["value"].Value<float>(),
                    k["inTangent"]?.Value<float>() ?? 0f, k["outTangent"]?.Value<float>() ?? 0f);
            }
            prop.animationCurveValue = new AnimationCurve(keys);
            return true;
        }

        // ── Generic: struct and array ────────────────────────────────────────

        private static bool TrySetStruct(SerializedProperty prop, JToken value, Func<string, Type> lookupType, out string error)
        {
            error = null;
            var obj = value as JObject;
            if (obj == null)
            {
                error = $"Property '{prop.propertyPath}' is a struct ({prop.type}) and requires a JSON object of its field names";
                return false;
            }
            foreach (var field in obj.Properties())
            {
                var child = FindRelativeFuzzy(prop, field.Name);
                if (child == null)
                {
                    error = $"'{prop.propertyPath}' has no field '{field.Name}' (tried exact and 'm_'-prefixed)";
                    return false;
                }
                if (!TrySet(child, field.Value, lookupType, out var childError))
                {
                    error = $"{prop.propertyPath}.{field.Name}: {childError}";
                    return false;
                }
            }
            return true;
        }

        private static bool TrySetArray(SerializedProperty prop, JToken value, Func<string, Type> lookupType, out string error)
        {
            error = null;
            var arr = value as JArray;
            if (arr == null)
            {
                error = $"Property '{prop.propertyPath}' is an array and requires a JSON array";
                return false;
            }
            prop.arraySize = arr.Count;
            for (int i = 0; i < arr.Count; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                if (!TrySet(element, arr[i], lookupType, out var elementError))
                {
                    error = $"{prop.propertyPath}[{i}]: {elementError}";
                    return false;
                }
            }
            return true;
        }

        /// <summary>FindPropertyRelative with the same 'm_' prefix fallback ComponentSetReferenceTool
        /// already applies at the top level — struct fields follow the identical Unity convention.</summary>
        private static SerializedProperty FindRelativeFuzzy(SerializedProperty parent, string name)
        {
            var direct = parent.FindPropertyRelative(name);
            if (direct != null) return direct;
            var mName = name.StartsWith("m_", StringComparison.Ordinal) ? name : "m_" + name;
            return parent.FindPropertyRelative(mName);
        }

        // ── Read-back JSON shapes ────────────────────────────────────────────

        private static JObject GradientToJson(Gradient g)
        {
            var colorKeys = new JArray(g.colorKeys.Select(k => new JObject
            {
                ["color"] = new JArray(k.color.r, k.color.g, k.color.b, k.color.a),
                ["time"] = k.time,
            }));
            var alphaKeys = new JArray(g.alphaKeys.Select(k => new JObject
            {
                ["alpha"] = k.alpha,
                ["time"] = k.time,
            }));
            return new JObject { ["colorKeys"] = colorKeys, ["alphaKeys"] = alphaKeys };
        }

        private static JArray AnimationCurveToJson(AnimationCurve curve) =>
            new JArray(curve.keys.Select(k => new JObject
            {
                ["time"] = k.time,
                ["value"] = k.value,
                ["inTangent"] = k.inTangent,
                ["outTangent"] = k.outTangent,
            }));

        private static JArray ArrayToJson(SerializedProperty prop)
        {
            var arr = new JArray();
            for (int i = 0; i < prop.arraySize; i++)
                arr.Add(ToJToken(Get(prop.GetArrayElementAtIndex(i))));
            return arr;
        }

        private static JObject StructToJson(SerializedProperty prop)
        {
            var obj = new JObject();
            var end = prop.GetEndProperty();
            var child = prop.Copy();
            if (!child.NextVisible(true)) return obj;
            do
            {
                if (SerializedProperty.EqualContents(child, end)) break;
                obj[child.name] = ToJToken(Get(child));
            } while (child.NextVisible(false));
            return obj;
        }

        private static JToken ToJToken(object value) => value == null ? JValue.CreateNull() : JToken.FromObject(value);
    }
}
