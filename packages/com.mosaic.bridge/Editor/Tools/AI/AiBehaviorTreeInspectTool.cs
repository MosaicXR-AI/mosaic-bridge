using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AI
{
    public static class AiBehaviorTreeInspectTool
    {
        [MosaicTool("ai/behavior-tree-inspect",
                    "Inspects a behavior tree component on a GameObject and returns its structure, node count, and blackboard keys",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<AiBehaviorTreeInspectResult> Execute(AiBehaviorTreeInspectParams p)
        {
            if (string.IsNullOrWhiteSpace(p.GameObjectName))
                return ToolResult<AiBehaviorTreeInspectResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<AiBehaviorTreeInspectResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            // Find a MonoBehaviour whose type name ends with "BehaviorTree"
            MonoBehaviour btComponent = null;
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name.EndsWith("BehaviorTree"))
                {
                    btComponent = mb;
                    break;
                }
            }

            if (btComponent == null)
                return ToolResult<AiBehaviorTreeInspectResult>.Fail(
                    $"No BehaviorTree component found on '{p.GameObjectName}'",
                    ErrorCodes.NOT_FOUND);

            var btType = btComponent.GetType();

            // Read the script source from the MonoScript
            var monoScript = MonoScript.FromMonoBehaviour(btComponent);
            var scriptText = monoScript != null ? monoScript.text : "";

            // Parse tree structure from script
            var treeStructure = ParseTreeStructure(scriptText, btType.Name);

            // Count nodes by looking for "new BT_" patterns in script
            int nodeCount = CountNodesInScript(scriptText);

            // Extract blackboard keys (public fields that aren't internal)
            var blackboardKeys = new List<string>();
            var fields = btType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                // Skip Unity internal fields and the root node
                if (field.Name.StartsWith("_") || field.Name == "rootNode")
                    continue;
                blackboardKeys.Add(field.Name);
            }

            return ToolResult<AiBehaviorTreeInspectResult>.Ok(new AiBehaviorTreeInspectResult
            {
                TreeStructure  = treeStructure,
                NodeCount      = nodeCount,
                BlackboardKeys = blackboardKeys.ToArray()
            });
        }

        static string ParseTreeStructure(string scriptText, string typeName)
        {
            if (string.IsNullOrEmpty(scriptText))
                return $"[{typeName}] (script source unavailable)";

            var sb = new StringBuilder();
            sb.AppendLine($"[{typeName}]");

            // Parse node construction lines from Awake()
            var nodePattern = new Regex(@"new\s+(BT_\w+)\(\)");
            var namePattern = new Regex(@"\.NodeName\s*=\s*""([^""]+)""");
            var addChildPattern = new Regex(@"(\w+)\.AddChild\((\w+)\)");
            var setChildPattern = new Regex(@"(\w+)\.SetChild\((\w+)\)");
            var actionPattern = new Regex(@"new\s+(BT_Action_\w+)\(");
            var condPattern = new Regex(@"new\s+(BT_\w+)\(this\)");

            var nodeTypes = new Dictionary<string, string>();
            var nodeNames = new Dictionary<string, string>();
            var parentChild = new List<(string parent, string child)>();

            var lines = scriptText.Split('\n');
            foreach (var line in lines)
            {
                // Match: var X = new BT_Type();
                var varMatch = Regex.Match(line, @"var\s+(\w+)\s*=\s*new\s+(BT_\w+)");
                if (varMatch.Success)
                {
                    nodeTypes[varMatch.Groups[1].Value] = varMatch.Groups[2].Value;
                }

                // Match: X.NodeName = "name";
                var nameMatch = Regex.Match(line, @"(\w+)\.NodeName\s*=\s*""([^""]+)""");
                if (nameMatch.Success)
                {
                    nodeNames[nameMatch.Groups[1].Value] = nameMatch.Groups[2].Value;
                }

                // Match: parent.AddChild(child) or parent.SetChild(child)
                var childMatch = addChildPattern.Match(line);
                if (!childMatch.Success)
                    childMatch = setChildPattern.Match(line);
                if (childMatch.Success)
                {
                    parentChild.Add((childMatch.Groups[1].Value, childMatch.Groups[2].Value));
                }
            }

            // Build tree display
            if (nodeTypes.Count > 0)
            {
                // Find root (the variable assigned to rootNode or first declared)
                var rootVar = nodeTypes.Keys.FirstOrDefault();
                var rootAssign = Regex.Match(scriptText, @"rootNode\s*=\s*(\w+)");
                if (rootAssign.Success && nodeTypes.ContainsKey(rootAssign.Groups[1].Value))
                    rootVar = rootAssign.Groups[1].Value;

                if (rootVar != null)
                    EmitTreeDisplay(sb, rootVar, nodeTypes, nodeNames, parentChild, "  ", new HashSet<string>());
            }
            else
            {
                sb.AppendLine("  (unable to parse tree structure)");
            }

            return sb.ToString().TrimEnd();
        }

        static void EmitTreeDisplay(StringBuilder sb, string varName,
            Dictionary<string, string> types, Dictionary<string, string> names,
            List<(string parent, string child)> edges, string indent,
            HashSet<string> visited)
        {
            if (visited.Contains(varName)) return;
            visited.Add(varName);

            var typeName = types.ContainsKey(varName) ? types[varName] : "?";
            var nodeName = names.ContainsKey(varName) ? names[varName] : varName;
            sb.AppendLine($"{indent}{typeName} \"{nodeName}\"");

            foreach (var edge in edges.Where(e => e.parent == varName))
            {
                EmitTreeDisplay(sb, edge.child, types, names, edges, indent + "  ", visited);
            }
        }

        static int CountNodesInScript(string scriptText)
        {
            if (string.IsNullOrEmpty(scriptText)) return 0;
            // Count "new BT_" instantiations in the Awake method
            return Regex.Matches(scriptText, @"new\s+BT_\w+").Count;
        }
    }
}
