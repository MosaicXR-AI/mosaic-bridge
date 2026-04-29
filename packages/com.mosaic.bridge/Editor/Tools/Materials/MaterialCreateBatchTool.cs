using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Materials
{
    public static class MaterialCreateBatchTool
    {
        [MosaicTool("material/create-batch",
                    "Creates multiple material assets in one call. " +
                    "Each entry needs a Path (e.g. 'Assets/Mats/Wood.mat') and optionally a ShaderName. " +
                    "Auto-detects render pipeline when ShaderName is omitted (same logic as material/create). " +
                    "Returns separate Created/Skipped/Failed lists so the caller can act on partial failures.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<MaterialCreateBatchResult> Execute(MaterialCreateBatchParams p)
        {
            if (p.Materials == null || p.Materials.Length == 0)
                return ToolResult<MaterialCreateBatchResult>.Fail(
                    "Materials array is required and must not be empty", ErrorCodes.INVALID_PARAM);

            string pipeline  = MaterialCreateTool.DetectRenderPipeline();
            string colorProp = (pipeline == "URP" || pipeline == "HDRP") ? "_BaseColor" : "_Color";

            var created = new List<MaterialCreateBatchResultEntry>();
            var skipped = new List<MaterialCreateBatchResultEntry>();
            var failed  = new List<MaterialCreateBatchResultEntry>();

            foreach (var entry in p.Materials)
            {
                if (string.IsNullOrEmpty(entry?.Path))
                {
                    failed.Add(new MaterialCreateBatchResultEntry { Path = entry?.Path, Error = "Path is required" });
                    continue;
                }

                string path = entry.Path.EndsWith(".mat") ? entry.Path : entry.Path + ".mat";

                bool existed = AssetDatabase.AssetPathExists(path);
                if (existed && !p.OverwriteExisting)
                {
                    skipped.Add(new MaterialCreateBatchResultEntry { Path = path, ShaderName = entry.ShaderName });
                    continue;
                }

                string shaderName = entry.ShaderName ?? GetDefaultShaderForPipeline(pipeline);
                var shader = UnityEngine.Shader.Find(shaderName);
                if (shader == null)
                {
                    shaderName = "Standard";
                    shader = UnityEngine.Shader.Find(shaderName);
                }
                if (shader == null)
                {
                    failed.Add(new MaterialCreateBatchResultEntry
                    {
                        Path  = path,
                        Error = $"Shader '{entry.ShaderName ?? shaderName}' not found (pipeline: {pipeline})"
                    });
                    continue;
                }

                try
                {
                    var absoluteDir = Path.GetDirectoryName(
                        Path.Combine(Application.dataPath, "..", path));
                    if (!string.IsNullOrEmpty(absoluteDir))
                        Directory.CreateDirectory(absoluteDir);

                    var mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                    created.Add(new MaterialCreateBatchResultEntry { Path = path, ShaderName = shader.name });
                }
                catch (System.Exception ex)
                {
                    failed.Add(new MaterialCreateBatchResultEntry { Path = path, Error = ex.Message });
                }
            }

            if (created.Count > 0)
                AssetDatabase.SaveAssets();

            return ToolResult<MaterialCreateBatchResult>.Ok(new MaterialCreateBatchResult
            {
                Created                = created.ToArray(),
                Skipped                = skipped.ToArray(),
                Failed                 = failed.ToArray(),
                RenderPipeline         = pipeline,
                SuggestedColorProperty = colorProp
            });
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
    }
}
