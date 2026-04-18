using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ScriptableObjects
{
    public static class ScriptableObjectInfoTool
    {
        [MosaicTool("scriptableobject/info",
                    "Queries a ScriptableObject asset and returns its serialized fields",
                    isReadOnly: true)]
        public static ToolResult<ScriptableObjectInfoResult> Execute(ScriptableObjectInfoParams p)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(p.AssetPath);
            if (asset == null)
                return ToolResult<ScriptableObjectInfoResult>.Fail(
                    $"ScriptableObject not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            var so = new SerializedObject(asset);
            var fields = new List<ScriptableObjectFieldInfo>();

            var iterator = so.GetIterator();
            // Enter the root level
            if (iterator.NextVisible(true))
            {
                do
                {
                    // Skip the built-in m_Script field
                    if (iterator.name == "m_Script") continue;

                    fields.Add(new ScriptableObjectFieldInfo
                    {
                        Name  = iterator.name,
                        Type  = iterator.type,
                        Value = GetPropertyValue(iterator)
                    });
                }
                while (iterator.NextVisible(false));
            }

            var guid = AssetDatabase.AssetPathToGUID(p.AssetPath);

            return ToolResult<ScriptableObjectInfoResult>.Ok(new ScriptableObjectInfoResult
            {
                AssetPath = p.AssetPath,
                TypeName  = asset.GetType().FullName,
                Guid      = guid,
                Fields    = fields
            });
        }

        private static object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:          return prop.floatValue;
                case SerializedPropertyType.Integer:        return prop.intValue;
                case SerializedPropertyType.Boolean:        return prop.boolValue;
                case SerializedPropertyType.String:         return prop.stringValue;
                case SerializedPropertyType.Color:
                    var c = prop.colorValue;
                    return new float[] { c.r, c.g, c.b, c.a };
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return new float[] { v2.x, v2.y };
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return new float[] { v3.x, v3.y, v3.z };
                case SerializedPropertyType.Vector4:
                    var v4 = prop.vector4Value;
                    return new float[] { v4.x, v4.y, v4.z, v4.w };
                case SerializedPropertyType.Enum:           return prop.enumDisplayNames[prop.enumValueIndex];
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null
                        ? prop.objectReferenceValue.name
                        : null;
                default:
                    return prop.type;
            }
        }
    }
}
