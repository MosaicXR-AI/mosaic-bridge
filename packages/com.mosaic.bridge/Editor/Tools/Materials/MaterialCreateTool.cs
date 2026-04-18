using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Materials
{
    public static class MaterialCreateTool
    {
        [MosaicTool("material/create",
                    "Creates a new material asset at the specified project path",
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

            string shaderNameUsed = p.ShaderName ?? "Standard";
            var shader = Shader.Find(shaderNameUsed);
            if (shader == null)
            {
                shaderNameUsed = "Standard";
                shader = Shader.Find(shaderNameUsed);
            }
            if (shader == null)
                return ToolResult<MaterialCreateResult>.Fail(
                    $"Shader '{p.ShaderName}' not found and fallback 'Standard' is also unavailable",
                    ErrorCodes.INVALID_PARAM);

            var absoluteDir = Path.GetDirectoryName(
                Path.Combine(Application.dataPath, "..", p.Path));
            if (!string.IsNullOrEmpty(absoluteDir))
                Directory.CreateDirectory(absoluteDir);

            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, p.Path);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(p.Path);

            return ToolResult<MaterialCreateResult>.Ok(new MaterialCreateResult
            {
                Path        = p.Path,
                ShaderName  = shaderNameUsed,
                Guid        = guid,
                Overwritten = existed
            });
        }
    }
}
