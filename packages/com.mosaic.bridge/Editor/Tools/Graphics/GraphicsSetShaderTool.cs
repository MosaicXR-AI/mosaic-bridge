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
                    "Assigns a shader to a material asset by name. " +
                    "After switching shaders, color property names change: URP/HDRP use '_BaseColor', Built-in uses '_Color'. " +
                    "Use material/set-property to update property values after a shader switch.",
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

            if (material.shader == null || material.shader.name != shader.name)
                return ToolResult<GraphicsSetShaderResult>.Fail(
                    $"Shader '{p.ShaderName}' was found but assignment failed — it may be unsupported by the active render pipeline.",
                    ErrorCodes.INTERNAL_ERROR);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            string colorProp = shader.name.StartsWith("Universal Render Pipeline") || shader.name.StartsWith("HDRP")
                ? "_BaseColor" : "_Color";

            return ToolResult<GraphicsSetShaderResult>.Ok(new GraphicsSetShaderResult
            {
                MaterialPath           = p.MaterialPath,
                MaterialName           = material.name,
                ShaderName             = shader.name,
                PreviousShaderName     = previousShader,
                SuggestedColorProperty = colorProp
            });
        }
    }
}
