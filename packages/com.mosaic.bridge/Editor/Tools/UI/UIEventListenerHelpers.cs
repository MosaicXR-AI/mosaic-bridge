using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEditor.Events;
using Mosaic.Bridge.Core.Assets;

namespace Mosaic.Bridge.Tools.UI
{
    /// <summary>Shared machinery for ui/add_listener and ui/remove_listener — resolving a
    /// UnityEventBase off any component by name, and building the bound UnityAction delegate
    /// UnityEventTools' Add*PersistentListener overloads need. Generic by design: works for
    /// Button.onClick, Toggle.onValueChanged, a custom MonoBehaviour's own UnityEvent field, or
    /// (per O4 §4.6) timeline/signal add-receiver's identical need.</summary>
    internal static class UIEventListenerHelpers
    {
        /// <summary>Finds a UnityEventBase-typed property or field named EventName on the given
        /// component instance.</summary>
        internal static bool TryResolveEvent(Component component, string eventName, out UnityEventBase evt, out string error)
        {
            evt = null;
            var type = component.GetType();
            var prop = type.GetProperty(eventName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && typeof(UnityEventBase).IsAssignableFrom(prop.PropertyType))
            {
                evt = prop.GetValue(component) as UnityEventBase;
            }
            else
            {
                var field = type.GetField(eventName, BindingFlags.Public | BindingFlags.Instance);
                if (field != null && typeof(UnityEventBase).IsAssignableFrom(field.FieldType))
                    evt = field.GetValue(component) as UnityEventBase;
            }
            if (evt == null)
            {
                error = $"'{eventName}' is not a public UnityEvent property or field on {type.Name}.";
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>Resolves the target method and, if it takes exactly one argument, which of
        /// float/int/bool/string/Object it is. "method public, 0–1 arg" per O4 §4.6.</summary>
        internal static bool TryResolveMethod(Component targetComponent, string methodName, out MethodInfo method, out Type argType, out string error)
        {
            method = null;
            argType = null;
            var candidates = System.Array.FindAll(
                targetComponent.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance),
                m => m.Name == methodName && m.GetParameters().Length <= 1);
            if (candidates.Length == 0)
            {
                error = $"No public method '{methodName}' with 0 or 1 parameters found on {targetComponent.GetType().Name}.";
                return false;
            }
            method = candidates[0];
            var parameters = method.GetParameters();
            if (parameters.Length == 0) { error = null; return true; }

            argType = parameters[0].ParameterType;
            if (argType != typeof(float) && argType != typeof(int) && argType != typeof(bool) &&
                argType != typeof(string) && !typeof(UnityEngine.Object).IsAssignableFrom(argType))
            {
                error = $"Method '{methodName}' takes a {argType.Name} parameter — persistent listeners only " +
                         "support float, int, bool, string, or an Object-derived type.";
                method = null;
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>Adds the persistent listener with whichever Add*PersistentListener overload
        /// matches the resolved method's arity/type, using the caller-supplied argument value for
        /// a 1-arg method. Returns the new listener's index.</summary>
        internal static bool TryAddPersistentListener(
            UnityEventBase evt, Component targetComponent, MethodInfo method, Type argType,
            float? floatArg, int? intArg, bool? boolArg, string stringArg, string objectArg,
            UnityEventCallState callState, out int index, out string error)
        {
            index = -1;
            error = null;
            try
            {
                if (argType == null)
                {
                    var action = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), targetComponent, method);
                    UnityEventTools.AddVoidPersistentListener(evt, action);
                }
                else if (argType == typeof(float))
                {
                    if (!floatArg.HasValue) { error = "FloatArg is required for this method"; return false; }
                    var action = (UnityAction<float>)Delegate.CreateDelegate(typeof(UnityAction<float>), targetComponent, method);
                    UnityEventTools.AddFloatPersistentListener(evt, action, floatArg.Value);
                }
                else if (argType == typeof(int))
                {
                    if (!intArg.HasValue) { error = "IntArg is required for this method"; return false; }
                    var action = (UnityAction<int>)Delegate.CreateDelegate(typeof(UnityAction<int>), targetComponent, method);
                    UnityEventTools.AddIntPersistentListener(evt, action, intArg.Value);
                }
                else if (argType == typeof(bool))
                {
                    if (!boolArg.HasValue) { error = "BoolArg is required for this method"; return false; }
                    var action = (UnityAction<bool>)Delegate.CreateDelegate(typeof(UnityAction<bool>), targetComponent, method);
                    UnityEventTools.AddBoolPersistentListener(evt, action, boolArg.Value);
                }
                else if (argType == typeof(string))
                {
                    if (stringArg == null) { error = "StringArg is required for this method"; return false; }
                    var action = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), targetComponent, method);
                    UnityEventTools.AddStringPersistentListener(evt, action, stringArg);
                }
                else
                {
                    UnityEngine.Object resolvedObject = null;
                    if (!string.IsNullOrEmpty(objectArg) &&
                        !ObjectReferenceResolver.TryResolveAsset(objectArg, argType, out resolvedObject, out error))
                        return false;
                    var delegateType = typeof(UnityAction<>).MakeGenericType(argType);
                    var action = Delegate.CreateDelegate(delegateType, targetComponent, method);
                    var addObjectMethod = typeof(UnityEventTools).GetMethod("AddObjectPersistentListener")
                        .MakeGenericMethod(argType);
                    addObjectMethod.Invoke(null, new object[] { evt, action, resolvedObject });
                }

                index = evt.GetPersistentEventCount() - 1;
                evt.SetPersistentListenerState(index, callState);
                return true;
            }
            catch (Exception e)
            {
                error = $"Failed to add listener: {e.Message}";
                return false;
            }
        }

        internal static bool TryParseCallState(string value, out UnityEventCallState result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "runtimeonly":      result = UnityEventCallState.RuntimeOnly;      return true;
                case "editorandruntime": result = UnityEventCallState.EditorAndRuntime; return true;
                case "off":              result = UnityEventCallState.Off;              return true;
                default:                 result = UnityEventCallState.RuntimeOnly;      return false;
            }
        }
    }
}
