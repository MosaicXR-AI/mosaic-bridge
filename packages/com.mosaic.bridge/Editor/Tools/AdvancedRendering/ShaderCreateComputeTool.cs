using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public static class ShaderCreateComputeTool
    {
        static readonly string[] ValidBufferTypes = { "float", "float2", "float3", "float4", "int", "uint" };

        [MosaicTool("shader/create-compute",
                    "Generates a compute shader from a template with configurable kernel, buffer type, and optional noise",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ShaderCreateComputeResult> Execute(ShaderCreateComputeParams p)
        {
            var kernelName     = string.IsNullOrEmpty(p.KernelName) ? "CSMain" : p.KernelName;
            var threadGroupSz  = p.ThreadGroupSize ?? 64;
            var bufferType     = string.IsNullOrEmpty(p.BufferType) ? "float" : p.BufferType;
            var bufferSize     = p.BufferSize ?? 1024;
            var outputDir      = string.IsNullOrEmpty(p.OutputDirectory)
                ? "Assets/Generated/Rendering/Compute"
                : p.OutputDirectory;

            if (!outputDir.StartsWith("Assets/"))
                return ToolResult<ShaderCreateComputeResult>.Fail(
                    "OutputDirectory must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (!ValidBufferTypes.Contains(bufferType.ToLowerInvariant()))
                return ToolResult<ShaderCreateComputeResult>.Fail(
                    $"Invalid BufferType '{bufferType}'. Valid: {string.Join(", ", ValidBufferTypes)}",
                    ErrorCodes.INVALID_PARAM);

            if (threadGroupSz <= 0 || threadGroupSz > 1024)
                return ToolResult<ShaderCreateComputeResult>.Fail(
                    "ThreadGroupSize must be between 1 and 1024", ErrorCodes.OUT_OF_RANGE);

            var computePath = Path.Combine(outputDir, $"{p.Name}.compute").Replace("\\", "/");

            var sb = new StringBuilder();
            sb.AppendLine($"#pragma kernel {kernelName}");
            sb.AppendLine();
            sb.AppendLine($"RWStructuredBuffer<{bufferType}> Result;");
            sb.AppendLine($"uint BufferSize;");
            sb.AppendLine();

            if (p.IncludeNoise)
            {
                sb.AppendLine("// Hash-based 3D Perlin noise");
                sb.AppendLine("float hash(float3 p)");
                sb.AppendLine("{");
                sb.AppendLine("    p = frac(p * 0.3183099 + 0.1);");
                sb.AppendLine("    p *= 17.0;");
                sb.AppendLine("    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));");
                sb.AppendLine("}");
                sb.AppendLine();
                sb.AppendLine("float noise3D(float3 x)");
                sb.AppendLine("{");
                sb.AppendLine("    float3 i = floor(x);");
                sb.AppendLine("    float3 f = frac(x);");
                sb.AppendLine("    f = f * f * (3.0 - 2.0 * f);");
                sb.AppendLine("    return lerp(");
                sb.AppendLine("        lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),");
                sb.AppendLine("             lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),");
                sb.AppendLine("        lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),");
                sb.AppendLine("             lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y),");
                sb.AppendLine("        f.z);");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            sb.AppendLine($"[numthreads({threadGroupSz},1,1)]");
            sb.AppendLine($"void {kernelName}(uint3 id : SV_DispatchThreadID)");
            sb.AppendLine("{");
            sb.AppendLine("    if (id.x >= BufferSize) return;");
            sb.AppendLine();

            if (p.IncludeNoise)
            {
                if (bufferType == "float")
                    sb.AppendLine("    Result[id.x] = noise3D(float3(id.x * 0.1, 0, 0));");
                else
                    sb.AppendLine($"    Result[id.x] = ({bufferType})noise3D(float3(id.x * 0.1, 0, 0));");
            }
            else
            {
                sb.AppendLine($"    Result[id.x] = ({bufferType})id.x;");
            }

            sb.AppendLine("}");

            // Write file
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, computePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(computePath);

            return ToolResult<ShaderCreateComputeResult>.Ok(new ShaderCreateComputeResult
            {
                ComputeShaderPath = computePath,
                KernelName        = kernelName,
                ThreadGroupSize   = threadGroupSz,
                BufferType        = bufferType
            });
        }
    }
}
