using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.EditorOps
{
    public static class EditorExecuteCodeTool
    {
        // ── Security blocklist ──────────────────────────────────────────────────

        private static readonly HashSet<string> BlockedTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Diagnostics.Process",
            "System.Diagnostics.ProcessStartInfo",
            "System.Environment",
            "System.AppDomain",
        };

        private static readonly HashSet<string> BlockedMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.IO.File.Delete",
            "System.IO.File.WriteAllText",
            "System.IO.File.WriteAllBytes",
            "System.IO.File.Move",
            "System.IO.File.Copy",
            "System.IO.Directory.Delete",
            "System.IO.Directory.Move",
            "System.Reflection.Assembly.Load",
            "System.Reflection.Assembly.LoadFrom",
            "System.Reflection.Assembly.LoadFile",
            "System.Reflection.Assembly.LoadWithPartialName",
        };

        // ── Assemblies to search ────────────────────────────────────────────────

        private static readonly string[] AssemblyPrefixes = new[]
        {
            "UnityEngine",
            "UnityEditor",
            "mscorlib",
            "System",
            "netstandard",
        };

        // ── Tool entry point ────────────────────────────────────────────────────

        [MosaicTool("editor/execute-code",
                    "Evaluates a single static C# expression via reflection (property read or method call). " +
                    "Use for reading editor state, not for creating scene content. " +
                    "⛔ DO NOT use to create GameObjects, meshes, materials, or any scene content — " +
                    "   use the dedicated MCP tools (scene/create-object, probuilder/create, material/create, etc.). " +
                    "IMPORTANT: ONE expression only — for multi-statement blocks use editor/run-block. " +
                    "Examples: 'UnityEditor.EditorApplication.isPlaying', 'UnityEngine.Time.time', " +
                    "'UnityEditor.Selection.activeGameObject.name'.",
                    isReadOnly: false)]
        public static ToolResult<EditorExecuteCodeResult> Execute(EditorExecuteCodeParams p)
        {
            string code = p.Code.Trim();

            // Reject multi-statement input early with actionable guidance.
            // A newline or semicolon inside the expression (outside of strings) signals
            // a block — redirect to script/create + editor/run-menu-item instead.
            if (ContainsMultipleStatements(code))
                return ToolResult<EditorExecuteCodeResult>.Fail(
                    "editor/execute-code only accepts a single expression (e.g. 'UnityEditor.Selection.activeGameObject.name'). " +
                    "For multi-line blocks: create a temporary script with script/create, " +
                    "then trigger it via editor/run-menu-item.",
                    ErrorCodes.INVALID_PARAM);

            // Parse expression into type path + member + optional args
            var parsed = ParseExpression(code);
            if (parsed.Error != null)
            {
                return ToolResult<EditorExecuteCodeResult>.Fail(
                    parsed.Error, ErrorCodes.INVALID_PARAM);
            }

            // Security check
            string securityError = CheckSecurity(parsed.FullTypeName, parsed.MemberName);
            if (securityError != null)
            {
                return ToolResult<EditorExecuteCodeResult>.Fail(
                    securityError, ErrorCodes.NOT_PERMITTED);
            }

            // Resolve the type
            Type type = ResolveType(parsed.FullTypeName);
            if (type == null)
            {
                return ToolResult<EditorExecuteCodeResult>.Fail(
                    $"Type '{parsed.FullTypeName}' not found. Searched UnityEngine, UnityEditor, System assemblies.",
                    ErrorCodes.NOT_FOUND);
            }

            // Security check on resolved type's full name (in case of aliases)
            securityError = CheckSecurity(type.FullName, parsed.MemberName);
            if (securityError != null)
            {
                return ToolResult<EditorExecuteCodeResult>.Fail(
                    securityError, ErrorCodes.NOT_PERMITTED);
            }

            try
            {
                object result;
                Type resultType;

                if (parsed.IsMethodCall)
                {
                    var invokeResult = InvokeMethod(type, parsed.MemberName, parsed.Arguments);
                    if (invokeResult.Error != null)
                    {
                        return ToolResult<EditorExecuteCodeResult>.Fail(
                            invokeResult.Error, ErrorCodes.INTERNAL_ERROR);
                    }
                    result = invokeResult.Value;
                    resultType = invokeResult.ReturnType;
                }
                else
                {
                    var accessResult = AccessProperty(type, parsed.MemberName);
                    if (accessResult.Error != null)
                    {
                        return ToolResult<EditorExecuteCodeResult>.Fail(
                            accessResult.Error, ErrorCodes.NOT_FOUND);
                    }
                    result = accessResult.Value;
                    resultType = accessResult.ReturnType;
                }

                // Prevent serialization errors for Unity objects (Vector3.normalized self-reference, etc.)
                object safeResult = result;
                if (result is UnityEngine.Object unityObj && !(result is System.Type))
                {
                    // Return a safe summary instead of the raw object
                    safeResult = new { InstanceId = unityObj.GetInstanceID(), Name = unityObj.name, Type = unityObj.GetType().Name };
                }
                else if (result is Vector3 v3)
                {
                    safeResult = new float[] { v3.x, v3.y, v3.z };
                }
                else if (result is Vector2 v2)
                {
                    safeResult = new float[] { v2.x, v2.y };
                }
                else if (result is Quaternion q)
                {
                    safeResult = new float[] { q.x, q.y, q.z, q.w };
                }
                else if (result is Color col)
                {
                    safeResult = new float[] { col.r, col.g, col.b, col.a };
                }

                return ToolResult<EditorExecuteCodeResult>.Ok(new EditorExecuteCodeResult
                {
                    Expression = code,
                    ResultType = resultType?.FullName ?? "void",
                    ResultValue = safeResult
                });
            }
            catch (TargetInvocationException ex)
            {
                return ToolResult<EditorExecuteCodeResult>.Fail(
                    $"Execution threw {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}",
                    ErrorCodes.INTERNAL_ERROR);
            }
            catch (Exception ex)
            {
                return ToolResult<EditorExecuteCodeResult>.Fail(
                    $"Execution failed: {ex.GetType().Name}: {ex.Message}",
                    ErrorCodes.INTERNAL_ERROR);
            }
        }

        // ── Multi-statement detection ───────────────────────────────────────────

        private static bool ContainsMultipleStatements(string code)
        {
            bool inString = false;
            char prev = '\0';
            int parenDepth = 0;

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '"' && prev != '\\') inString = !inString;
                if (!inString)
                {
                    if (c == '(') parenDepth++;
                    else if (c == ')') parenDepth--;
                    else if (c == '\n' || c == '\r') return true;
                    else if (c == ';' && parenDepth == 0) return true;
                    else if (c == '{' || c == '}') return true;
                }
                prev = c;
            }
            return false;
        }

        // ── Expression parsing ──────────────────────────────────────────────────

        internal struct ParsedExpression
        {
            public string FullTypeName;
            public string MemberName;
            public bool IsMethodCall;
            public string[] Arguments;
            public string Error;
        }

        internal static ParsedExpression ParseExpression(string code)
        {
            var result = new ParsedExpression();

            // Check for method call: ends with )
            int parenOpen = code.IndexOf('(');
            bool isMethod = parenOpen >= 0 && code.EndsWith(")");

            string memberPath;
            if (isMethod)
            {
                memberPath = code.Substring(0, parenOpen);
                string argString = code.Substring(parenOpen + 1, code.Length - parenOpen - 2).Trim();
                result.IsMethodCall = true;
                result.Arguments = string.IsNullOrEmpty(argString)
                    ? Array.Empty<string>()
                    : SplitArguments(argString);
            }
            else
            {
                memberPath = code;
                result.IsMethodCall = false;
                result.Arguments = Array.Empty<string>();
            }

            // Split "Namespace.Type.Member" — member is last segment
            int lastDot = memberPath.LastIndexOf('.');
            if (lastDot <= 0 || lastDot >= memberPath.Length - 1)
            {
                result.Error = $"Cannot parse '{code}'. Expected format: TypeName.MemberName or TypeName.Method(args)";
                return result;
            }

            result.FullTypeName = memberPath.Substring(0, lastDot);
            result.MemberName = memberPath.Substring(lastDot + 1);
            return result;
        }

        internal static string[] SplitArguments(string argString)
        {
            var args = new List<string>();
            int depth = 0;
            int start = 0;
            bool inString = false;

            for (int i = 0; i < argString.Length; i++)
            {
                char c = argString[i];
                if (c == '"' && (i == 0 || argString[i - 1] != '\\'))
                {
                    inString = !inString;
                }
                else if (!inString)
                {
                    if (c == '(' || c == '[') depth++;
                    else if (c == ')' || c == ']') depth--;
                    else if (c == ',' && depth == 0)
                    {
                        args.Add(argString.Substring(start, i - start).Trim());
                        start = i + 1;
                    }
                }
            }

            args.Add(argString.Substring(start).Trim());
            return args.ToArray();
        }

        // ── Security ────────────────────────────────────────────────────────────

        internal static string CheckSecurity(string typeName, string memberName)
        {
            if (typeName == null) return null;

            if (BlockedTypes.Contains(typeName))
                return $"Access to type '{typeName}' is blocked for security reasons.";

            string fullMember = $"{typeName}.{memberName}";
            if (BlockedMembers.Contains(fullMember))
                return $"Access to '{fullMember}' is blocked for security reasons.";

            return null;
        }

        // ── Type resolution ─────────────────────────────────────────────────────

        internal static Type ResolveType(string typeName)
        {
            // Try direct resolution first
            Type t = Type.GetType(typeName);
            if (t != null) return t;

            // Search loaded assemblies whose name matches known prefixes
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string asmName = asm.GetName().Name;
                bool match = false;
                for (int i = 0; i < AssemblyPrefixes.Length; i++)
                {
                    if (asmName.StartsWith(AssemblyPrefixes[i], StringComparison.Ordinal))
                    {
                        match = true;
                        break;
                    }
                }
                if (!match) continue;

                t = asm.GetType(typeName);
                if (t != null) return t;
            }

            return null;
        }

        // ── Property / field access ─────────────────────────────────────────────

        private struct AccessResult
        {
            public object Value;
            public Type ReturnType;
            public string Error;
        }

        private static AccessResult AccessProperty(Type type, string memberName)
        {
            // Try property first
            var prop = type.GetProperty(memberName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (prop != null)
            {
                return new AccessResult
                {
                    Value = prop.GetValue(null),
                    ReturnType = prop.PropertyType
                };
            }

            // Try field
            var field = type.GetField(memberName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (field != null)
            {
                return new AccessResult
                {
                    Value = field.GetValue(null),
                    ReturnType = field.FieldType
                };
            }

            return new AccessResult
            {
                Error = $"No static property or field '{memberName}' found on type '{type.FullName}'."
            };
        }

        // ── Method invocation ───────────────────────────────────────────────────

        private struct InvokeResult
        {
            public object Value;
            public Type ReturnType;
            public string Error;
        }

        private static InvokeResult InvokeMethod(Type type, string methodName, string[] rawArgs)
        {
            // Get all public static methods with the given name
            var candidates = type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(m => m.Name == methodName)
                .ToArray();

            if (candidates.Length == 0)
            {
                return new InvokeResult
                {
                    Error = $"No static method '{methodName}' found on type '{type.FullName}'."
                };
            }

            // Find an overload whose parameter count matches
            var method = candidates.FirstOrDefault(m => m.GetParameters().Length == rawArgs.Length);
            if (method == null)
            {
                var counts = string.Join(", ", candidates.Select(m => m.GetParameters().Length.ToString()));
                return new InvokeResult
                {
                    Error = $"No overload of '{type.FullName}.{methodName}' accepts {rawArgs.Length} argument(s). " +
                            $"Available overloads accept: [{counts}] parameters."
                };
            }

            // Convert arguments
            var parameters = method.GetParameters();
            var convertedArgs = new object[rawArgs.Length];
            for (int i = 0; i < rawArgs.Length; i++)
            {
                try
                {
                    convertedArgs[i] = ConvertArgument(rawArgs[i], parameters[i].ParameterType);
                }
                catch (Exception ex)
                {
                    return new InvokeResult
                    {
                        Error = $"Failed to convert argument {i} ('{rawArgs[i]}') to " +
                                $"{parameters[i].ParameterType.Name}: {ex.Message}"
                    };
                }
            }

            object returnVal = method.Invoke(null, convertedArgs);
            return new InvokeResult
            {
                Value = returnVal,
                ReturnType = method.ReturnType == typeof(void) ? null : method.ReturnType
            };
        }

        // ── Argument conversion ─────────────────────────────────────────────────

        internal static object ConvertArgument(string raw, Type targetType)
        {
            // Handle quoted strings
            if (raw.StartsWith("\"") && raw.EndsWith("\""))
            {
                string unquoted = raw.Substring(1, raw.Length - 2)
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");

                if (targetType == typeof(string))
                    return unquoted;

                return Convert.ChangeType(unquoted, targetType);
            }

            // Handle booleans (case-insensitive)
            if (targetType == typeof(bool))
            {
                if (raw.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            }

            // Handle null
            if (raw.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            // Handle enums
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, raw, ignoreCase: true);
            }

            // Numeric / general conversion
            return Convert.ChangeType(raw, targetType, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
