using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ScriptableObjects
{
    public static class ScriptableObjectSetFieldTool
    {
        [MosaicTool("scriptableobject/set-field",
                    "Sets a serialized field value on a ScriptableObject asset",
                    isReadOnly: false)]
        public static ToolResult<ScriptableObjectSetFieldResult> Execute(ScriptableObjectSetFieldParams p)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(p.AssetPath);
            if (asset == null)
                return ToolResult<ScriptableObjectSetFieldResult>.Fail(
                    $"ScriptableObject not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            var so = new SerializedObject(asset);
            var prop = so.FindProperty(p.FieldName);
            if (prop == null)
                return ToolResult<ScriptableObjectSetFieldResult>.Fail(
                    $"Field '{p.FieldName}' not found on ScriptableObject at '{p.AssetPath}'",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(asset, "Mosaic: Set ScriptableObject Field");

            var value = p.Value;
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:
                    prop.floatValue = System.Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.Integer:
                    prop.intValue = System.Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = System.Convert.ToBoolean(value);
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value?.ToString() ?? "";
                    break;
                case SerializedPropertyType.Vector3:
                    var arr = value as JArray;
                    if (arr == null || arr.Count < 3)
                        return ToolResult<ScriptableObjectSetFieldResult>.Fail(
                            "Vector3 requires a [x, y, z] float array", ErrorCodes.INVALID_PARAM);
                    prop.vector3Value = new Vector3(
                        arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>());
                    break;
                case SerializedPropertyType.Vector2:
                    var v2 = value as JArray;
                    if (v2 == null || v2.Count < 2)
                        return ToolResult<ScriptableObjectSetFieldResult>.Fail(
                            "Vector2 requires a [x, y] float array", ErrorCodes.INVALID_PARAM);
                    prop.vector2Value = new Vector2(
                        v2[0].Value<float>(), v2[1].Value<float>());
                    break;
                case SerializedPropertyType.Color:
                    var ca = value as JArray;
                    if (ca == null || ca.Count < 4)
                        return ToolResult<ScriptableObjectSetFieldResult>.Fail(
                            "Color requires a [r, g, b, a] float array", ErrorCodes.INVALID_PARAM);
                    prop.colorValue = new Color(
                        ca[0].Value<float>(), ca[1].Value<float>(),
                        ca[2].Value<float>(), ca[3].Value<float>());
                    break;
                default:
                    return ToolResult<ScriptableObjectSetFieldResult>.Fail(
                        $"Unsupported property type: {prop.propertyType}", ErrorCodes.TYPE_MISMATCH);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            // Re-read the value after apply
            so.Update();
            var updated = so.FindProperty(p.FieldName);
            object newValue = GetPropertyValue(updated);

            return ToolResult<ScriptableObjectSetFieldResult>.Ok(new ScriptableObjectSetFieldResult
            {
                AssetPath = p.AssetPath,
                FieldName = p.FieldName,
                NewValue  = newValue
            });
        }

        private static object GetPropertyValue(SerializedProperty prop)
        {
            if (prop == null) return null;
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:   return prop.floatValue;
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.String:  return prop.stringValue;
                case SerializedPropertyType.Vector3:
                    return new float[] { prop.vector3Value.x, prop.vector3Value.y, prop.vector3Value.z };
                case SerializedPropertyType.Vector2:
                    return new float[] { prop.vector2Value.x, prop.vector2Value.y };
                case SerializedPropertyType.Color:
                    return new float[] { prop.colorValue.r, prop.colorValue.g, prop.colorValue.b, prop.colorValue.a };
                default: return prop.type;
            }
        }
    }
}
