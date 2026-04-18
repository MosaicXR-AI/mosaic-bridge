#if MOSAIC_HAS_VISUALSCRIPTING
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Unity.VisualScripting;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.VisualScripting
{
    public static class VisualScriptingAddNodeTool
    {
        [MosaicTool("visualscripting/add_node",
                    "Add a node (unit) to an existing Script Graph asset by type name",
                    isReadOnly: false,
                    category: "visualscripting")]
        public static ToolResult<VisualScriptingAddNodeResult> Execute(VisualScriptingAddNodeParams p)
        {
            var graphAsset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(p.GraphPath);
            if (graphAsset == null)
                return ToolResult<VisualScriptingAddNodeResult>.Fail(
                    $"Script Graph asset not found at '{p.GraphPath}'.",
                    ErrorCodes.NOT_FOUND);

            var graph = graphAsset.graph;
            if (graph == null)
                return ToolResult<VisualScriptingAddNodeResult>.Fail(
                    "Script Graph has no graph data.",
                    ErrorCodes.INTERNAL_ERROR);

            // Try to resolve the node type
            var unit = ResolveUnit(p.NodeType);
            if (unit == null)
                return ToolResult<VisualScriptingAddNodeResult>.Fail(
                    $"Could not resolve node type '{p.NodeType}'. Try a full type name like 'UnityEngine.Debug' with method 'Log', or a unit type name.",
                    ErrorCodes.NOT_FOUND);

            // Set position
            var pos = Vector2.zero;
            if (p.Position != null && p.Position.Length >= 2)
                pos = new Vector2(p.Position[0], p.Position[1]);
            unit.position = pos;

            // Add to graph
            graph.units.Add(unit);

            EditorUtility.SetDirty(graphAsset);
            AssetDatabase.SaveAssets();

            return ToolResult<VisualScriptingAddNodeResult>.Ok(new VisualScriptingAddNodeResult
            {
                GraphPath = p.GraphPath,
                NodeType = unit.GetType().Name,
                Position = new[] { pos.x, pos.y },
                NodeDescription = unit.ToString(),
                TotalNodeCount = graph.units.Count
            });
        }

        private static IUnit ResolveUnit(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType))
                return null;

            // Try common patterns like "Debug.Log" => InvokeMember for Debug.Log
            var dotIndex = nodeType.LastIndexOf('.');
            if (dotIndex > 0)
            {
                var typePart = nodeType.Substring(0, dotIndex);
                var memberPart = nodeType.Substring(dotIndex + 1);

                // Try to find the type
                var type = FindType(typePart);
                if (type != null)
                {
                    // Try to create an InvokeMember unit for this member
                    var member = type.GetMember(memberPart,
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                        .FirstOrDefault();

                    if (member is MethodInfo method)
                    {
                        return new InvokeMember(new Member(type, method));
                    }

                    if (member is PropertyInfo prop)
                    {
                        return new GetMember(new Member(type, prop));
                    }
                }
            }

            // Try as a direct Unit type
            var unitType = FindType(nodeType);
            if (unitType != null && typeof(IUnit).IsAssignableFrom(unitType))
            {
                return (IUnit)Activator.CreateInstance(unitType);
            }

            // Try with "Unit" suffix
            unitType = FindType(nodeType + "Unit");
            if (unitType != null && typeof(IUnit).IsAssignableFrom(unitType))
            {
                return (IUnit)Activator.CreateInstance(unitType);
            }

            return null;
        }

        private static Type FindType(string name)
        {
            // Try common Unity namespaces
            var prefixes = new[]
            {
                "", "UnityEngine.", "Unity.VisualScripting.", "System."
            };

            foreach (var prefix in prefixes)
            {
                var fullName = prefix + name;
                var type = Type.GetType(fullName);
                if (type != null) return type;

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(fullName);
                    if (type != null) return type;
                }
            }

            return null;
        }
    }
}
#endif
