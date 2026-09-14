using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.SerializedValues;

namespace Mosaic.Bridge.Tools.Components
{
    public static class ComponentSetPropertyTool
    {
        [MosaicTool("component/set_property",
                    "Sets a serialized property value on a component attached to a GameObject. " +
                    "IMPORTANT: PropertyName must use Unity's serialized field name, which is usually but NOT " +
                    "always 'm_'-prefixed — Rigidbody 'm_Mass' not 'mass', but AudioSource fields like " +
                    "'OutputAudioMixerGroup'/'Loop'/'Mute'/'Priority' and Camera fields like 'orthographic'/" +
                    "'orthographic size' have no 'm_' at all. When unsure, check the component's serialized YAML " +
                    "or a scene file rather than assuming the prefix. " +
                    "Value types: Float=[1.5], Int=[2], Bool=[true], String=['text'], " +
                    "Vector2=[x,y], Vector3=[x,y,z], Vector4=[x,y,z,w], Color=[r,g,b,a], " +
                    "Quaternion=[x,y,z,w], ObjectReference='Assets/path.mat' (or 'Assets/sheet.png#SubAsset'), " +
                    "Enum='MemberName' or an index int, LayerMask=an int bitmask or ['LayerName',...]/[index,...], " +
                    "Gradient={colorKeys:[{color:[r,g,b,a],time}],alphaKeys:[{alpha,time}]}, " +
                    "AnimationCurve=[{time,value,inTangent?,outTangent?},...], " +
                    "struct-valued fields (ColorBlock, RectOffset, JointLimits, ...)={fieldName: value, ...} " +
                    "assigned atomically, and array-valued fields take a JSON array of per-element values. " +
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

            if (!SerializedPropertyCodec.TrySet(prop, p.Value, ResolveType, out var setError))
                return ToolResult<ComponentSetPropertyResult>.Fail(setError, ErrorCodes.TYPE_MISMATCH);

            so.ApplyModifiedProperties();

            return ToolResult<ComponentSetPropertyResult>.Ok(new ComponentSetPropertyResult
            {
                GameObjectName = go.name,
                ComponentType = type.FullName,
                PropertyName = p.PropertyName,
                NewValue = GetPropertyValue(so.FindProperty(p.PropertyName))
            });
        }

        private static object GetPropertyValue(SerializedProperty prop) =>
            prop == null ? null : SerializedPropertyCodec.Get(prop);

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
