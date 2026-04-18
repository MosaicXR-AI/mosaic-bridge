using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Profiling
{
    public static class ProfilerMemoryTool
    {
        [MosaicTool("profiler/memory",
                    "Returns a memory breakdown by object type (Textures, Meshes, Materials, etc.)",
                    isReadOnly: true)]
        public static ToolResult<ProfilerMemoryResult> Memory(ProfilerMemoryParams p)
        {
            var areas = new List<MemoryAreaInfo>();

            // Textures
            var textures = Resources.FindObjectsOfTypeAll<Texture>();
            long textureSize = 0;
            foreach (var t in textures)
                textureSize += Profiler.GetRuntimeMemorySizeLong(t);
            areas.Add(new MemoryAreaInfo { Name = "Textures", Count = textures.Length, TotalSizeBytes = textureSize });

            // Meshes
            var meshes = Resources.FindObjectsOfTypeAll<Mesh>();
            long meshSize = 0;
            foreach (var m in meshes)
                meshSize += Profiler.GetRuntimeMemorySizeLong(m);
            areas.Add(new MemoryAreaInfo { Name = "Meshes", Count = meshes.Length, TotalSizeBytes = meshSize });

            // Materials
            var materials = Resources.FindObjectsOfTypeAll<Material>();
            long matSize = 0;
            foreach (var m in materials)
                matSize += Profiler.GetRuntimeMemorySizeLong(m);
            areas.Add(new MemoryAreaInfo { Name = "Materials", Count = materials.Length, TotalSizeBytes = matSize });

            // AnimationClips
            var clips = Resources.FindObjectsOfTypeAll<AnimationClip>();
            long clipSize = 0;
            foreach (var c in clips)
                clipSize += Profiler.GetRuntimeMemorySizeLong(c);
            areas.Add(new MemoryAreaInfo { Name = "AnimationClips", Count = clips.Length, TotalSizeBytes = clipSize });

            // AudioClips
            var audioClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            long audioSize = 0;
            foreach (var a in audioClips)
                audioSize += Profiler.GetRuntimeMemorySizeLong(a);
            areas.Add(new MemoryAreaInfo { Name = "AudioClips", Count = audioClips.Length, TotalSizeBytes = audioSize });

            // Shaders
            var shaders = Resources.FindObjectsOfTypeAll<Shader>();
            long shaderSize = 0;
            foreach (var s in shaders)
                shaderSize += Profiler.GetRuntimeMemorySizeLong(s);
            areas.Add(new MemoryAreaInfo { Name = "Shaders", Count = shaders.Length, TotalSizeBytes = shaderSize });

            return ToolResult<ProfilerMemoryResult>.Ok(new ProfilerMemoryResult
            {
                TotalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong(),
                TotalReservedMemory = Profiler.GetTotalReservedMemoryLong(),
                Areas = areas
            });
        }
    }
}
