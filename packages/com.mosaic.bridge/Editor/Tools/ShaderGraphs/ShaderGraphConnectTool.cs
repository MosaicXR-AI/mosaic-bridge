using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public static class ShaderGraphConnectTool
    {
        [MosaicTool("shadergraph/connect",
                    "Creates an edge between two nodes in a ShaderGraph. " +
                    "Provide OutputNodeId + OutputSlotId (from the source node) and InputNodeId + InputSlotId " +
                    "(on the destination node). Both NodeIds and SlotIds are returned by shadergraph/add-node. " +
                    "Example: connect a Multiply node's Out (slot 2) into a Master node's Albedo (slot 0).",
                    isReadOnly: false)]
        public static ToolResult<ShaderGraphConnectResult> Execute(ShaderGraphConnectParams p)
        {
            if (string.IsNullOrEmpty(p.GraphPath))
                return ToolResult<ShaderGraphConnectResult>.Fail("GraphPath is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.OutputNodeId))
                return ToolResult<ShaderGraphConnectResult>.Fail("OutputNodeId is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.InputNodeId))
                return ToolResult<ShaderGraphConnectResult>.Fail("InputNodeId is required", ErrorCodes.INVALID_PARAM);

            var graph = ShaderGraphJsonHelper.ReadGraph(p.GraphPath);
            if (graph == null)
                return ToolResult<ShaderGraphConnectResult>.Fail(
                    $"ShaderGraph not found at '{p.GraphPath}'", ErrorCodes.NOT_FOUND);

            // Validate that both nodes exist in the graph
            if (!NodeExists(graph, p.OutputNodeId))
                return ToolResult<ShaderGraphConnectResult>.Fail(
                    $"Output node '{p.OutputNodeId}' not found in graph. " +
                    "Use shadergraph/info to list existing node IDs.",
                    ErrorCodes.NOT_FOUND);
            if (!NodeExists(graph, p.InputNodeId))
                return ToolResult<ShaderGraphConnectResult>.Fail(
                    $"Input node '{p.InputNodeId}' not found in graph. " +
                    "Use shadergraph/info to list existing node IDs.",
                    ErrorCodes.NOT_FOUND);

            // Build edge JSON
            var edgeObj = new JObject
            {
                ["m_OutputSlot"] = new JObject
                {
                    ["m_Node"]   = new JObject { ["m_Id"] = p.OutputNodeId },
                    ["m_SlotId"] = p.OutputSlotId
                },
                ["m_InputSlot"] = new JObject
                {
                    ["m_Node"]   = new JObject { ["m_Id"] = p.InputNodeId },
                    ["m_SlotId"] = p.InputSlotId
                }
            };

            string edgeJson = edgeObj.ToString(Formatting.None);

            // Append to m_SerializedEdges
            var edgesArray = graph["m_SerializedEdges"] as JArray ?? graph["m_Edges"] as JArray;
            if (edgesArray == null)
            {
                edgesArray = new JArray();
                graph["m_SerializedEdges"] = edgesArray;
            }
            edgesArray.Add(new JValue(edgeJson));

            ShaderGraphJsonHelper.WriteGraph(p.GraphPath, graph);
            AssetDatabase.ImportAsset(p.GraphPath, ImportAssetOptions.ForceUpdate);

            return ToolResult<ShaderGraphConnectResult>.Ok(new ShaderGraphConnectResult
            {
                GraphPath    = p.GraphPath,
                OutputNodeId = p.OutputNodeId,
                OutputSlotId = p.OutputSlotId,
                InputNodeId  = p.InputNodeId,
                InputSlotId  = p.InputSlotId,
                TotalEdges   = ShaderGraphJsonHelper.CountEdges(graph)
            });
        }

        private static bool NodeExists(JObject graph, string nodeId)
        {
            var nodes = graph["m_SerializedNodes"] as JArray
                     ?? graph["m_Nodes"] as JArray;
            if (nodes == null) return false;

            foreach (var token in nodes)
            {
                string json = token.Type == JTokenType.String
                    ? token.Value<string>()
                    : token.ToString();
                if (json != null && json.Contains(nodeId))
                    return true;
            }

            // Also check output node
            var outputNode = graph["m_OutputNode"];
            if (outputNode != null && outputNode["m_ObjectId"]?.Value<string>() == nodeId)
                return true;

            return false;
        }
    }
}
