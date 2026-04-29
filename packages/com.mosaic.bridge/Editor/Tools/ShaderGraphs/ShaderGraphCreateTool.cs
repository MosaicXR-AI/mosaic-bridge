using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public static class ShaderGraphCreateTool
    {
        [MosaicTool("shadergraph/create",
                    "Creates a new ShaderGraph asset (.shadergraph) with a URP Lit or Unlit template. " +
                    "ShaderType: 'Lit' (PBR — BaseColor, Normal, Metallic, Smoothness, Emission, Occlusion) " +
                    "or 'Unlit' (unlit — BaseColor only). " +
                    "Writes Unity ShaderGraph 14.x+ MultiJson format with block-based vertex/fragment contexts. " +
                    "After creating, use shadergraph/add-node to add nodes and shadergraph/connect to wire them.",
                    isReadOnly: false)]
        public static ToolResult<ShaderGraphCreateResult> Execute(ShaderGraphCreateParams p)
        {
            if (string.IsNullOrEmpty(p.Name))
                return ToolResult<ShaderGraphCreateResult>.Fail(
                    "Name is required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<ShaderGraphCreateResult>.Fail(
                    "Path is required", ErrorCodes.INVALID_PARAM);

            // Auto-append extension if missing
            if (!p.Path.EndsWith(".shadergraph"))
                p.Path = p.Path + ".shadergraph";

            // Validate shader type
            string shaderType = p.ShaderType ?? "Lit";
            if (shaderType != "Lit" && shaderType != "Unlit")
                return ToolResult<ShaderGraphCreateResult>.Fail(
                    $"ShaderType must be 'Lit' or 'Unlit', got '{shaderType}'",
                    ErrorCodes.INVALID_PARAM);

            bool existed = AssetDatabase.AssetPathExists(p.Path);
            if (existed && !p.OverwriteExisting)
                return ToolResult<ShaderGraphCreateResult>.Fail(
                    $"ShaderGraph already exists at '{p.Path}'. Set OverwriteExisting=true to replace it.",
                    ErrorCodes.CONFLICT);

            // Ensure directory exists
            string fullPath = ShaderGraphJsonHelper.GetFullPath(p.Path);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Write the template JSON
            string template = shaderType == "Unlit"
                ? GetUnlitTemplate(p.Name)
                : GetLitTemplate(p.Name);

            File.WriteAllText(fullPath, template);
            try
            {
                AssetDatabase.ImportAsset(p.Path, ImportAssetOptions.ForceUpdate);
            }
            catch (Exception)
            {
                // Unity's ShaderGraph importer may throw on minimal templates.
                // The file is written; proceed and let Unity auto-import on next refresh.
            }

            var guid = AssetDatabase.AssetPathToGUID(p.Path);

            return ToolResult<ShaderGraphCreateResult>.Ok(new ShaderGraphCreateResult
            {
                Path        = p.Path,
                Name        = p.Name,
                ShaderType  = shaderType,
                Guid        = guid,
                Overwritten = existed
            });
        }

        private static string NewGuid() => Guid.NewGuid().ToString("D");

        private static string GetLitTemplate(string name)
        {
            var graphId      = NewGuid();
            var targetId     = NewGuid();
            var subTargetId  = NewGuid();

            // Vertex blocks
            var posId  = NewGuid(); var posSlotId  = NewGuid();
            var nrmId  = NewGuid(); var nrmSlotId  = NewGuid();
            var tanId  = NewGuid(); var tanSlotId  = NewGuid();

            // Fragment blocks — Lit
            var bcId   = NewGuid(); var bcSlotId   = NewGuid();
            var ntId   = NewGuid(); var ntSlotId   = NewGuid();
            var mtId   = NewGuid(); var mtSlotId   = NewGuid();
            var smId   = NewGuid(); var smSlotId   = NewGuid();
            var emId   = NewGuid(); var emSlotId   = NewGuid();
            var ocId   = NewGuid(); var ocSlotId   = NewGuid();

            var sb = new StringBuilder();
            sb.Append(GraphDataBlock(graphId, targetId, name,
                new[] { posId, nrmId, tanId },
                new[] { bcId, ntId, mtId, smId, emId, ocId }));
            sb.AppendLine();
            sb.Append(UniversalTargetBlock(targetId, subTargetId));
            sb.AppendLine();
            sb.Append(LitSubTargetBlock(subTargetId));
            sb.AppendLine();
            // Vertex blocks
            sb.Append(BlockNode(posId, posSlotId, "VertexDescription.Position", "Vector3MaterialSlot", 9, "Position",  1));
            sb.AppendLine();
            sb.Append(BlockNode(nrmId, nrmSlotId, "VertexDescription.Normal",   "Vector3MaterialSlot", 10, "Normal",   1));
            sb.AppendLine();
            sb.Append(BlockNode(tanId, tanSlotId, "VertexDescription.Tangent",  "Vector3MaterialSlot", 11, "Tangent",  1));
            sb.AppendLine();
            // Fragment blocks — Lit
            sb.Append(BlockNode(bcId,  bcSlotId,  "SurfaceDescription.BaseColor",   "ColorRGBMaterialSlot", 0,  "BaseColor",   2));
            sb.AppendLine();
            sb.Append(BlockNode(ntId,  ntSlotId,  "SurfaceDescription.NormalTS",    "Vector3MaterialSlot",  2,  "Normal",      2));
            sb.AppendLine();
            sb.Append(BlockNode(mtId,  mtSlotId,  "SurfaceDescription.Metallic",    "Vector1MaterialSlot",  6,  "Metallic",    2));
            sb.AppendLine();
            sb.Append(BlockNode(smId,  smSlotId,  "SurfaceDescription.Smoothness",  "Vector1MaterialSlot",  3,  "Smoothness",  2));
            sb.AppendLine();
            sb.Append(BlockNode(emId,  emSlotId,  "SurfaceDescription.Emission",    "ColorRGBMaterialSlot", 4,  "Emission",    2));
            sb.AppendLine();
            sb.Append(BlockNode(ocId,  ocSlotId,  "SurfaceDescription.Occlusion",   "Vector1MaterialSlot",  7,  "Occlusion",   2));
            return sb.ToString();
        }

        private static string GetUnlitTemplate(string name)
        {
            var graphId      = NewGuid();
            var targetId     = NewGuid();
            var subTargetId  = NewGuid();

            var posId  = NewGuid(); var posSlotId  = NewGuid();
            var nrmId  = NewGuid(); var nrmSlotId  = NewGuid();
            var tanId  = NewGuid(); var tanSlotId  = NewGuid();
            var bcId   = NewGuid(); var bcSlotId   = NewGuid();

            var sb = new StringBuilder();
            sb.Append(GraphDataBlock(graphId, targetId, name,
                new[] { posId, nrmId, tanId },
                new[] { bcId }));
            sb.AppendLine();
            sb.Append(UniversalTargetBlock(targetId, subTargetId));
            sb.AppendLine();
            sb.Append(UnlitSubTargetBlock(subTargetId));
            sb.AppendLine();
            sb.Append(BlockNode(posId, posSlotId, "VertexDescription.Position", "Vector3MaterialSlot", 9,  "Position", 1));
            sb.AppendLine();
            sb.Append(BlockNode(nrmId, nrmSlotId, "VertexDescription.Normal",   "Vector3MaterialSlot", 10, "Normal",   1));
            sb.AppendLine();
            sb.Append(BlockNode(tanId, tanSlotId, "VertexDescription.Tangent",  "Vector3MaterialSlot", 11, "Tangent",  1));
            sb.AppendLine();
            sb.Append(BlockNode(bcId,  bcSlotId,  "SurfaceDescription.BaseColor", "ColorRGBMaterialSlot", 0, "BaseColor", 2));
            return sb.ToString();
        }

        // ── Template block builders ─────────────────────────────────────────────

        private static string GraphDataBlock(
            string graphId, string targetId, string name,
            string[] vertexBlockIds, string[] fragmentBlockIds)
        {
            var vertBlocks = string.Join(",\n", System.Array.ConvertAll(vertexBlockIds,   id => $"        {{\"m_Id\": \"{id}\"}}"));
            var fragBlocks = string.Join(",\n", System.Array.ConvertAll(fragmentBlockIds, id => $"        {{\"m_Id\": \"{id}\"}}"));
            var allNodes   = string.Join(",\n", System.Array.ConvertAll(
                Concat(vertexBlockIds, fragmentBlockIds), id => $"    {{\"m_Id\": \"{id}\"}}"));

            return $@"{{
    ""m_SGVersion"": 3,
    ""m_Type"": ""UnityEditor.ShaderGraph.GraphData"",
    ""m_ObjectId"": ""{graphId}"",
    ""m_Properties"": [],
    ""m_Keywords"": [],
    ""m_Dropdowns"": [],
    ""m_CategoryData"": [],
    ""m_Nodes"": [
{allNodes}
    ],
    ""m_SerializedNodes"": [],
    ""m_GroupDatas"": [],
    ""m_StickyNoteDatas"": [],
    ""m_Edges"": [],
    ""m_VertexContext"": {{
        ""m_Position"": {{""x"": 0.0, ""y"": 0.0}},
        ""m_Blocks"": [
{vertBlocks}
        ]
    }},
    ""m_FragmentContext"": {{
        ""m_Position"": {{""x"": 200.0, ""y"": 0.0}},
        ""m_Blocks"": [
{fragBlocks}
        ]
    }},
    ""m_PreviewData"": {{""serializedMesh"": {{""mesh"": null}}, ""preventRotation"": false}},
    ""m_Path"": ""Shader Graphs/{EscapeJson(name)}"",
    ""m_GraphPrecision"": 1,
    ""m_PreviewMode"": 2,
    ""m_OutputNode"": {{""m_Id"": """"}},
    ""m_ActiveTargets"": [
        {{""m_Id"": ""{targetId}""}}
    ]
}}";
        }

        private static string UniversalTargetBlock(string targetId, string subTargetId) => $@"{{
    ""m_SGVersion"": 3,
    ""m_Type"": ""UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget"",
    ""m_ObjectId"": ""{targetId}"",
    ""m_SubTargets"": [{{""m_Id"": ""{subTargetId}""}}],
    ""m_AllowMaterialOverride"": false,
    ""m_AlphaClip"": false,
    ""m_CastShadows"": true,
    ""m_ReceiveShadows"": true,
    ""m_AdditionalMotionVectorMode"": 0,
    ""m_AlembicMotionVectors"": false,
    ""m_SupportsLODCrossFade"": false,
    ""m_CustomEditorGUI"": """",
    ""m_SupportVFXGraph"": false
}}";

        private static string LitSubTargetBlock(string subTargetId) => $@"{{
    ""m_SGVersion"": 3,
    ""m_Type"": ""UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget"",
    ""m_ObjectId"": ""{subTargetId}"",
    ""m_WorkflowMode"": 1,
    ""m_NormalDropOffSpace"": 0,
    ""m_ClearCoat"": false,
    ""m_BlendModePreserveSpecular"": false
}}";

        private static string UnlitSubTargetBlock(string subTargetId) => $@"{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget"",
    ""m_ObjectId"": ""{subTargetId}""
}}";

        private static string BlockNode(
            string nodeId, string slotId, string descriptorName,
            string slotTypeName, int slotIndex, string displayName, int stageCap)
        {
            return $@"{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.BlockNode"",
    ""m_ObjectId"": ""{nodeId}"",
    ""m_Group"": {{""m_Id"": """"}},
    ""m_Name"": ""{descriptorName}"",
    ""m_DrawState"": {{
        ""m_Expanded"": true,
        ""m_Position"": {{""serializedVersion"": ""2"", ""x"": 0.0, ""y"": 0.0, ""width"": 200.0, ""height"": 100.0}}
    }},
    ""m_Slots"": [{{""m_Id"": ""{slotId}""}}],
    ""sgVersion"": 0
}}

{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.{slotTypeName}"",
    ""m_ObjectId"": ""{slotId}"",
    ""m_Id"": {slotIndex},
    ""m_DisplayName"": ""{displayName}"",
    ""m_SlotType"": 0,
    ""m_Hidden"": false,
    ""m_ShaderOutputName"": ""{displayName}"",
    ""m_StageCapability"": {stageCap},
    ""m_Value"": {{""x"": 0.0, ""y"": 0.0, ""z"": 0.0}},
    ""m_DefaultValue"": {{""x"": 0.0, ""y"": 0.0, ""z"": 0.0}},
    ""m_Labels"": []
}}";
        }

        private static string[] Concat(string[] a, string[] b)
        {
            var result = new string[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }

        private static string EscapeJson(string s) =>
            s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
    }
}
