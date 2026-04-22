using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public static class ShaderGraphAddNodeTool
    {
        [MosaicTool("shadergraph/add-node",
                    "Adds a node to an existing ShaderGraph (.shadergraph) file. " +
                    "Supported node types (NodeType aliases): " +
                    "Math — add, subtract, multiply, divide, power, lerp, clamp, saturate, abs, negate, sqrt, floor, ceil, frac, step, smoothstep, remap; " +
                    "Utility — split, combine, swizzle, fresnel; " +
                    "Input — float, vector2, vector3, vector4, color, uv, time, position, normal, viewdir; " +
                    "Texture — sampletexture2d, samplecubemap. " +
                    "Returns NodeId (GUID) and Slots — use NodeId in shadergraph/connect to wire the node.",
                    isReadOnly: false)]
        public static ToolResult<ShaderGraphAddNodeResult> Execute(ShaderGraphAddNodeParams p)
        {
            if (string.IsNullOrEmpty(p.GraphPath))
                return ToolResult<ShaderGraphAddNodeResult>.Fail(
                    "GraphPath is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.NodeType))
                return ToolResult<ShaderGraphAddNodeResult>.Fail(
                    "NodeType is required", ErrorCodes.INVALID_PARAM);

            var def = ShaderGraphNodeRegistry.Get(p.NodeType);
            if (def == null)
            {
                var all = string.Join(", ", ShaderGraphNodeRegistry.AllAliases().Take(20)) + "...";
                return ToolResult<ShaderGraphAddNodeResult>.Fail(
                    $"Unknown NodeType '{p.NodeType}'. Supported aliases (first 20): {all}",
                    ErrorCodes.INVALID_PARAM);
            }

            var graph = ShaderGraphJsonHelper.ReadGraph(p.GraphPath);
            if (graph == null)
                return ToolResult<ShaderGraphAddNodeResult>.Fail(
                    $"ShaderGraph not found at '{p.GraphPath}'", ErrorCodes.NOT_FOUND);

            // Generate a unique node ID (without hyphens — Unity uses compact GUIDs)
            var nodeId = Guid.NewGuid().ToString("N");
            string displayName = !string.IsNullOrEmpty(p.NodeName) ? p.NodeName : def.DisplayName;

            float posX = p.Position?.Length > 0 ? p.Position[0] : 0f;
            float posY = p.Position?.Length > 1 ? p.Position[1] : 0f;

            // Build slot JSON array
            var slotsJson = new JArray();
            foreach (var slot in def.Slots)
            {
                var slotObj = new JObject
                {
                    ["m_Id"]             = slot.Id,
                    ["m_DisplayName"]    = slot.DisplayName,
                    ["m_SlotType"]       = slot.SlotType,
                    ["m_Priority"]       = int.MaxValue,
                    ["m_Hidden"]         = false,
                    ["m_ShaderOutputName"] = slot.DisplayName.Replace(" ", "_"),
                    ["m_StageCapability"]  = 3,
                    ["m_Value"]          = 0.0,
                    ["m_DefaultValue"]   = 0.0,
                    ["m_Labels"]         = new JArray()
                };

                // For Float nodes, apply the default value to the output slot
                if (p.DefaultValue.HasValue && slot.SlotType == 1)
                {
                    slotObj["m_Value"]        = p.DefaultValue.Value;
                    slotObj["m_DefaultValue"] = p.DefaultValue.Value;
                }
                slotsJson.Add(slotObj);
            }

            // Build node JSON object
            var nodeObj = new JObject
            {
                ["m_Type"]        = def.TypeName,
                ["m_SGVersion"]   = 0,
                ["m_ObjectId"]    = nodeId,
                ["m_Group"]       = new JObject { ["m_Id"] = "" },
                ["m_Name"]        = displayName,
                ["m_DrawState"]   = new JObject
                {
                    ["m_Expanded"] = true,
                    ["m_Position"] = new JObject
                    {
                        ["serializedVersion"] = "2",
                        ["x"] = posX, ["y"] = posY,
                        ["width"] = 130f, ["height"] = 100f
                    }
                },
                ["m_Slots"]          = slotsJson,
                ["m_Precision"]      = 0,
                ["m_PreviewExpanded"] = true,
                ["m_PreviewMode"]    = 0,
                ["m_CustomColors"]   = new JObject { ["m_SerializableColors"] = new JArray() }
            };

            // Optional: default value field for Float/Vector nodes
            if (p.DefaultValue.HasValue)
                nodeObj["m_Value"] = p.DefaultValue.Value;

            // Nodes are serialized as escaped JSON strings in the array
            string nodeJson = nodeObj.ToString(Formatting.None);

            // Append to m_SerializedNodes (try both key variants)
            var nodesArray = graph["m_SerializedNodes"] as JArray ?? graph["m_Nodes"] as JArray;
            if (nodesArray == null)
            {
                nodesArray = new JArray();
                graph["m_SerializedNodes"] = nodesArray;
            }
            nodesArray.Add(new JValue(nodeJson));

            ShaderGraphJsonHelper.WriteGraph(p.GraphPath, graph);
            AssetDatabase.ImportAsset(p.GraphPath, ImportAssetOptions.ForceUpdate);

            // Build result slots
            var resultSlots = def.Slots.Select(s => new ShaderGraphNodeSlot
            {
                Id          = s.Id,
                DisplayName = s.DisplayName,
                Direction   = s.SlotType == 0 ? "Input" : "Output"
            }).ToArray();

            return ToolResult<ShaderGraphAddNodeResult>.Ok(new ShaderGraphAddNodeResult
            {
                GraphPath  = p.GraphPath,
                NodeId     = nodeId,
                NodeType   = def.TypeName,
                NodeName   = displayName,
                TotalNodes = ShaderGraphJsonHelper.CountNodes(graph),
                Slots      = resultSlots
            });
        }
    }
}
