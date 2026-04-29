using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Materials
{
    public static class MaterialCreateTool
    {
        [MosaicTool("material/create",
                    "Creates a new material asset at the specified project path. " +
                    "Auto-detects the active render pipeline when ShaderName is omitted: " +
                    "URP projects get 'Universal Render Pipeline/Lit', HDRP gets 'HDRP/Lit', Built-in gets 'Standard'. " +
                    "Check SuggestedColorProperty in the result to know whether to use '_BaseColor' (URP/HDRP) or '_Color' (Built-in).",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<MaterialCreateResult> Execute(MaterialCreateParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<MaterialCreateResult>.Fail(
                    "Path is required", ErrorCodes.INVALID_PARAM);

            bool existed = AssetDatabase.AssetPathExists(p.Path);
            if (existed && !p.OverwriteExisting)
                return ToolResult<MaterialCreateResult>.Fail(
                    $"Material already exists at '{p.Path}'. Set OverwriteExisting=true to replace it.",
                    ErrorCodes.CONFLICT);

            string pipelineName  = DetectRenderPipeline();
            string shaderNameUsed = p.ShaderName ?? GetDefaultShaderForPipeline(pipelineName);

            var shader = Shader.Find(shaderNameUsed);
            if (shader == null)
            {
                // Explicit shader name supplied but not found — give a clear error
                if (!string.IsNullOrEmpty(p.ShaderName))
                    return ToolResult<MaterialCreateResult>.Fail(
                        $"Shader '{p.ShaderName}' not found. Active pipeline: {pipelineName}. " +
                        $"Try 'Universal Render Pipeline/Lit' for URP or 'Standard' for Built-in.",
                        ErrorCodes.INVALID_PARAM);

                // Auto-detected shader not found — pipeline mismatch or SRP without URP package
                shaderNameUsed = "Standard";
                shader = Shader.Find(shaderNameUsed);
            }
            if (shader == null)
                return ToolResult<MaterialCreateResult>.Fail(
                    $"Could not find any usable shader. Active pipeline: {pipelineName}",
                    ErrorCodes.INVALID_PARAM);

            var absoluteDir = Path.GetDirectoryName(
                Path.Combine(Application.dataPath, "..", p.Path));
            if (!string.IsNullOrEmpty(absoluteDir))
                Directory.CreateDirectory(absoluteDir);

            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, p.Path);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(p.Path);
            string colorProp = GetSuggestedColorProperty(shaderNameUsed);

            return ToolResult<MaterialCreateResult>.Ok(new MaterialCreateResult
            {
                Path                   = p.Path,
                ShaderName             = shaderNameUsed,
                Guid                   = guid,
                Overwritten            = existed,
                RenderPipeline         = pipelineName,
                SuggestedColorProperty = colorProp
            });
        }

        internal static string DetectRenderPipeline()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null)
                return "BuiltIn";
            string typeName = pipeline.GetType().Name;
            if (typeName.Contains("Universal") || typeName.Contains("URP"))
                return "URP";
            if (typeName.Contains("HighDefinition") || typeName.Contains("HDRP"))
                return "HDRP";
            return "SRP";
        }

        private static string GetDefaultShaderForPipeline(string pipeline)
        {
            switch (pipeline)
            {
                case "URP":  return "Universal Render Pipeline/Lit";
                case "HDRP": return "HDRP/Lit";
                default:     return "Standard";
            }
        }

        private static string GetSuggestedColorProperty(string shaderName)
        {
            if (shaderName.StartsWith("Universal Render Pipeline") || shaderName.StartsWith("HDRP"))
                return "_BaseColor";
            return "_Color";
        }
    }
}
