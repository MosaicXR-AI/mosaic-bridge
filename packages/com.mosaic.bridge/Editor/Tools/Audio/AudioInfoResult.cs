using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioInfoResult
    {
        public List<AudioSourceInfo> Sources { get; set; }
        public List<AudioListenerInfo> Listeners { get; set; }
        public List<string> Warnings { get; set; }
    }

    public sealed class AudioSourceInfo
    {
        public int InstanceId { get; set; }
        public string GameObjectName { get; set; }
        public string HierarchyPath { get; set; }
        public string ClipName { get; set; }
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public float SpatialBlend { get; set; }
        public bool Loop { get; set; }
        public bool PlayOnAwake { get; set; }
        public bool IsPlaying { get; set; }
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }
        public string RolloffMode { get; set; }
        public float DopplerLevel { get; set; }
        public float Spread { get; set; }
    }

    public sealed class AudioListenerInfo
    {
        public int InstanceId { get; set; }
        public string GameObjectName { get; set; }
        public string HierarchyPath { get; set; }
        public bool Enabled { get; set; }
    }
}
