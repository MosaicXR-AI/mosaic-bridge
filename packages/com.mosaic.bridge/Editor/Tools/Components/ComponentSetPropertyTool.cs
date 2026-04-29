using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Components
{
    public static class ComponentSetPropertyTool
    {
        [MosaicTool("component/set_property",
                    "Sets a serialized property value on a component attached to a GameObject. " +
                    "IMPORTANT: PropertyName must use Unity's serialized field name with the 'm_' prefix, " +
                    "NOT the public C# property name. Examples: 'm_Mass' not 'mass', 'm_Enabled' not 'enabled'. " +
                    "Value types: Float=[1.5], Int=[2], Bool=[true], String=['text'], " +
                    "Vector2=[x,y], Vector3=[x,y,z], Vector4=[x,y,z,w], Color=[r,g,b,a], " +
                    "Quaternion=[x,y,z,w], ObjectReference='Assets/path.mat'. " +
                    "Use component/get_properties first to discover the exact serialized property names.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ComponentSetPropertyResult> SetProperty(ComponentSetPropertyParams p)
        {
            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<ComponentSetPropertyResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            var type = ResolveType(p.ComponentType);
            if (type == null)
                return ToolResult<ComponentSetPropertyResult>.Fail(
                    $"Component type not found: {p.ComponentType}", ErrorCodes.NOT_FOUND);

            var component = go.GetComponent(type);
            if (component == null)
                return ToolResult<ComponentSetPropertyResult>.Fail(
                    $"Component '{p.ComponentType}' not found on '{p.GameObjectName}'", ErrorCodes.NOT_FOUND);

            var so = new SerializedObject(component);
            var prop = so.FindProperty(p.PropertyName);
            if (prop == null)
                return ToolResult<ComponentSetPropertyResult>.Fail(
                    $"Property '{p.PropertyName}' not found on component '{p.ComponentType}'", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(component, "Mosaic: Set Property");

            var value = p.Value;
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:
                    prop.floatValue = (float)value;
                    break;
                case SerializedPropertyType.Integer:
                    prop.intValue = (int)value;
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = (bool)value;
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Vector2:
                    var arr2 = value as JArray;
                    if (arr2 == null || arr2.Count < 2)
                        return ToolResult<ComponentSetPropertyResult>.Fail(
                            "Vector2 requires a [x, y] float array", ErrorCodes.INVALID_PARAM);
                    prop.vector2Value = new Vector2(arr2[0].Value<float>(), arr2[1].Value<float>());
                    break;
                case SerializedPropertyType.Vector3:
                    var arr = value as JArray;
                    if (arr == null || arr.Count < 3)
                        return ToolResult<ComponentSetPropertyResult>.Fail(
                            "Vector3 requires a [x, y, z] float array", ErrorCodes.INVALID_PARAM);
                    prop.vector3Value = new Vector3(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>());
                    break;
                case SerializedPropertyType.Vector4:
                    var arr4 = value as JArray;
                    if (arr4 == null || arr4.Count < 4)
                        return ToolResult<ComponentSetPropertyResult>.Fail(
                            "Vector4 requires a [x, y, z, w] float array", ErrorCodes.INVALID_PARAM);
                    prop.vector4Value = new Vector4(arr4[0].Value<float>(), arr4[1].Value<float>(), arr4[2].Value<float>(), arr4[3].Value<float>());
                    break;
                case SerializedPropertyType.Color:
                    var arrC = value as JArray;
                    if (arrC == null || arrC.Count < 3)
                        return ToolResult<ComponentSetPropertyResult>.Fail(
                            "Color requires a [r, g, b] or [r, g, b, a] float array", ErrorCodes.INVALID_PARAM);
                    prop.colorValue = new Color(arrC[0].Value<float>(), arrC[1].Value<float>(), arrC[2].Value<float>(),
                        arrC.Count >= 4 ? arrC[3].Value<float>() : 1f);
                    break;
                case SerializedPropertyType.Quaternion:
                    var arrQ = value as JArray;
                    if (arrQ == null || arrQ.Count < 4)
                        return ToolResult<ComponentSetPropertyResult>.Fail(
                            "Quaternion requires a [x, y, z, w] float array", ErrorCodes.INVALID_PARAM);
                    prop.quaternionValue = new Quaternion(arrQ[0].Value<float>(), arrQ[1].Value<float>(), arrQ[2].Value<float>(), arrQ[3].Value<float>());
                    break;
                case SerializedPropertyType.ObjectReference:
                    var refPath = value?.Value<string>();
                    if (!string.IsNullOrEmpty(refPath))
                    {
                        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(refPath);
                        if (asset == null)
                            return ToolResult<ComponentSetPropertyResult>.Fail(
                                $"Asset not found at '{refPath}'", ErrorCodes.NOT_FOUND);
                        prop.objectReferenceValue = asset;
                    }
                    break;
                default:
                    return ToolResult<ComponentSetPropertyResult>.Fail(
                        $"Unsupported property type: {prop.propertyType}. Supported: Float, Integer, Boolean, String, Vector2, Vector3, Vector4, Color, Quaternion, ObjectReference",
                        ErrorCodes.TYPE_MISMATCH);
            }

            so.ApplyModifiedProperties();

            return ToolResult<ComponentSetPropertyResult>.Ok(new ComponentSetPropertyResult
            {
                GameObjectName = go.name,
                ComponentType = type.FullName,
                PropertyName = p.PropertyName,
                NewValue = GetPropertyValue(so.FindProperty(p.PropertyName))
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
                case SerializedPropertyType.Vector2:
                    return new float[] { prop.vector2Value.x, prop.vector2Value.y };
                case SerializedPropertyType.Vector3:
                    return new float[] { prop.vector3Value.x, prop.vector3Value.y, prop.vector3Value.z };
                case SerializedPropertyType.Vector4:
                    var v4 = prop.vector4Value;
                    return new float[] { v4.x, v4.y, v4.z, v4.w };
                case SerializedPropertyType.Color:
                    var col = prop.colorValue;
                    return new float[] { col.r, col.g, col.b, col.a };
                case SerializedPropertyType.Quaternion:
                    var q = prop.quaternionValue;
                    return new float[] { q.x, q.y, q.z, q.w };
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null
                        ? UnityEditor.AssetDatabase.GetAssetPath(prop.objectReferenceValue)
                        : null;
                default: return prop.type;
            }
        }

        private static Type ResolveType(string typeName)
        {
            var t = Type.GetType(typeName);
            if (t != null) return t;
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(x => x.Name == typeName || x.FullName == typeName);
        }
    }
}
