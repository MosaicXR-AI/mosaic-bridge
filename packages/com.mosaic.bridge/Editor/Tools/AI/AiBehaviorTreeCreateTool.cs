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
    public static class AiBehaviorTreeCreateTool
    {
        static readonly HashSet<string> ValidNodeTypes = new HashSet<string>
        {
            "selector", "sequence", "parallel",
            "inverter", "repeater", "succeeder",
            "action", "condition"
        };

        static readonly HashSet<string> CompositeTypes = new HashSet<string>
        {
            "selector", "sequence", "parallel"
        };

        static readonly HashSet<string> DecoratorTypes = new HashSet<string>
        {
            "inverter", "repeater", "succeeder"
        };

        static readonly HashSet<string> LeafTypes = new HashSet<string>
        {
            "action", "condition"
        };

        static readonly Dictionary<string, string> BlackboardTypeMap = new Dictionary<string, string>
        {
            { "int",        "int" },
            { "float",      "float" },
            { "bool",       "bool" },
            { "string",     "string" },
            { "vector3",    "Vector3" },
            { "gameobject", "GameObject" }
        };

        [MosaicTool("ai/behavior-tree-create",
                    "Generates a C# MonoBehaviour implementing a full behavior tree with composites, decorators, leaf nodes, and optional blackboard",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AiBehaviorTreeCreateResult> Execute(AiBehaviorTreeCreateParams p)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
                return ToolResult<AiBehaviorTreeCreateResult>.Fail(
                    "Name is required", ErrorCodes.INVALID_PARAM);

            if (p.RootNode == null)
                return ToolResult<AiBehaviorTreeCreateResult>.Fail(
                    "RootNode is required", ErrorCodes.INVALID_PARAM);

            // Validate tree structure
            var validationError = ValidateNode(p.RootNode);
            if (validationError != null)
                return ToolResult<AiBehaviorTreeCreateResult>.Fail(
                    validationError, ErrorCodes.INVALID_PARAM);

            // Validate blackboard
            if (p.Blackboard != null)
            {
                foreach (var bv in p.Blackboard)
                {
                    if (string.IsNullOrWhiteSpace(bv.Key))
                        return ToolResult<AiBehaviorTreeCreateResult>.Fail(
                            "Blackboard variable Key is required", ErrorCodes.INVALID_PARAM);
                    if (!BlackboardTypeMap.ContainsKey((bv.Type ?? "").ToLowerInvariant()))
                        return ToolResult<AiBehaviorTreeCreateResult>.Fail(
                            $"Invalid blackboard type '{bv.Type}'. Valid: {string.Join(", ", BlackboardTypeMap.Keys)}",
                            ErrorCodes.INVALID_PARAM);
                }
            }

            var savePath = string.IsNullOrEmpty(p.SavePath)
                ? "Assets/Generated/AI/"
                : p.SavePath;

            if (!savePath.StartsWith("Assets/"))
                return ToolResult<AiBehaviorTreeCreateResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (!savePath.EndsWith("/"))
                savePath += "/";

            // Count nodes
            int nodeCount = CountNodes(p.RootNode);

            // Reset var counter for code generation
            _varCounter = 0;

            // Generate the script
            var sb = new StringBuilder();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();

            // NodeStatus enum
            sb.AppendLine("public enum NodeStatus { Success, Failure, Running }");
            sb.AppendLine();

            // Base node class
            sb.AppendLine("public abstract class BT_NodeBase");
            sb.AppendLine("{");
            sb.AppendLine("    public string NodeName { get; set; }");
            sb.AppendLine("    public abstract NodeStatus Tick();");
            sb.AppendLine("}");
            sb.AppendLine();

            // Composite base
            sb.AppendLine("public abstract class BT_Composite : BT_NodeBase");
            sb.AppendLine("{");
            sb.AppendLine("    protected List<BT_NodeBase> children = new List<BT_NodeBase>();");
            sb.AppendLine("    public void AddChild(BT_NodeBase child) { children.Add(child); }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Selector
            sb.AppendLine("public class BT_Selector : BT_Composite");
            sb.AppendLine("{");
            sb.AppendLine("    public override NodeStatus Tick()");
            sb.AppendLine("    {");
            sb.AppendLine("        foreach (var child in children)");
            sb.AppendLine("        {");
            sb.AppendLine("            var s = child.Tick();");
            sb.AppendLine("            if (s == NodeStatus.Success || s == NodeStatus.Running) return s;");
            sb.AppendLine("        }");
            sb.AppendLine("        return NodeStatus.Failure;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Sequence
            sb.AppendLine("public class BT_Sequence : BT_Composite");
            sb.AppendLine("{");
            sb.AppendLine("    public override NodeStatus Tick()");
            sb.AppendLine("    {");
            sb.AppendLine("        foreach (var child in children)");
            sb.AppendLine("        {");
            sb.AppendLine("            var s = child.Tick();");
            sb.AppendLine("            if (s == NodeStatus.Failure || s == NodeStatus.Running) return s;");
            sb.AppendLine("        }");
            sb.AppendLine("        return NodeStatus.Success;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Parallel
            sb.AppendLine("public class BT_Parallel : BT_Composite");
            sb.AppendLine("{");
            sb.AppendLine("    public override NodeStatus Tick()");
            sb.AppendLine("    {");
            sb.AppendLine("        int successCount = 0;");
            sb.AppendLine("        int failureCount = 0;");
            sb.AppendLine("        foreach (var child in children)");
            sb.AppendLine("        {");
            sb.AppendLine("            var s = child.Tick();");
            sb.AppendLine("            if (s == NodeStatus.Success) successCount++;");
            sb.AppendLine("            else if (s == NodeStatus.Failure) failureCount++;");
            sb.AppendLine("        }");
            sb.AppendLine("        if (failureCount > 0) return NodeStatus.Failure;");
            sb.AppendLine("        if (successCount == children.Count) return NodeStatus.Success;");
            sb.AppendLine("        return NodeStatus.Running;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Decorator base
            sb.AppendLine("public abstract class BT_Decorator : BT_NodeBase");
            sb.AppendLine("{");
            sb.AppendLine("    protected BT_NodeBase child;");
            sb.AppendLine("    public void SetChild(BT_NodeBase c) { child = c; }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Inverter
            sb.AppendLine("public class BT_Inverter : BT_Decorator");
            sb.AppendLine("{");
            sb.AppendLine("    public override NodeStatus Tick()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (child == null) return NodeStatus.Failure;");
            sb.AppendLine("        var s = child.Tick();");
            sb.AppendLine("        if (s == NodeStatus.Success) return NodeStatus.Failure;");
            sb.AppendLine("        if (s == NodeStatus.Failure) return NodeStatus.Success;");
            sb.AppendLine("        return NodeStatus.Running;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Repeater
            sb.AppendLine("public class BT_Repeater : BT_Decorator");
            sb.AppendLine("{");
            sb.AppendLine("    public int RepeatCount { get; set; } = 1;");
            sb.AppendLine("    public override NodeStatus Tick()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (child == null) return NodeStatus.Failure;");
            sb.AppendLine("        for (int i = 0; i < RepeatCount; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            var s = child.Tick();");
            sb.AppendLine("            if (s == NodeStatus.Running) return NodeStatus.Running;");
            sb.AppendLine("        }");
            sb.AppendLine("        return NodeStatus.Success;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Succeeder
            sb.AppendLine("public class BT_Succeeder : BT_Decorator");
            sb.AppendLine("{");
            sb.AppendLine("    public override NodeStatus Tick()");
            sb.AppendLine("    {");
            sb.AppendLine("        child?.Tick();");
            sb.AppendLine("        return NodeStatus.Success;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Generate action leaf classes
            var actionMethods = new List<string>();
            CollectActionNames(p.RootNode, actionMethods);

            foreach (var actionName in actionMethods.Distinct())
            {
                sb.AppendLine($"public class BT_Action_{actionName} : BT_NodeBase");
                sb.AppendLine("{");
                sb.AppendLine($"    private {p.Name}BehaviorTree _owner;");
                sb.AppendLine($"    public BT_Action_{actionName}({p.Name}BehaviorTree owner) {{ _owner = owner; }}");
                sb.AppendLine($"    public override NodeStatus Tick() {{ return _owner.Action_{actionName}(); }}");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            // Generate condition leaf classes
            var conditionExprs = new List<(string name, string expr)>();
            CollectConditions(p.RootNode, conditionExprs);

            int condIdx = 0;
            foreach (var cond in conditionExprs)
            {
                var condClassName = !string.IsNullOrEmpty(cond.name)
                    ? SanitizeIdentifier(cond.name)
                    : $"Condition_{condIdx}";
                sb.AppendLine($"public class BT_{condClassName} : BT_NodeBase");
                sb.AppendLine("{");
                sb.AppendLine($"    private {p.Name}BehaviorTree _owner;");
                sb.AppendLine($"    public BT_{condClassName}({p.Name}BehaviorTree owner) {{ _owner = owner; }}");
                sb.AppendLine($"    public override NodeStatus Tick()");
                sb.AppendLine("    {");
                sb.AppendLine($"        // Condition: {EscapeStringLiteral(cond.expr ?? "true")}");
                sb.AppendLine($"        return _owner.Condition_{condClassName}() ? NodeStatus.Success : NodeStatus.Failure;");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
                condIdx++;
            }

            // MonoBehaviour class
            sb.AppendLine($"public class {p.Name}BehaviorTree : MonoBehaviour");
            sb.AppendLine("{");

            // Blackboard fields
            int bbCount = 0;
            if (p.Blackboard != null && p.Blackboard.Length > 0)
            {
                sb.AppendLine("    // === Blackboard ===");
                foreach (var bv in p.Blackboard)
                {
                    var csType = BlackboardTypeMap[bv.Type.ToLowerInvariant()];
                    var defaultVal = GetDefaultValueLiteral(bv);
                    sb.AppendLine($"    public {csType} {bv.Key}{defaultVal};");
                    bbCount++;
                }
                sb.AppendLine();
            }

            sb.AppendLine("    private BT_NodeBase rootNode;");
            sb.AppendLine();

            // Awake — build tree
            sb.AppendLine("    void Awake()");
            sb.AppendLine("    {");
            EmitNodeConstruction(sb, p.RootNode, "rootNode", "        ", p.Name, actionMethods, conditionExprs);
            sb.AppendLine("    }");
            sb.AppendLine();

            // Update
            sb.AppendLine("    void Update()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (rootNode != null) rootNode.Tick();");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Action methods (virtual, returns Running)
            foreach (var actionName in actionMethods.Distinct())
            {
                sb.AppendLine($"    public virtual NodeStatus Action_{actionName}()");
                sb.AppendLine("    {");
                sb.AppendLine($"        Debug.Log(\"[BT] Action: {actionName}\");");
                sb.AppendLine("        return NodeStatus.Running;");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            // Condition methods (virtual, returns true)
            condIdx = 0;
            foreach (var cond in conditionExprs)
            {
                var condClassName = !string.IsNullOrEmpty(cond.name)
                    ? SanitizeIdentifier(cond.name)
                    : $"Condition_{condIdx}";
                sb.AppendLine($"    public virtual bool Condition_{condClassName}()");
                sb.AppendLine("    {");
                sb.AppendLine($"        // TODO: Implement condition: {EscapeStringLiteral(cond.expr ?? "true")}");
                sb.AppendLine("        return true;");
                sb.AppendLine("    }");
                sb.AppendLine();
                condIdx++;
            }

            sb.AppendLine("}");

            // Write script
            var scriptFileName = $"{p.Name}BehaviorTree.cs";
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
                    return ToolResult<AiBehaviorTreeCreateResult>.Fail(
                        $"GameObject '{p.AttachTo}' not found", ErrorCodes.NOT_FOUND);

                // We cannot AddComponent for a script that hasn't compiled yet,
                // so we record the intent; the component will be added after domain reload.
                goName = go.name;
                instanceId = go.GetInstanceID();

                // Try to add if the type is already available (unlikely on first create)
                var scriptType = FindTypeByName($"{p.Name}BehaviorTree");
                if (scriptType != null)
                {
                    Undo.RegisterCompleteObjectUndo(go, $"Add {p.Name}BehaviorTree");
                    go.AddComponent(scriptType);
                    instanceId = go.GetInstanceID();
                }
            }

            return ToolResult<AiBehaviorTreeCreateResult>.Ok(new AiBehaviorTreeCreateResult
            {
                ScriptPath         = scriptAssetPath,
                GameObjectName     = goName,
                InstanceId         = instanceId,
                NodeCount          = nodeCount,
                BlackboardVarCount = bbCount
            });
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        static string ValidateNode(TreeNodeDef node, int depth = 0)
        {
            if (node == null)
                return "TreeNodeDef cannot be null";
            if (string.IsNullOrWhiteSpace(node.Type))
                return "TreeNodeDef.Type is required";

            var type = node.Type.ToLowerInvariant();
            if (!ValidNodeTypes.Contains(type))
                return $"Invalid node type '{node.Type}'. Valid: {string.Join(", ", ValidNodeTypes)}";

            if (string.IsNullOrWhiteSpace(node.Name))
                return "TreeNodeDef.Name is required";

            if (CompositeTypes.Contains(type))
            {
                if (node.Children == null || node.Children.Length == 0)
                    return $"Composite node '{node.Name}' (type={type}) must have at least one child";
                foreach (var child in node.Children)
                {
                    var err = ValidateNode(child, depth + 1);
                    if (err != null) return err;
                }
            }
            else if (DecoratorTypes.Contains(type))
            {
                if (node.Children == null || node.Children.Length != 1)
                    return $"Decorator node '{node.Name}' (type={type}) must have exactly one child";
                var err = ValidateNode(node.Children[0], depth + 1);
                if (err != null) return err;
            }
            else if (type == "action")
            {
                if (string.IsNullOrWhiteSpace(node.Action))
                    return $"Action node '{node.Name}' must have an Action method name";
            }

            return null;
        }

        static int CountNodes(TreeNodeDef node)
        {
            if (node == null) return 0;
            int count = 1;
            if (node.Children != null)
                foreach (var c in node.Children)
                    count += CountNodes(c);
            return count;
        }

        static void CollectActionNames(TreeNodeDef node, List<string> names)
        {
            if (node == null) return;
            if (node.Type?.ToLowerInvariant() == "action" && !string.IsNullOrEmpty(node.Action))
                names.Add(SanitizeIdentifier(node.Action));
            if (node.Children != null)
                foreach (var c in node.Children)
                    CollectActionNames(c, names);
        }

        static void CollectConditions(TreeNodeDef node, List<(string name, string expr)> conditions)
        {
            if (node == null) return;
            if (node.Type?.ToLowerInvariant() == "condition")
                conditions.Add((node.Name, node.Condition));
            if (node.Children != null)
                foreach (var c in node.Children)
                    CollectConditions(c, conditions);
        }

        static int _varCounter;

        static void EmitNodeConstruction(StringBuilder sb, TreeNodeDef node, string varName,
            string indent, string treeName, List<string> actionNames,
            List<(string name, string expr)> conditions)
        {
            var type = node.Type.ToLowerInvariant();

            if (CompositeTypes.Contains(type))
            {
                var className = type switch
                {
                    "selector" => "BT_Selector",
                    "sequence" => "BT_Sequence",
                    "parallel" => "BT_Parallel",
                    _          => "BT_Selector"
                };
                sb.AppendLine($"{indent}var {varName} = new {className}();");
                sb.AppendLine($"{indent}{varName}.NodeName = \"{EscapeStringLiteral(node.Name)}\";");

                for (int i = 0; i < node.Children.Length; i++)
                {
                    var childVar = $"node_{_varCounter++}";
                    EmitNodeConstruction(sb, node.Children[i], childVar, indent, treeName, actionNames, conditions);
                    sb.AppendLine($"{indent}{varName}.AddChild({childVar});");
                }
            }
            else if (DecoratorTypes.Contains(type))
            {
                var className = type switch
                {
                    "inverter"  => "BT_Inverter",
                    "repeater"  => "BT_Repeater",
                    "succeeder" => "BT_Succeeder",
                    _           => "BT_Inverter"
                };
                sb.AppendLine($"{indent}var {varName} = new {className}();");
                sb.AppendLine($"{indent}{varName}.NodeName = \"{EscapeStringLiteral(node.Name)}\";");
                if (type == "repeater" && node.RepeatCount.HasValue)
                    sb.AppendLine($"{indent}(({className}){varName}).RepeatCount = {node.RepeatCount.Value};");

                var childVar = $"node_{_varCounter++}";
                EmitNodeConstruction(sb, node.Children[0], childVar, indent, treeName, actionNames, conditions);
                sb.AppendLine($"{indent}{varName}.SetChild({childVar});");
            }
            else if (type == "action")
            {
                var sanitized = SanitizeIdentifier(node.Action);
                sb.AppendLine($"{indent}var {varName} = new BT_Action_{sanitized}(this);");
                sb.AppendLine($"{indent}{varName}.NodeName = \"{EscapeStringLiteral(node.Name)}\";");
            }
            else if (type == "condition")
            {
                var condName = SanitizeIdentifier(node.Name);
                sb.AppendLine($"{indent}var {varName} = new BT_{condName}(this);");
                sb.AppendLine($"{indent}{varName}.NodeName = \"{EscapeStringLiteral(node.Name)}\";");
            }
        }

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

        static string GetDefaultValueLiteral(BlackboardVar bv)
        {
            if (string.IsNullOrEmpty(bv.DefaultValue)) return "";
            var csType = bv.Type.ToLowerInvariant();
            return csType switch
            {
                "string"     => $" = \"{EscapeStringLiteral(bv.DefaultValue)}\"",
                "bool"       => $" = {bv.DefaultValue.ToLowerInvariant()}",
                "int"        => $" = {bv.DefaultValue}",
                "float"      => $" = {bv.DefaultValue}f",
                "vector3"    => $" = new Vector3({bv.DefaultValue})",
                "gameobject" => "",
                _            => ""
            };
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
