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
                case SerializedPropertyType.Vector3:
                    var arr = value as JArray;
                    if (arr == null || arr.Count < 3)
                        return ToolResult<ComponentSetPropertyResult>.Fail(
                            "Vector3 requires a [x, y, z] float array", ErrorCodes.INVALID_PARAM);
                    prop.vector3Value = new Vector3(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>());
                    break;
                default:
                    return ToolResult<ComponentSetPropertyResult>.Fail(
                        $"Unsupported property type: {prop.propertyType}", ErrorCodes.TYPE_MISMATCH);
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
                case SerializedPropertyType.Vector3:
                    return new float[] { prop.vector3Value.x, prop.vector3Value.y, prop.vector3Value.z };
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
