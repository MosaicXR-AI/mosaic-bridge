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

namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public static class NavBehaviorTreeTool
    {
        static readonly string[] ValidNodeTypes = { "selector", "sequence", "action", "condition" };

        [MosaicTool("nav/behavior-tree",
                    "Scaffolds a behavior tree framework with node base classes and a tree MonoBehaviour built from specified nodes",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<NavBehaviorTreeResult> Execute(NavBehaviorTreeParams p)
        {
            var outputDir = string.IsNullOrEmpty(p.OutputDirectory)
                ? $"Assets/Generated/Navigation/BehaviorTree/{p.TreeName}"
                : p.OutputDirectory;

            if (!outputDir.StartsWith("Assets/"))
                return ToolResult<NavBehaviorTreeResult>.Fail(
                    "OutputDirectory must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            // Parse nodes
            var parsedNodes = new List<(string type, string name)>();
            var nodeTypes = new HashSet<string>();

            if (p.Nodes != null)
            {
                foreach (var nodeStr in p.Nodes)
                {
                    var parts = nodeStr.Split(':');
                    if (parts.Length != 2)
                        return ToolResult<NavBehaviorTreeResult>.Fail(
                            $"Invalid node format '{nodeStr}'. Expected 'type:Name' (e.g. 'selector:Root')",
                            ErrorCodes.INVALID_PARAM);

                    var type = parts[0].Trim().ToLowerInvariant();
                    var name = parts[1].Trim();

                    if (!ValidNodeTypes.Contains(type))
                        return ToolResult<NavBehaviorTreeResult>.Fail(
                            $"Invalid node type '{type}'. Valid: {string.Join(", ", ValidNodeTypes)}",
                            ErrorCodes.INVALID_PARAM);

                    parsedNodes.Add((type, name));
                    nodeTypes.Add(type);
                }
            }

            if (parsedNodes.Count == 0)
                return ToolResult<NavBehaviorTreeResult>.Fail(
                    "At least one node must be specified in the Nodes array",
                    ErrorCodes.INVALID_PARAM);

            var projectRoot = Application.dataPath.Replace("/Assets", "");

            // --- BT_Node.cs (abstract base) ---
            var nodeBaseSrc = @"using System.Collections.Generic;

public enum NodeState { Running, Success, Failure }

/// <summary>
/// Abstract base class for all behavior tree nodes.
/// </summary>
public abstract class BT_Node
{
    public NodeState State { get; protected set; } = NodeState.Failure;
    public string NodeName { get; set; }
    protected List<BT_Node> children = new List<BT_Node>();

    public virtual BT_Node AddChild(BT_Node child)
    {
        children.Add(child);
        return this;
    }

    public abstract NodeState Tick();
}";

            // --- BT_Selector.cs ---
            var selectorSrc = @"/// <summary>
/// Selector (OR) node: returns Success on first child success, Failure if all fail.
/// </summary>
public class BT_Selector : BT_Node
{
    public override NodeState Tick()
    {
        foreach (var child in children)
        {
            var state = child.Tick();
            if (state == NodeState.Running)
            {
                State = NodeState.Running;
                return State;
            }
            if (state == NodeState.Success)
            {
                State = NodeState.Success;
                return State;
            }
        }
        State = NodeState.Failure;
        return State;
    }
}";

            // --- BT_Sequence.cs ---
            var sequenceSrc = @"/// <summary>
/// Sequence (AND) node: returns Failure on first child failure, Success if all succeed.
/// </summary>
public class BT_Sequence : BT_Node
{
    public override NodeState Tick()
    {
        foreach (var child in children)
        {
            var state = child.Tick();
            if (state == NodeState.Running)
            {
                State = NodeState.Running;
                return State;
            }
            if (state == NodeState.Failure)
            {
                State = NodeState.Failure;
                return State;
            }
        }
        State = NodeState.Success;
        return State;
    }
}";

            // --- BT_Condition.cs ---
            var conditionSrc = @"/// <summary>
/// Base class for condition (check) leaf nodes.
/// Override Evaluate() to implement the condition check.
/// </summary>
public abstract class BT_Condition : BT_Node
{
    protected abstract bool Evaluate();

    public override NodeState Tick()
    {
        State = Evaluate() ? NodeState.Success : NodeState.Failure;
        return State;
    }
}";

            // --- BT_Action.cs ---
            var actionSrc = @"/// <summary>
/// Base class for action leaf nodes.
/// Override Execute() to implement the behavior.
/// Return NodeState.Running for multi-frame actions.
/// </summary>
public abstract class BT_Action : BT_Node
{
    protected abstract NodeState Execute();

    public override NodeState Tick()
    {
        State = Execute();
        return State;
    }
}";

            // Write framework files
            WriteFile(projectRoot, Path.Combine(outputDir, "BT_Node.cs").Replace("\\", "/"), nodeBaseSrc);
            WriteFile(projectRoot, Path.Combine(outputDir, "BT_Selector.cs").Replace("\\", "/"), selectorSrc);
            WriteFile(projectRoot, Path.Combine(outputDir, "BT_Sequence.cs").Replace("\\", "/"), sequenceSrc);
            WriteFile(projectRoot, Path.Combine(outputDir, "BT_Condition.cs").Replace("\\", "/"), conditionSrc);
            WriteFile(projectRoot, Path.Combine(outputDir, "BT_Action.cs").Replace("\\", "/"), actionSrc);

            // Generate concrete node classes for each action/condition
            foreach (var node in parsedNodes)
            {
                if (node.type == "action")
                {
                    var src = $@"using UnityEngine;

/// <summary>
/// Concrete action node: {node.name}.
/// Implement your action logic in the Execute method.
/// </summary>
public class {node.name} : BT_Action
{{
    protected override NodeState Execute()
    {{
        // TODO: Implement {node.name} action logic
        Debug.Log(""[BT] Executing action: {node.name}"");
        return NodeState.Success;
    }}
}}";
                    WriteFile(projectRoot, Path.Combine(outputDir, $"{node.name}.cs").Replace("\\", "/"), src);
                }
                else if (node.type == "condition")
                {
                    var src = $@"using UnityEngine;

/// <summary>
/// Concrete condition node: {node.name}.
/// Implement your condition check in the Evaluate method.
/// </summary>
public class {node.name} : BT_Condition
{{
    protected override bool Evaluate()
    {{
        // TODO: Implement {node.name} condition check
        Debug.Log(""[BT] Evaluating condition: {node.name}"");
        return true;
    }}
}}";
                    WriteFile(projectRoot, Path.Combine(outputDir, $"{node.name}.cs").Replace("\\", "/"), src);
                }
            }

            // --- {TreeName}Tree.cs MonoBehaviour ---
            var treeSb = new StringBuilder();
            treeSb.AppendLine("using UnityEngine;");
            treeSb.AppendLine();
            treeSb.AppendLine($"/// <summary>");
            treeSb.AppendLine($"/// {p.TreeName} behavior tree. Builds the tree from configured nodes and ticks each Update.");
            treeSb.AppendLine($"/// </summary>");
            treeSb.AppendLine($"public class {p.TreeName}Tree : MonoBehaviour");
            treeSb.AppendLine("{");
            treeSb.AppendLine("    BT_Node root;");
            treeSb.AppendLine();
            treeSb.AppendLine("    void Start()");
            treeSb.AppendLine("    {");
            treeSb.AppendLine("        BuildTree();");
            treeSb.AppendLine("    }");
            treeSb.AppendLine();
            treeSb.AppendLine("    void BuildTree()");
            treeSb.AppendLine("    {");

            // Emit node creation
            var nodeVarNames = new Dictionary<string, string>();
            int varIdx = 0;
            foreach (var node in parsedNodes)
            {
                string varName = $"node_{varIdx}";
                nodeVarNames[node.name] = varName;
                string typeName = node.type switch
                {
                    "selector" => "BT_Selector",
                    "sequence" => "BT_Sequence",
                    _          => node.name
                };
                treeSb.AppendLine($"        var {varName} = new {typeName}();");
                treeSb.AppendLine($"        {varName}.NodeName = \"{node.name}\";");
                varIdx++;
            }

            treeSb.AppendLine();

            // Wire up: composites get subsequent nodes as children until next composite
            // Simple heuristic: first composite is root, subsequent leaf nodes are children of the most recent composite
            string lastComposite = null;
            foreach (var node in parsedNodes)
            {
                var vn = nodeVarNames[node.name];
                if (node.type == "selector" || node.type == "sequence")
                {
                    if (lastComposite != null)
                    {
                        treeSb.AppendLine($"        {lastComposite}.AddChild({vn});");
                    }
                    lastComposite = vn;
                }
                else
                {
                    if (lastComposite != null)
                        treeSb.AppendLine($"        {lastComposite}.AddChild({vn});");
                }
            }

            treeSb.AppendLine();
            treeSb.AppendLine($"        root = {nodeVarNames[parsedNodes[0].name]};");
            treeSb.AppendLine("    }");
            treeSb.AppendLine();
            treeSb.AppendLine("    void Update()");
            treeSb.AppendLine("    {");
            treeSb.AppendLine("        if (root != null)");
            treeSb.AppendLine("            root.Tick();");
            treeSb.AppendLine("    }");
            treeSb.AppendLine("}");

            WriteFile(projectRoot, Path.Combine(outputDir, $"{p.TreeName}Tree.cs").Replace("\\", "/"), treeSb.ToString());

            return ToolResult<NavBehaviorTreeResult>.Ok(new NavBehaviorTreeResult
            {
                ScriptDirectory = outputDir,
                TreeName        = p.TreeName,
                NodeCount       = parsedNodes.Count,
                NodeTypes       = nodeTypes.ToArray()
            });
        }

        static void WriteFile(string projectRoot, string assetPath, string content)
        {
            var fullPath = Path.Combine(projectRoot, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, content, Encoding.UTF8);
            AssetDatabase.ImportAsset(assetPath);
        }
    }
}
