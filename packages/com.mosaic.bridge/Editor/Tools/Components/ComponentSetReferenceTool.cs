using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Components
{
    public static class ComponentSetReferenceTool
    {
        [MosaicTool("component/set_reference",
                    "Sets a serialized property on a component. Supports object-reference fields (e.g. Follow targets, " +
                    "material slots) AND value-type fields (float, int, bool, string/enum, color, vector). " +
                    "PropertyPath accepts dot-notation for nested struct members and automatically tries the Unity " +
                    "'m_' prefix convention, so both 'Lens.FieldOfView' and 'm_Lens.m_FieldOfView' work. " +
                    "Provide TargetObjectPath for object references, or FloatValue/IntValue/BoolValue/StringValue/" +
                    "ColorValue/VectorValue for primitive fields.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ComponentSetReferenceResult> Execute(ComponentSetReferenceParams p)
        {
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.ComponentType))
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    "ComponentType is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.PropertyPath))
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    "PropertyPath is required", ErrorCodes.INVALID_PARAM);

            // Must provide either a target object path or a value type
            bool hasObjectTarget = !string.IsNullOrEmpty(p.TargetObjectPath);
            bool hasValueTarget  = p.FloatValue.HasValue || p.IntValue.HasValue || p.BoolValue.HasValue
                                || p.StringValue != null  || p.ColorValue != null || p.VectorValue != null;
            if (!hasObjectTarget && !hasValueTarget)
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    "Provide TargetObjectPath (for object-reference fields) or one of FloatValue / IntValue / " +
                    "BoolValue / StringValue / ColorValue / VectorValue (for primitive fields).",
                    ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            var type = ResolveType(p.ComponentType);
            if (type == null)
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    $"Component type not found: {p.ComponentType}", ErrorCodes.NOT_FOUND);

            var component = go.GetComponent(type);
            if (component == null)
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    $"Component '{p.ComponentType}' not found on '{p.GameObjectName}'", ErrorCodes.NOT_FOUND);

            var so   = new SerializedObject(component);
            var prop = FindPropertyFuzzy(so, p.PropertyPath);
            if (prop == null)
                return ToolResult<ComponentSetReferenceResult>.Fail(
                    $"Property '{p.PropertyPath}' not found on '{p.ComponentType}'. " +
                    "Tried exact path and 'm_' prefix variants. " +
                    "Use shadergraph/get-properties or a Unity Inspector debug view to find the correct serialized path.",
                    ErrorCodes.NOT_FOUND);

            string assignedValue;

            // ── Object reference ───────────────────────────────────────────────
            if (hasObjectTarget)
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                    return ToolResult<ComponentSetReferenceResult>.Fail(
                        $"Property '{p.PropertyPath}' is of type '{prop.propertyType}', not ObjectReference. " +
                        "Use FloatValue / IntValue / BoolValue / StringValue / ColorValue / VectorValue for this property.",
                        ErrorCodes.INVALID_PARAM);

                var resolvedTarget = ResolveTarget(p.TargetObjectPath, p.TargetType);
                if (resolvedTarget == null)
                    return ToolResult<ComponentSetReferenceResult>.Fail(
                        $"Target object not found: '{p.TargetObjectPath}'", ErrorCodes.NOT_FOUND);

                prop.objectReferenceValue = resolvedTarget;
                assignedValue = resolvedTarget.name;
            }
            // ── Value types ───────────────────────────────────────────────────
            else if (p.FloatValue.HasValue)
            {
                if (prop.propertyType == SerializedPropertyType.Float)
                    prop.floatValue = p.FloatValue.Value;
                else if (prop.propertyType == SerializedPropertyType.Integer)
                    prop.intValue = (int)p.FloatValue.Value;
                else
                    return ToolResult<ComponentSetReferenceResult>.Fail(
                        $"FloatValue provided but property '{p.PropertyPath}' is '{prop.propertyType}'.",
                        ErrorCodes.INVALID_PARAM);
                assignedValue = p.FloatValue.Value.ToString("G");
            }
            else if (p.IntValue.HasValue)
            {
                if (prop.propertyType == SerializedPropertyType.Integer)
                    prop.intValue = p.IntValue.Value;
                else if (prop.propertyType == SerializedPropertyType.Enum)
                    prop.enumValueIndex = p.IntValue.Value;
                else if (prop.propertyType == SerializedPropertyType.Float)
                    prop.floatValue = p.IntValue.Value;
                else
                    return ToolResult<ComponentSetReferenceResult>.Fail(
                        $"IntValue provided but property '{p.PropertyPath}' is '{prop.propertyType}'.",
                        ErrorCodes.INVALID_PARAM);
                assignedValue = p.IntValue.Value.ToString();
            }
            else if (p.BoolValue.HasValue)
            {
                if (prop.propertyType != SerializedPropertyType.Boolean)
                    return ToolResult<ComponentSetReferenceResult>.Fail(
                        $"BoolValue provided but property '{p.PropertyPath}' is '{prop.propertyType}'.",
                        ErrorCodes.INVALID_PARAM);
                prop.boolValue = p.BoolValue.Value;
                assignedValue = p.BoolValue.Value.ToString();
            }
            else if (p.StringValue != null)
            {
                if (prop.propertyType == SerializedPropertyType.String)
                {
                    prop.stringValue = p.StringValue;
                }
                else if (prop.propertyType == SerializedPropertyType.Enum)
                {
                    // Try to match enum name (case-insensitive)
                    int idx = Array.FindIndex(
                        prop.enumNames,
                        n => string.Equals(n, p.StringValue, StringComparison.OrdinalIgnoreCase));
                    if (idx < 0)
                        return ToolResult<ComponentSetReferenceResult>.Fail(
                            $"Enum value '{p.StringValue}' not valid for '{p.PropertyPath}'. " +
                            $"Valid names: {string.Join(", ", prop.enumNames)}",
                            ErrorCodes.INVALID_PARAM);
                    prop.enumValueIndex = idx;
                }
                else
                {
                    return ToolResult<ComponentSetReferenceResult>.Fail(
                        $"StringValue provided but property '{p.PropertyPath}' is '{prop.propertyType}'. " +
                        "Use FloatValue/IntValue/BoolValue/ColorValue/VectorValue for this type.",
                        ErrorCodes.INVALID_PARAM);
                }
                assignedValue = p.StringValue;
            }
            else if (p.ColorValue != null)
            {
                if (prop.propertyType != SerializedPropertyType.Color)
                    return ToolResult<ComponentSetReferenceResult>.Fail(
                        $"ColorValue provided but property '{p.PropertyPath}' is '{prop.propertyType}'.",
                        ErrorCodes.INVALID_PARAM);
                float r = p.ColorValue.Length > 0 ? p.ColorValue[0] : 0f;
                float g = p.ColorValue.Length > 1 ? p.ColorValue[1] : 0f;
                float b = p.ColorValue.Length > 2 ? p.ColorValue[2] : 0f;
                float a = p.ColorValue.Length > 3 ? p.ColorValue[3] : 1f;
                prop.colorValue = new Color(r, g, b, a);
                assignedValue = $"({r},{g},{b},{a})";
            }
            else // VectorValue
            {
                if (prop.propertyType == SerializedPropertyType.Vector2)
                {
                    prop.vector2Value = new Vector2(
                        p.VectorValue.Length > 0 ? p.VectorValue[0] : 0f,
                        p.VectorValue.Length > 1 ? p.VectorValue[1] : 0f);
                }
                else if (prop.propertyType == SerializedPropertyType.Vector3)
                {
                    prop.vector3Value = new Vector3(
                        p.VectorValue.Length > 0 ? p.VectorValue[0] : 0f,
                        p.VectorValue.Length > 1 ? p.VectorValue[1] : 0f,
                        p.VectorValue.Length > 2 ? p.VectorValue[2] : 0f);
                }
                else if (prop.propertyType == SerializedPropertyType.Vector4)
                {
                    prop.vector4Value = new Vector4(
                        p.VectorValue.Length > 0 ? p.VectorValue[0] : 0f,
                        p.VectorValue.Length > 1 ? p.VectorValue[1] : 0f,
                        p.VectorValue.Length > 2 ? p.VectorValue[2] : 0f,
                        p.VectorValue.Length > 3 ? p.VectorValue[3] : 0f);
                }
                else
                {
                    return ToolResult<ComponentSetReferenceResult>.Fail(
                        $"VectorValue provided but property '{p.PropertyPath}' is '{prop.propertyType}'.",
                        ErrorCodes.INVALID_PARAM);
                }
                assignedValue = "[" + string.Join(",", p.VectorValue) + "]";
            }

            so.ApplyModifiedProperties();

            return ToolResult<ComponentSetReferenceResult>.Ok(new ComponentSetReferenceResult
            {
                GameObjectName  = go.name,
                ComponentType   = type.FullName,
                PropertyPath    = prop.propertyPath,
                PropertyType    = prop.propertyType.ToString(),
                ResolvedTarget  = assignedValue
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Finds a serialized property supporting dot-notation nested paths and
        /// automatic 'm_' prefix fallback for each path segment.
        /// Resolves: "Target.TrackingTarget" → tries "Target", "m_Target", then
        /// FindPropertyRelative("TrackingTarget"), FindPropertyRelative("m_TrackingTarget").
        /// </summary>
        private static SerializedProperty FindPropertyFuzzy(SerializedObject so, string path)
        {
            // 1. Exact match — Unity's FindProperty already handles some nested paths
            var direct = so.FindProperty(path);
            if (direct != null) return direct;

            // 2. Segment-by-segment traversal with m_ fallback
            var segments = path.Split('.');
            SerializedProperty current = null;

            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];
                string mSeg = seg.StartsWith("m_", StringComparison.Ordinal) ? seg : "m_" + seg;

                if (i == 0)
                {
                    current = so.FindProperty(seg) ?? so.FindProperty(mSeg);
                }
                else
                {
                    if (current == null) return null;
                    current = current.FindPropertyRelative(seg) ?? current.FindPropertyRelative(mSeg);
                }

                if (current == null) return null;
            }

            return current;
        }

        private static UnityEngine.Object ResolveTarget(string targetPath, string targetType)
        {
            bool tryAsset      = string.IsNullOrEmpty(targetType) || targetType == "Asset";
            bool tryGameObject = string.IsNullOrEmpty(targetType) || targetType == "GameObject";
            UnityEngine.Object result = null;

            if (tryAsset)
            {
                result = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath);
                if (result == null && targetPath.StartsWith("Assets/"))
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    result = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath);
                }
                if (result == null && targetPath.StartsWith("Assets/"))
                {
                    var all = AssetDatabase.LoadAllAssetsAtPath(targetPath);
                    if (all != null && all.Length > 0) result = all[0];
                }
            }

            if (result == null && tryGameObject)
                result = GameObject.Find(targetPath);

            return result;
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
