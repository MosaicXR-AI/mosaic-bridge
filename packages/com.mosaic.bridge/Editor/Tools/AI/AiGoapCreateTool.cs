using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AI
{
    public static class AiGoapCreateTool
    {
        static readonly HashSet<string> ValidStateTypes = new HashSet<string>
        {
            "bool", "int", "float", "string"
        };

        [MosaicTool("ai/goap-create",
                    "Generates a C# MonoBehaviour implementing a GOAP agent with world state, goals, actions, and an A* planner",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AiGoapCreateResult> Execute(AiGoapCreateParams p)
        {
            if (string.IsNullOrWhiteSpace(p.AgentName))
                return ToolResult<AiGoapCreateResult>.Fail(
                    "AgentName is required", ErrorCodes.INVALID_PARAM);

            if (p.Actions == null || p.Actions.Length == 0)
                return ToolResult<AiGoapCreateResult>.Fail(
                    "At least one Action is required", ErrorCodes.INVALID_PARAM);

            if (p.Goals == null || p.Goals.Length == 0)
                return ToolResult<AiGoapCreateResult>.Fail(
                    "At least one Goal is required", ErrorCodes.INVALID_PARAM);

            // Validate world state types
            if (p.WorldState != null)
            {
                foreach (var sv in p.WorldState)
                {
                    if (string.IsNullOrWhiteSpace(sv.Key))
                        return ToolResult<AiGoapCreateResult>.Fail(
                            "WorldState variable Key is required", ErrorCodes.INVALID_PARAM);
                    if (!ValidStateTypes.Contains((sv.Type ?? "").ToLowerInvariant()))
                        return ToolResult<AiGoapCreateResult>.Fail(
                            $"Invalid state type '{sv.Type}'. Valid: {string.Join(", ", ValidStateTypes)}",
                            ErrorCodes.INVALID_PARAM);
                }
            }

            // Validate goals
            foreach (var goal in p.Goals)
            {
                if (string.IsNullOrWhiteSpace(goal.Name))
                    return ToolResult<AiGoapCreateResult>.Fail(
                        "Goal Name is required", ErrorCodes.INVALID_PARAM);
                if (goal.Conditions == null || goal.Conditions.Length == 0)
                    return ToolResult<AiGoapCreateResult>.Fail(
                        $"Goal '{goal.Name}' must have at least one Condition", ErrorCodes.INVALID_PARAM);
            }

            // Validate actions
            foreach (var action in p.Actions)
            {
                if (string.IsNullOrWhiteSpace(action.Name))
                    return ToolResult<AiGoapCreateResult>.Fail(
                        "Action Name is required", ErrorCodes.INVALID_PARAM);
                if (string.IsNullOrWhiteSpace(action.MethodName))
                    return ToolResult<AiGoapCreateResult>.Fail(
                        $"Action '{action.Name}' must have a MethodName", ErrorCodes.INVALID_PARAM);
            }

            var savePath = string.IsNullOrEmpty(p.SavePath)
                ? "Assets/Generated/AI/"
                : p.SavePath;

            if (!savePath.StartsWith("Assets/"))
                return ToolResult<AiGoapCreateResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (!savePath.EndsWith("/"))
                savePath += "/";

            var className = SanitizeIdentifier(p.AgentName) + "GoapAgent";
            var sb = new StringBuilder();

            // Usings
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();

            // GoapAction data class
            sb.AppendLine("[Serializable]");
            sb.AppendLine("public class GoapAction");
            sb.AppendLine("{");
            sb.AppendLine("    public string Name;");
            sb.AppendLine("    public float Cost;");
            sb.AppendLine("    public Dictionary<string, object> Preconditions;");
            sb.AppendLine("    public Dictionary<string, object> Effects;");
            sb.AppendLine("    public Func<bool> Method;");
            sb.AppendLine("}");
            sb.AppendLine();

            // GoapGoal data class
            sb.AppendLine("[Serializable]");
            sb.AppendLine("public class GoapGoal");
            sb.AppendLine("{");
            sb.AppendLine("    public string Name;");
            sb.AppendLine("    public float Priority;");
            sb.AppendLine("    public Dictionary<string, object> Conditions;");
            sb.AppendLine("}");
            sb.AppendLine();

            // Planner node (used by A*)
            sb.AppendLine("public class PlannerNode : IComparable<PlannerNode>");
            sb.AppendLine("{");
            sb.AppendLine("    public Dictionary<string, object> State;");
            sb.AppendLine("    public List<GoapAction> Plan;");
            sb.AppendLine("    public float Cost;");
            sb.AppendLine("    public float Heuristic;");
            sb.AppendLine("    public float F => Cost + Heuristic;");
            sb.AppendLine("    public int CompareTo(PlannerNode other) => F.CompareTo(other.F);");
            sb.AppendLine("}");
            sb.AppendLine();

            // MonoBehaviour class
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");

            // World state
            sb.AppendLine("    // === World State ===");
            sb.AppendLine("    public Dictionary<string, object> WorldState = new Dictionary<string, object>();");
            sb.AppendLine();

            // Goals and actions lists
            sb.AppendLine("    // === Goals (sorted by priority) ===");
            sb.AppendLine("    public List<GoapGoal> Goals = new List<GoapGoal>();");
            sb.AppendLine();
            sb.AppendLine("    // === Actions ===");
            sb.AppendLine("    public List<GoapAction> Actions = new List<GoapAction>();");
            sb.AppendLine();

            // Current plan
            sb.AppendLine("    // === Current Plan ===");
            sb.AppendLine("    private List<GoapAction> _currentPlan = new List<GoapAction>();");
            sb.AppendLine("    private int _currentActionIndex;");
            sb.AppendLine();

            // Awake — initialize world state, goals, actions
            sb.AppendLine("    void Awake()");
            sb.AppendLine("    {");

            // Initialize world state
            if (p.WorldState != null)
            {
                foreach (var sv in p.WorldState)
                {
                    var val = FormatValueLiteral(sv.Type, sv.Value);
                    sb.AppendLine($"        WorldState[\"{EscapeString(sv.Key)}\"] = {val};");
                }
            }
            sb.AppendLine();

            // Initialize goals sorted by priority descending
            var sortedGoals = p.Goals.OrderByDescending(g => g.Priority).ToArray();
            foreach (var goal in sortedGoals)
            {
                sb.AppendLine($"        Goals.Add(new GoapGoal");
                sb.AppendLine("        {");
                sb.AppendLine($"            Name = \"{EscapeString(goal.Name)}\",");
                sb.AppendLine($"            Priority = {goal.Priority}f,");
                sb.AppendLine("            Conditions = new Dictionary<string, object>");
                sb.AppendLine("            {");
                foreach (var cond in goal.Conditions)
                {
                    var condVal = InferValueLiteral(cond.Value);
                    sb.AppendLine($"                {{ \"{EscapeString(cond.Key)}\", {condVal} }},");
                }
                sb.AppendLine("            }");
                sb.AppendLine("        });");
                sb.AppendLine();
            }

            // Initialize actions
            foreach (var action in p.Actions)
            {
                var methodId = SanitizeIdentifier(action.MethodName);
                sb.AppendLine($"        Actions.Add(new GoapAction");
                sb.AppendLine("        {");
                sb.AppendLine($"            Name = \"{EscapeString(action.Name)}\",");
                sb.AppendLine($"            Cost = {action.Cost}f,");
                sb.AppendLine("            Preconditions = new Dictionary<string, object>");
                sb.AppendLine("            {");
                if (action.Preconditions != null)
                {
                    foreach (var pre in action.Preconditions)
                    {
                        var preVal = InferValueLiteral(pre.Value);
                        sb.AppendLine($"                {{ \"{EscapeString(pre.Key)}\", {preVal} }},");
                    }
                }
                sb.AppendLine("            },");
                sb.AppendLine("            Effects = new Dictionary<string, object>");
                sb.AppendLine("            {");
                if (action.Effects != null)
                {
                    foreach (var eff in action.Effects)
                    {
                        var effVal = InferValueLiteral(eff.Value);
                        sb.AppendLine($"                {{ \"{EscapeString(eff.Key)}\", {effVal} }},");
                    }
                }
                sb.AppendLine("            },");
                sb.AppendLine($"            Method = {methodId}");
                sb.AppendLine("        });");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine();

            // Update — run planner if no plan, then execute
            sb.AppendLine("    void Update()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_currentPlan.Count == 0 || _currentActionIndex >= _currentPlan.Count)");
            sb.AppendLine("        {");
            sb.AppendLine("            _currentPlan = RunPlanner();");
            sb.AppendLine("            _currentActionIndex = 0;");
            sb.AppendLine("            if (_currentPlan.Count == 0) return;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        var action = _currentPlan[_currentActionIndex];");
            sb.AppendLine("        if (action.Method != null && action.Method())");
            sb.AppendLine("        {");
            sb.AppendLine("            // Apply effects to world state");
            sb.AppendLine("            foreach (var effect in action.Effects)");
            sb.AppendLine("                WorldState[effect.Key] = effect.Value;");
            sb.AppendLine("            _currentActionIndex++;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // A* planner
            sb.AppendLine("    public List<GoapAction> RunPlanner(int maxDepth = 10)");
            sb.AppendLine("    {");
            sb.AppendLine("        foreach (var goal in Goals)");
            sb.AppendLine("        {");
            sb.AppendLine("            var plan = PlanForGoal(goal, maxDepth);");
            sb.AppendLine("            if (plan != null) return plan;");
            sb.AppendLine("        }");
            sb.AppendLine("        return new List<GoapAction>();");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    private List<GoapAction> PlanForGoal(GoapGoal goal, int maxDepth)");
            sb.AppendLine("    {");
            sb.AppendLine("        var open = new SortedSet<PlannerNode>(Comparer<PlannerNode>.Create((a, b) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            int cmp = a.F.CompareTo(b.F);");
            sb.AppendLine("            return cmp != 0 ? cmp : a.GetHashCode().CompareTo(b.GetHashCode());");
            sb.AppendLine("        }));");
            sb.AppendLine();
            sb.AppendLine("        var startNode = new PlannerNode");
            sb.AppendLine("        {");
            sb.AppendLine("            State = new Dictionary<string, object>(WorldState),");
            sb.AppendLine("            Plan = new List<GoapAction>(),");
            sb.AppendLine("            Cost = 0f,");
            sb.AppendLine("            Heuristic = ComputeHeuristic(WorldState, goal.Conditions)");
            sb.AppendLine("        };");
            sb.AppendLine("        open.Add(startNode);");
            sb.AppendLine();
            sb.AppendLine("        while (open.Count > 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            var current = open.Min;");
            sb.AppendLine("            open.Remove(current);");
            sb.AppendLine();
            sb.AppendLine("            if (StateSatisfiesGoal(current.State, goal.Conditions))");
            sb.AppendLine("                return current.Plan;");
            sb.AppendLine();
            sb.AppendLine("            if (current.Plan.Count >= maxDepth) continue;");
            sb.AppendLine();
            sb.AppendLine("            foreach (var action in Actions)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (!PreconditionsMet(current.State, action.Preconditions)) continue;");
            sb.AppendLine();
            sb.AppendLine("                var newState = new Dictionary<string, object>(current.State);");
            sb.AppendLine("                foreach (var effect in action.Effects)");
            sb.AppendLine("                    newState[effect.Key] = effect.Value;");
            sb.AppendLine();
            sb.AppendLine("                var newPlan = new List<GoapAction>(current.Plan) { action };");
            sb.AppendLine("                open.Add(new PlannerNode");
            sb.AppendLine("                {");
            sb.AppendLine("                    State = newState,");
            sb.AppendLine("                    Plan = newPlan,");
            sb.AppendLine("                    Cost = current.Cost + action.Cost,");
            sb.AppendLine("                    Heuristic = ComputeHeuristic(newState, goal.Conditions)");
            sb.AppendLine("                });");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        return null;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Helper: check if state satisfies goal
            sb.AppendLine("    private bool StateSatisfiesGoal(Dictionary<string, object> state, Dictionary<string, object> conditions)");
            sb.AppendLine("    {");
            sb.AppendLine("        foreach (var cond in conditions)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!state.ContainsKey(cond.Key)) return false;");
            sb.AppendLine("            if (!state[cond.Key].Equals(cond.Value)) return false;");
            sb.AppendLine("        }");
            sb.AppendLine("        return true;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Helper: check preconditions
            sb.AppendLine("    private bool PreconditionsMet(Dictionary<string, object> state, Dictionary<string, object> preconditions)");
            sb.AppendLine("    {");
            sb.AppendLine("        foreach (var pre in preconditions)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!state.ContainsKey(pre.Key)) return false;");
            sb.AppendLine("            if (!state[pre.Key].Equals(pre.Value)) return false;");
            sb.AppendLine("        }");
            sb.AppendLine("        return true;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Helper: heuristic (count of unsatisfied conditions)
            sb.AppendLine("    private float ComputeHeuristic(Dictionary<string, object> state, Dictionary<string, object> conditions)");
            sb.AppendLine("    {");
            sb.AppendLine("        int unsatisfied = 0;");
            sb.AppendLine("        foreach (var cond in conditions)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!state.ContainsKey(cond.Key) || !state[cond.Key].Equals(cond.Value))");
            sb.AppendLine("                unsatisfied++;");
            sb.AppendLine("        }");
            sb.AppendLine("        return unsatisfied;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Virtual action methods
            foreach (var action in p.Actions)
            {
                var methodId = SanitizeIdentifier(action.MethodName);
                sb.AppendLine($"    public virtual bool {methodId}()");
                sb.AppendLine("    {");
                sb.AppendLine($"        Debug.Log(\"[GOAP] Executing: {EscapeString(action.Name)}\");");
                sb.AppendLine("        return true;");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("}");

            // Write script
            var scriptFileName = $"{className}.cs";
            var scriptAssetPath = savePath + scriptFileName;
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, scriptAssetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(scriptAssetPath);

            // Optionally attach to GO
            string goName = null;
            int instanceId = 0;

            if (!string.IsNullOrEmpty(p.AttachTo))
            {
                var go = GameObject.Find(p.AttachTo);
                if (go == null)
                    return ToolResult<AiGoapCreateResult>.Fail(
                        $"GameObject '{p.AttachTo}' not found", ErrorCodes.NOT_FOUND);

                goName = go.name;
                instanceId = go.GetInstanceID();

                var scriptType = FindTypeByName(className);
                if (scriptType != null)
                {
                    Undo.RegisterCompleteObjectUndo(go, $"Add {className}");
                    go.AddComponent(scriptType);
                    instanceId = go.GetInstanceID();
                }
            }

            return ToolResult<AiGoapCreateResult>.Ok(new AiGoapCreateResult
            {
                ScriptPath     = scriptAssetPath,
                GameObjectName = goName,
                InstanceId     = instanceId,
                GoalCount      = p.Goals.Length,
                ActionCount    = p.Actions.Length
            });
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        internal static string SanitizeIdentifier(string name)
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

        internal static string EscapeString(string s)
        {
            return s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
        }

        internal static string FormatValueLiteral(string type, string value)
        {
            if (string.IsNullOrEmpty(value)) value = "";
            switch ((type ?? "string").ToLowerInvariant())
            {
                case "bool":   return value.ToLowerInvariant() == "true" ? "true" : "false";
                case "int":    return int.TryParse(value, out var i) ? i.ToString() : "0";
                case "float":  return float.TryParse(value, out var f) ? $"{f}f" : "0f";
                case "string": return $"\"{EscapeString(value)}\"";
                default:       return $"\"{EscapeString(value)}\"";
            }
        }

        internal static string InferValueLiteral(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase)) return "true";
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) return "false";
            if (int.TryParse(value, out var i)) return i.ToString();
            if (float.TryParse(value, out var f)) return $"{f}f";
            return $"\"{EscapeString(value)}\"";
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
