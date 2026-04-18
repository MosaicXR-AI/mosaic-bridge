using System;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Compute
{
    public static class ComputeDispatchTool
    {
        [MosaicTool("compute/dispatch",
                    "Dispatches an existing compute shader with specified parameters and optional buffer data",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ComputeDispatchResult> Execute(ComputeDispatchParams p)
        {
            if (string.IsNullOrEmpty(p.ShaderPath))
                return ToolResult<ComputeDispatchResult>.Fail(
                    "ShaderPath is required", ErrorCodes.INVALID_PARAM);

            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(p.ShaderPath);
            if (shader == null)
                return ToolResult<ComputeDispatchResult>.Fail(
                    $"Compute shader not found at '{p.ShaderPath}'", ErrorCodes.NOT_FOUND);

            string kernelName = string.IsNullOrEmpty(p.KernelName) ? "CSMain" : p.KernelName;
            int kernel;
            try
            {
                kernel = shader.FindKernel(kernelName);
            }
            catch (Exception)
            {
                return ToolResult<ComputeDispatchResult>.Fail(
                    $"Kernel '{kernelName}' not found in shader", ErrorCodes.NOT_FOUND);
            }

            int tgx = Math.Max(1, Math.Min(p.ThreadGroupsX, 65535));
            int tgy = Math.Max(1, Math.Min(p.ThreadGroupsY, 65535));
            int tgz = Math.Max(1, Math.Min(p.ThreadGroupsZ, 65535));

            ComputeBuffer buffer = null;
            float[] outputData = null;
            int bufferSize = 0;

            try
            {
                if (p.BufferData != null && p.BufferData.Length > 0)
                {
                    bufferSize = p.BufferData.Length;
                    buffer = new ComputeBuffer(bufferSize, sizeof(float));
                    buffer.SetData(p.BufferData);
                    shader.SetBuffer(kernel, "Result", buffer);
                    shader.SetInt("count", bufferSize);
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                shader.Dispatch(kernel, tgx, tgy, tgz);
                stopwatch.Stop();

                if (buffer != null)
                {
                    float[] fullOutput = new float[bufferSize];
                    buffer.GetData(fullOutput);
                    int readCount = Math.Min(bufferSize, 64);
                    outputData = new float[readCount];
                    Array.Copy(fullOutput, outputData, readCount);
                }

                return ToolResult<ComputeDispatchResult>.Ok(new ComputeDispatchResult
                {
                    KernelName = kernelName,
                    ThreadGroups = $"{tgx}x{tgy}x{tgz}",
                    BufferSize = bufferSize,
                    OutputData = outputData,
                    ExecutionTimeMs = (float)stopwatch.Elapsed.TotalMilliseconds
                });
            }
            finally
            {
                if (buffer != null)
                    buffer.Release();
            }
        }
    }
}
