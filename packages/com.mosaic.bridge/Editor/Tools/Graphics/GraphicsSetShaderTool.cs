using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Graphics
{
    public static class GraphicsSetShaderTool
    {
        [MosaicTool("graphics/set-shader",
                    "Assigns a shader to a material asset",
                    isReadOnly: false)]
        public static ToolResult<GraphicsSetShaderResult> SetShader(GraphicsSetShaderParams p)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(p.MaterialPath);
            if (material == null)
                return ToolResult<GraphicsSetShaderResult>.Fail(
                    $"Material not found at '{p.MaterialPath}'", ErrorCodes.NOT_FOUND);

            var shader = Shader.Find(p.ShaderName);
            if (shader == null)
                return ToolResult<GraphicsSetShaderResult>.Fail(
                    $"Shader '{p.ShaderName}' not found", ErrorCodes.NOT_FOUND,
                    "Check the shader name is correct. Common shaders: 'Standard', 'Universal Render Pipeline/Lit', 'HDRP/Lit'");

            string previousShader = material.shader != null ? material.shader.name : null;

            Undo.RecordObject(material, "Mosaic: Set Shader");
            material.shader = shader;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            return ToolResult<GraphicsSetShaderResult>.Ok(new GraphicsSetShaderResult
            {
                MaterialPath = p.MaterialPath,
                MaterialName = material.name,
                ShaderName = shader.name,
                PreviousShaderName = previousShader
            });
        }
    }
}
