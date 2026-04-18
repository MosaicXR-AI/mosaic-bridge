using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Compute
{
    public static class ComputeTextureProcessTool
    {
        [MosaicTool("compute/texture-process",
                    "Applies GPU texture operations (blur, diffuse, decay) via inline compute shaders",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ComputeTextureProcessResult> Execute(ComputeTextureProcessParams p)
        {
            if (string.IsNullOrEmpty(p.Operation))
                return ToolResult<ComputeTextureProcessResult>.Fail(
                    "Operation is required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.SourceTexturePath))
                return ToolResult<ComputeTextureProcessResult>.Fail(
                    "SourceTexturePath is required", ErrorCodes.INVALID_PARAM);

            string op = p.Operation.ToLowerInvariant();
            if (op != "blur" && op != "diffuse" && op != "decay")
                return ToolResult<ComputeTextureProcessResult>.Fail(
                    "Operation must be one of: blur, diffuse, decay",
                    ErrorCodes.INVALID_PARAM);

            var sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(p.SourceTexturePath);
            if (sourceTexture == null)
                return ToolResult<ComputeTextureProcessResult>.Fail(
                    $"Texture not found at '{p.SourceTexturePath}'", ErrorCodes.NOT_FOUND);

            if (!sourceTexture.isReadable)
                return ToolResult<ComputeTextureProcessResult>.Fail(
                    "Source texture is not readable. Enable Read/Write in import settings.",
                    ErrorCodes.INVALID_PARAM);

            int width = sourceTexture.width;
            int height = sourceTexture.height;

            string outputPath = string.IsNullOrEmpty(p.OutputPath)
                ? $"Assets/Generated/Textures/{Path.GetFileNameWithoutExtension(p.SourceTexturePath)}_{op}.png"
                : p.OutputPath;

            string absoluteOutDir = Path.GetDirectoryName(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputPath)));
            if (!string.IsNullOrEmpty(absoluteOutDir))
                Directory.CreateDirectory(absoluteOutDir);

            int radius = Math.Max(1, Math.Min(p.Radius, 32));
            float decayRate = Mathf.Clamp(p.DecayRate, 0f, 1f);

            string shaderSource = GenerateShaderSource(op, radius, decayRate);

            ComputeShader shader = null;
            RenderTexture srcRT = null;
            RenderTexture dstRT = null;

            try
            {
                // Create inline compute shader via temporary file
                string tempDir = Path.Combine(Application.dataPath, "MosaicTemp");
                Directory.CreateDirectory(tempDir);
                string tempShaderPath = Path.Combine(tempDir, "_mosaic_tex_process.compute");
                File.WriteAllText(tempShaderPath, shaderSource);

                string assetPath = "Assets/MosaicTemp/_mosaic_tex_process.compute";
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(assetPath);

                if (shader == null)
                    return ToolResult<ComputeTextureProcessResult>.Fail(
                        "Failed to compile inline compute shader", ErrorCodes.INTERNAL_ERROR);

                int kernel = shader.FindKernel("CSMain");

                srcRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat);
                srcRT.enableRandomWrite = true;
                srcRT.Create();

                dstRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat);
                dstRT.enableRandomWrite = true;
                dstRT.Create();

                UnityEngine.Graphics.Blit(sourceTexture, srcRT);

                shader.SetTexture(kernel, "Source", srcRT);
                shader.SetTexture(kernel, "Result", dstRT);
                shader.SetInt("Width", width);
                shader.SetInt("Height", height);

                int tgx = Mathf.CeilToInt(width / 8f);
                int tgy = Mathf.CeilToInt(height / 8f);
                shader.Dispatch(kernel, tgx, tgy, 1);

                // Read back
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = dstRT;
                var resultTex = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
                resultTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                resultTex.Apply();
                RenderTexture.active = prev;

                byte[] pngBytes = resultTex.EncodeToPNG();
                string absoluteOutputPath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", outputPath));
                File.WriteAllBytes(absoluteOutputPath, pngBytes);

                UnityEngine.Object.DestroyImmediate(resultTex);

                AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

                // Cleanup temp shader
                AssetDatabase.DeleteAsset(assetPath);
                if (Directory.Exists(tempDir) && Directory.GetFiles(tempDir).Length == 0)
                {
                    AssetDatabase.DeleteAsset("Assets/MosaicTemp");
                }

                return ToolResult<ComputeTextureProcessResult>.Ok(new ComputeTextureProcessResult
                {
                    OutputPath = outputPath,
                    Operation = op,
                    Width = width,
                    Height = height
                });
            }
            catch (Exception ex)
            {
                return ToolResult<ComputeTextureProcessResult>.Fail(
                    $"Texture processing failed: {ex.Message}", ErrorCodes.INTERNAL_ERROR);
            }
            finally
            {
                if (srcRT != null) RenderTexture.ReleaseTemporary(srcRT);
                if (dstRT != null) RenderTexture.ReleaseTemporary(dstRT);
            }
        }

        static string GenerateShaderSource(string op, int radius, float decayRate)
        {
            switch (op)
            {
                case "blur":
                    return $@"#pragma kernel CSMain

Texture2D<float4> Source;
RWTexture2D<float4> Result;
int Width;
int Height;

[numthreads(8,8,1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{{
    if ((int)id.x >= Width || (int)id.y >= Height) return;

    float4 sum = float4(0, 0, 0, 0);
    int count = 0;
    int r = {radius};

    for (int dy = -r; dy <= r; dy++)
    {{
        for (int dx = -r; dx <= r; dx++)
        {{
            int sx = clamp((int)id.x + dx, 0, Width - 1);
            int sy = clamp((int)id.y + dy, 0, Height - 1);
            sum += Source[uint2(sx, sy)];
            count++;
        }}
    }}

    Result[id.xy] = sum / (float)count;
}}
";
                case "diffuse":
                    return @"#pragma kernel CSMain

Texture2D<float4> Source;
RWTexture2D<float4> Result;
int Width;
int Height;

[numthreads(8,8,1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    if ((int)id.x >= Width || (int)id.y >= Height) return;

    float4 center = Source[id.xy];
    float4 sum = center * 4.0;
    int count = 4;

    if ((int)id.x > 0)         { sum += Source[uint2(id.x - 1, id.y)]; count++; }
    if ((int)id.x < Width - 1) { sum += Source[uint2(id.x + 1, id.y)]; count++; }
    if ((int)id.y > 0)         { sum += Source[uint2(id.x, id.y - 1)]; count++; }
    if ((int)id.y < Height - 1){ sum += Source[uint2(id.x, id.y + 1)]; count++; }

    Result[id.xy] = sum / (float)count;
}
";
                case "decay":
                    return $@"#pragma kernel CSMain

Texture2D<float4> Source;
RWTexture2D<float4> Result;
int Width;
int Height;

[numthreads(8,8,1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{{
    if ((int)id.x >= Width || (int)id.y >= Height) return;
    Result[id.xy] = Source[id.xy] * {decayRate:F4};
}}
";
                default:
                    return "";
            }
        }
    }
}
