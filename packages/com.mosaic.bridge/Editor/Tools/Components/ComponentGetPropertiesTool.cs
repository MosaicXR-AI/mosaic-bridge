using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Components
{
    public static class ComponentGetPropertiesTool
    {
        [MosaicTool("component/get_properties",
                    "Returns the serialized properties of a component attached to a GameObject",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<ComponentGetPropertiesResult> GetProperties(ComponentGetPropertiesParams p)
        {
            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<ComponentGetPropertiesResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            var type = ResolveType(p.ComponentType);
            if (type == null)
                return ToolResult<ComponentGetPropertiesResult>.Fail(
                    $"Component type not found: {p.ComponentType}", ErrorCodes.NOT_FOUND);

            var component = go.GetComponent(type);
            if (component == null)
                return ToolResult<ComponentGetPropertiesResult>.Fail(
                    $"Component '{p.ComponentType}' not found on '{p.GameObjectName}'", ErrorCodes.NOT_FOUND);

            var so = new SerializedObject(component);
            var prop = so.GetIterator();
            var properties = new Dictionary<string, object>();
            if (prop.NextVisible(true))
            {
                do
                {
                    properties[prop.name] = GetPropertyValue(prop);
                } while (prop.NextVisible(false));
            }

            return ToolResult<ComponentGetPropertiesResult>.Ok(new ComponentGetPropertiesResult
            {
                GameObjectName = go.name,
                ComponentType = type.FullName,
                Properties = properties
            });
        }

        private static object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:
                    return prop.floatValue;
                case SerializedPropertyType.Integer:
                    return prop.intValue;
                case SerializedPropertyType.Boolean:
                    return prop.boolValue;
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Vector3:
                    return new float[] { prop.vector3Value.x, prop.vector3Value.y, prop.vector3Value.z };
                case SerializedPropertyType.Color:
                    return new float[] { prop.colorValue.r, prop.colorValue.g, prop.colorValue.b, prop.colorValue.a };
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : (object)null;
                default:
                    return prop.type;
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
