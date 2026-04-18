using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AI
{
    public static class AiUtilityCreateTool
    {
        static readonly HashSet<string> ValidCurves = new HashSet<string>
        {
            "linear", "quadratic", "logistic", "exponential", "step"
        };

        static readonly HashSet<string> ValidCombinations = new HashSet<string>
        {
            "multiply", "average", "min"
        };

        static readonly HashSet<string> ValidInputTypes = new HashSet<string>
        {
            "float", "health", "distance", "ammo", "time"
        };

        [MosaicTool("ai/utility-create",
                    "Generates a C# MonoBehaviour implementing a utility AI system with response curves, scored actions, and input axes",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AiUtilityCreateResult> Execute(AiUtilityCreateParams p)
        {
            // --- Validation ---
            if (string.IsNullOrWhiteSpace(p.AgentName))
                return ToolResult<AiUtilityCreateResult>.Fail(
                    "AgentName is required", ErrorCodes.INVALID_PARAM);

            if (p.Actions == null || p.Actions.Count == 0)
                return ToolResult<AiUtilityCreateResult>.Fail(
                    "At least one action is required", ErrorCodes.INVALID_PARAM);

            foreach (var action in p.Actions)
            {
                if (string.IsNullOrWhiteSpace(action.Name))
                    return ToolResult<AiUtilityCreateResult>.Fail(
                        "Action.Name is required for every action", ErrorCodes.INVALID_PARAM);

                var combo = (action.CombinationMethod ?? "multiply").ToLowerInvariant();
                if (!ValidCombinations.Contains(combo))
                    return ToolResult<AiUtilityCreateResult>.Fail(
                        $"Invalid CombinationMethod '{action.CombinationMethod}'. Valid: {string.Join(", ", ValidCombinations)}",
                        ErrorCodes.INVALID_PARAM);

                if (action.Considerations != null)
                {
                    foreach (var c in action.Considerations)
                    {
                        var curve = (c.Curve ?? "linear").ToLowerInvariant();
                        if (!ValidCurves.Contains(curve))
                            return ToolResult<AiUtilityCreateResult>.Fail(
                                $"Invalid curve type '{c.Curve}'. Valid: {string.Join(", ", ValidCurves)}",
                                ErrorCodes.INVALID_PARAM);
                    }
                }
            }

            if (p.Inputs != null)
            {
                foreach (var input in p.Inputs)
                {
                    if (string.IsNullOrWhiteSpace(input.Name))
                        return ToolResult<AiUtilityCreateResult>.Fail(
                            "Input.Name is required", ErrorCodes.INVALID_PARAM);
                    var inputType = (input.Type ?? "float").ToLowerInvariant();
                    if (!ValidInputTypes.Contains(inputType))
                        return ToolResult<AiUtilityCreateResult>.Fail(
                            $"Invalid input type '{input.Type}'. Valid: {string.Join(", ", ValidInputTypes)}",
                            ErrorCodes.INVALID_PARAM);
                }
            }

            var savePath = string.IsNullOrEmpty(p.SavePath)
                ? "Assets/Generated/AI/"
                : p.SavePath;

            if (!savePath.StartsWith("Assets/"))
                return ToolResult<AiUtilityCreateResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (!savePath.EndsWith("/"))
                savePath += "/";

            // --- Collect all input names ---
            var inputNames = new HashSet<string>();
            if (p.Inputs != null)
                foreach (var inp in p.Inputs)
                    inputNames.Add(inp.Name);

            // Also collect any InputAxis references from considerations
            foreach (var action in p.Actions)
            {
                if (action.Considerations == null) continue;
                foreach (var c in action.Considerations)
                {
                    if (!string.IsNullOrEmpty(c.InputAxis))
                        inputNames.Add(c.InputAxis);
                }
            }

            var className = SanitizeIdentifier(p.AgentName) + "UtilityAI";

            // --- Generate script ---
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");

            // Input fields (normalized 0-1)
            sb.AppendLine("    // === Input Axes (normalized 0-1) ===");
            foreach (var inputName in inputNames.OrderBy(n => n))
            {
                var fieldName = SanitizeIdentifier(inputName);
                var source = "";
                if (p.Inputs != null)
                {
                    var def = p.Inputs.FirstOrDefault(i => i.Name == inputName);
                    if (def != null && !string.IsNullOrEmpty(def.Source))
                        source = $" // {EscapeComment(def.Source)}";
                }
                sb.AppendLine($"    [Range(0f, 1f)] public float {fieldName};{source}");
            }
            sb.AppendLine();

            // Response curve static methods
            sb.AppendLine("    // === Response Curves ===");
            sb.AppendLine("    public static float CurveLinear(float x, float slope, float shift)");
            sb.AppendLine("    {");
            sb.AppendLine("        return Mathf.Clamp01(slope * (x - shift));");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static float CurveQuadratic(float x, float exponent, float shift)");
            sb.AppendLine("    {");
            sb.AppendLine("        return Mathf.Clamp01(Mathf.Pow(Mathf.Max(0f, x - shift), exponent));");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static float CurveLogistic(float x, float slope, float shift)");
            sb.AppendLine("    {");
            sb.AppendLine("        return Mathf.Clamp01(1f / (1f + Mathf.Exp(-slope * (x - shift))));");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static float CurveExponential(float x, float exponent, float shift)");
            sb.AppendLine("    {");
            sb.AppendLine("        return Mathf.Clamp01(Mathf.Pow(Mathf.Max(0f, x - shift), exponent));");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static float CurveStep(float x, float threshold)");
            sb.AppendLine("    {");
            sb.AppendLine("        return x >= threshold ? 1f : 0f;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Score methods per action
            sb.AppendLine("    // === Action Scoring ===");
            for (int i = 0; i < p.Actions.Count; i++)
            {
                var action = p.Actions[i];
                var methodId = SanitizeIdentifier(action.Name);
                var combo = (action.CombinationMethod ?? "multiply").ToLowerInvariant();

                sb.AppendLine($"    float Score_{methodId}()");
                sb.AppendLine("    {");

                if (action.Considerations == null || action.Considerations.Count == 0)
                {
                    sb.AppendLine($"        return {FormatFloat(action.Weight)};");
                }
                else
                {
                    // Evaluate each consideration
                    for (int ci = 0; ci < action.Considerations.Count; ci++)
                    {
                        var c = action.Considerations[ci];
                        var inputField = SanitizeIdentifier(c.InputAxis ?? "0f");
                        var curve = (c.Curve ?? "linear").ToLowerInvariant();
                        var curveCall = curve switch
                        {
                            "linear"      => $"CurveLinear({inputField}, {FormatFloat(c.Slope)}, {FormatFloat(c.Shift)})",
                            "quadratic"   => $"CurveQuadratic({inputField}, {FormatFloat(c.Exponent)}, {FormatFloat(c.Shift)})",
                            "logistic"    => $"CurveLogistic({inputField}, {FormatFloat(c.Slope)}, {FormatFloat(c.Shift)})",
                            "exponential" => $"CurveExponential({inputField}, {FormatFloat(c.Exponent)}, {FormatFloat(c.Shift)})",
                            "step"        => $"CurveStep({inputField}, {FormatFloat(c.Threshold)})",
                            _             => $"CurveLinear({inputField}, {FormatFloat(c.Slope)}, {FormatFloat(c.Shift)})"
                        };
                        sb.AppendLine($"        float c{ci} = {curveCall};");
                    }

                    // Combine
                    var cCount = action.Considerations.Count;
                    var cVars = string.Join(", ", Enumerable.Range(0, cCount).Select(ci => $"c{ci}"));

                    switch (combo)
                    {
                        case "multiply":
                            var mulExpr = string.Join(" * ", Enumerable.Range(0, cCount).Select(ci => $"c{ci}"));
                            sb.AppendLine($"        float combined = {mulExpr};");
                            break;
                        case "average":
                            var sumExpr = string.Join(" + ", Enumerable.Range(0, cCount).Select(ci => $"c{ci}"));
                            sb.AppendLine($"        float combined = ({sumExpr}) / {cCount}f;");
                            break;
                        case "min":
                            sb.AppendLine($"        float combined = Mathf.Min({cVars});");
                            break;
                    }

                    sb.AppendLine($"        return combined * {FormatFloat(action.Weight)};");
                }

                sb.AppendLine("    }");
                sb.AppendLine();
            }

            // Update — pick highest scoring action
            sb.AppendLine("    // === Decision Loop ===");
            sb.AppendLine("    void Update()");
            sb.AppendLine("    {");
            sb.AppendLine("        float bestScore = float.MinValue;");
            sb.AppendLine("        int bestAction = 0;");
            sb.AppendLine();

            for (int i = 0; i < p.Actions.Count; i++)
            {
                var methodId = SanitizeIdentifier(p.Actions[i].Name);
                sb.AppendLine($"        float score{i} = Score_{methodId}();");
                sb.AppendLine($"        if (score{i} > bestScore) {{ bestScore = score{i}; bestAction = {i}; }}");
            }

            sb.AppendLine();
            sb.AppendLine("        switch (bestAction)");
            sb.AppendLine("        {");
            for (int i = 0; i < p.Actions.Count; i++)
            {
                var action = p.Actions[i];
                var callMethod = SanitizeIdentifier(
                    !string.IsNullOrEmpty(action.MethodName) ? action.MethodName : action.Name);
                sb.AppendLine($"            case {i}: Execute_{callMethod}(); break;");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Virtual action stubs
            sb.AppendLine("    // === Action Methods (override in subclass) ===");
            var emittedMethods = new HashSet<string>();
            foreach (var action in p.Actions)
            {
                var callMethod = SanitizeIdentifier(
                    !string.IsNullOrEmpty(action.MethodName) ? action.MethodName : action.Name);
                if (emittedMethods.Contains(callMethod)) continue;
                emittedMethods.Add(callMethod);

                sb.AppendLine($"    public virtual void Execute_{callMethod}()");
                sb.AppendLine("    {");
                sb.AppendLine($"        Debug.Log(\"[UtilityAI] Executing: {EscapeStringLiteral(callMethod)}\");");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("}");

            // --- Write script ---
            var scriptFileName = $"{className}.cs";
            var scriptAssetPath = savePath + scriptFileName;
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, scriptAssetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(scriptAssetPath);

            // --- Optionally attach to GO ---
            string goName = null;
            int instanceId = 0;

            if (!string.IsNullOrEmpty(p.AttachTo))
            {
                var go = GameObject.Find(p.AttachTo);
                if (go == null)
                    return ToolResult<AiUtilityCreateResult>.Fail(
                        $"GameObject '{p.AttachTo}' not found", ErrorCodes.NOT_FOUND);

                goName = go.name;
                instanceId = go.GetInstanceID();

                // Try to add if the type is already compiled
                var scriptType = FindTypeByName(className);
                if (scriptType != null)
                {
                    Undo.RegisterCompleteObjectUndo(go, $"Add {className}");
                    go.AddComponent(scriptType);
                    instanceId = go.GetInstanceID();
                }
            }

            return ToolResult<AiUtilityCreateResult>.Ok(new AiUtilityCreateResult
            {
                ScriptPath     = scriptAssetPath,
                GameObjectName = goName,
                InstanceId     = instanceId,
                ActionCount    = p.Actions.Count,
                InputCount     = inputNames.Count
            });
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var sb = new StringBuilder();
            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_')
                    sb.Append(ch);
            }
            var result = sb.ToString();
            if (result.Length == 0 || char.IsDigit(result[0]))
                result = "_" + result;
            return result;
        }

        static string EscapeStringLiteral(string s)
        {
            return s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
        }

        static string EscapeComment(string s)
        {
            return s?.Replace("\n", " ").Replace("\r", "") ?? "";
        }

        static string FormatFloat(float v)
        {
            // Ensure we always get the 'f' suffix for C# float literals
            var s = v.ToString("G");
            if (!s.Contains('.') && !s.Contains('E'))
                s += ".0";
            return s + "f";
        }

        static Type FindTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
