using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Reflection
{
    public static class ReflectionCallMethodTool
    {
        [MosaicTool("reflection/call-method",
                    "Invokes a method via reflection. Supports static methods and instance methods " +
                    "(via InstancePath to a GameObject). Blocked methods are rejected for security. " +
                    "IMPORTANT: Struct/vector args must be passed as JSON objects, NOT arrays. " +
                    "Use {\"x\":1,\"y\":2,\"z\":3} for Vector3, NOT [1,2,3]. " +
                    "Use {\"r\":1,\"g\":0,\"b\":0,\"a\":1} for Color.",
                    isReadOnly: false)]
        public static ToolResult<ReflectionCallMethodResult> Execute(ReflectionCallMethodParams p)
        {
            if (string.IsNullOrWhiteSpace(p.TypeName))
            {
                return ToolResult<ReflectionCallMethodResult>.Fail(
                    "TypeName is required.", ErrorCodes.INVALID_PARAM);
            }

            if (string.IsNullOrWhiteSpace(p.MethodName))
            {
                return ToolResult<ReflectionCallMethodResult>.Fail(
                    "MethodName is required.", ErrorCodes.INVALID_PARAM);
            }

            Type type = ReflectionFindMethodTool.ResolveType(p.TypeName);
            if (type == null)
            {
                return ToolResult<ReflectionCallMethodResult>.Fail(
                    $"Type '{p.TypeName}' not found in loaded assemblies.",
                    ErrorCodes.NOT_FOUND);
            }

            bool isInstanceCall = !string.IsNullOrWhiteSpace(p.InstancePath);

            try
            {
                // Resolve binding flags
                BindingFlags flags = BindingFlags.Public | (isInstanceCall ? BindingFlags.Instance : BindingFlags.Static);
                if (p.AllowPrivate)
                    flags |= BindingFlags.NonPublic;

                // Find candidate methods
                MethodInfo[] candidates = type.GetMethods(flags)
                    .Where(m => m.Name == p.MethodName)
                    .ToArray();

                if (candidates.Length == 0)
                {
                    return ToolResult<ReflectionCallMethodResult>.Fail(
                        $"No {(isInstanceCall ? "instance" : "static")} method '{p.MethodName}' found on type '{type.FullName}'." +
                        (!p.AllowPrivate ? " Set AllowPrivate=true to include non-public methods." : ""),
                        ErrorCodes.NOT_FOUND);
                }

                // Determine argument count
                int argCount = p.Arguments?.Count ?? 0;

                // Find overload matching argument count
                MethodInfo method = candidates.FirstOrDefault(m => m.GetParameters().Length == argCount);
                if (method == null)
                {
                    var counts = string.Join(", ", candidates.Select(m => m.GetParameters().Length.ToString()));
                    return ToolResult<ReflectionCallMethodResult>.Fail(
                        $"No overload of '{type.FullName}.{p.MethodName}' accepts {argCount} argument(s). " +
                        $"Available overloads accept: [{counts}] parameters.",
                        ErrorCodes.INVALID_PARAM);
                }

                // Security check
                string blockReason = ReflectionBlocklist.GetBlockReason(method);
                if (blockReason != null)
                {
                    return ToolResult<ReflectionCallMethodResult>.Fail(
                        blockReason, ErrorCodes.NOT_PERMITTED);
                }

                // Convert arguments
                ParameterInfo[] paramInfos = method.GetParameters();
                object[] convertedArgs = new object[argCount];
                for (int i = 0; i < argCount; i++)
                {
                    try
                    {
                        convertedArgs[i] = ConvertArgument(p.Arguments[i], paramInfos[i].ParameterType);
                    }
                    catch (Exception ex)
                    {
                        return ToolResult<ReflectionCallMethodResult>.Fail(
                            $"Failed to convert argument {i} to {paramInfos[i].ParameterType.Name}: {ex.Message}",
                            ErrorCodes.TYPE_MISMATCH);
                    }
                }

                // Resolve instance target if needed
                object target = null;
                if (isInstanceCall)
                {
                    GameObject go = GameObject.Find(p.InstancePath);
                    if (go == null)
                    {
                        return ToolResult<ReflectionCallMethodResult>.Fail(
                            $"GameObject not found at path '{p.InstancePath}'.",
                            ErrorCodes.NOT_FOUND);
                    }

                    target = go.GetComponent(type);
                    if (target == null)
                    {
                        return ToolResult<ReflectionCallMethodResult>.Fail(
                            $"No component of type '{type.FullName}' found on GameObject '{p.InstancePath}'.",
                            ErrorCodes.NOT_FOUND);
                    }
                }

                // Invoke
                var sw = Stopwatch.StartNew();
                object returnVal = method.Invoke(target, convertedArgs);
                sw.Stop();

                // Build method signature string
                string sig = BuildSignature(method);

                // Safe-serialize the return value
                object safeResult = SafeSerialize(returnVal);
                string returnTypeName = method.ReturnType == typeof(void) ? "void" : (method.ReturnType.FullName ?? method.ReturnType.Name);

                return ToolResult<ReflectionCallMethodResult>.Ok(new ReflectionCallMethodResult
                {
                    MethodSignature = sig,
                    ReturnValue = safeResult,
                    ReturnType = returnTypeName,
                    ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
                });
            }
            catch (TargetInvocationException ex)
            {
                return ToolResult<ReflectionCallMethodResult>.Fail(
                    $"Method threw {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}",
                    ErrorCodes.INTERNAL_ERROR);
            }
            catch (Exception ex)
            {
                return ToolResult<ReflectionCallMethodResult>.Fail(
                    $"Invocation failed: {ex.GetType().Name}: {ex.Message}",
                    ErrorCodes.INTERNAL_ERROR);
            }
        }

        // ── Argument conversion ─────────────────────────────────────────────────

        internal static object ConvertArgument(JToken token, Type targetType)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            // Let Newtonsoft handle the conversion via ToObject
            return token.ToObject(targetType);
        }

        // ── Safe serialization (mirrors EditorExecuteCodeTool pattern) ──────────

        internal static object SafeSerialize(object value)
        {
            if (value == null) return null;

            // Unity objects: return summary to prevent serialization explosions
            if (value is UnityEngine.Object unityObj)
            {
                return new { InstanceId = unityObj.GetInstanceID(), Name = unityObj.name, Type = unityObj.GetType().Name };
            }

            if (value is Vector3 v3)
                return new float[] { v3.x, v3.y, v3.z };

            if (value is Vector2 v2)
                return new float[] { v2.x, v2.y };

            if (value is Vector4 v4)
                return new float[] { v4.x, v4.y, v4.z, v4.w };

            if (value is Quaternion q)
                return new float[] { q.x, q.y, q.z, q.w };

            if (value is Color col)
                return new float[] { col.r, col.g, col.b, col.a };

            if (value is Color32 c32)
                return new int[] { c32.r, c32.g, c32.b, c32.a };

            // Primitives and strings pass through
            Type t = value.GetType();
            if (t.IsPrimitive || t == typeof(string) || t == typeof(decimal))
                return value;

            // Enums: return string representation
            if (t.IsEnum)
                return value.ToString();

            // Collections: safe-serialize each element
            if (value is IEnumerable enumerable && !(value is string))
            {
                var list = new List<object>();
                foreach (object item in enumerable)
                {
                    list.Add(SafeSerialize(item));
                }
                return list;
            }

            // Fallback: return ToString for unknown complex types
            return value.ToString();
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        internal static string BuildSignature(MethodInfo method)
        {
            var paramStr = string.Join(", ",
                method.GetParameters().Select(pi =>
                    $"{pi.ParameterType.Name} {pi.Name}"));

            string returnType = method.ReturnType == typeof(void) ? "void" : method.ReturnType.Name;
            string staticMod = method.IsStatic ? "static " : "";

            return $"{staticMod}{returnType} {method.DeclaringType?.FullName}.{method.Name}({paramStr})";
        }
    }
}
