using System.IO;
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
                    "Creates a new ShaderGraph asset with a Lit or Unlit template",
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
            AssetDatabase.ImportAsset(p.Path, ImportAssetOptions.ForceUpdate);

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

        private static string GetLitTemplate(string name)
        {
            // Minimal valid ShaderGraph JSON for a PBR/Lit graph.
            // Unity's ShaderGraph importer reads this and produces a working shader.
            return @"{
    ""m_SerializedNodes"": [],
    ""m_SerializedEdges"": [],
    ""m_SerializedProperties"": [],
    ""m_SGVersion"": 3,
    ""m_Type"": ""UnityEditor.ShaderGraph.GraphData"",
    ""m_ObjectId"": """",
    ""m_GraphPrecision"": 1,
    ""m_PreviewMode"": 2,
    ""m_OutputNode"": {
        ""m_Type"": ""UnityEditor.ShaderGraph.PBRMasterNode"",
        ""m_SGVersion"": 0,
        ""m_Name"": """ + EscapeJson(name) + @""",
        ""m_WorkflowMode"": 1,
        ""m_SurfaceType"": 0,
        ""m_AlphaMode"": 0
    },
    ""m_ActiveTargets"": [
        {
            ""m_Type"": ""UnityEditor.Rendering.Universal.UniversalTarget"",
            ""m_Active"": true,
            ""m_CustomEditorGUI"": """"
        }
    ],
    ""m_Path"": ""Shader Graphs/" + EscapeJson(name) + @"""
}";
        }

        private static string GetUnlitTemplate(string name)
        {
            return @"{
    ""m_SerializedNodes"": [],
    ""m_SerializedEdges"": [],
    ""m_SerializedProperties"": [],
    ""m_SGVersion"": 3,
    ""m_Type"": ""UnityEditor.ShaderGraph.GraphData"",
    ""m_ObjectId"": """",
    ""m_GraphPrecision"": 1,
    ""m_PreviewMode"": 2,
    ""m_OutputNode"": {
        ""m_Type"": ""UnityEditor.ShaderGraph.UnlitMasterNode"",
        ""m_SGVersion"": 0,
        ""m_Name"": """ + EscapeJson(name) + @""",
        ""m_SurfaceType"": 0,
        ""m_AlphaMode"": 0
    },
    ""m_ActiveTargets"": [
        {
            ""m_Type"": ""UnityEditor.Rendering.Universal.UniversalTarget"",
            ""m_Active"": true,
            ""m_CustomEditorGUI"": """"
        }
    ],
    ""m_Path"": ""Shader Graphs/" + EscapeJson(name) + @"""
}";
        }

        private static string EscapeJson(string s)
        {
            return s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
        }
    }
}
