using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioRouteSourceParams
    {
        /// <summary>InstanceId of the GameObject carrying the AudioSource.</summary>
        public int? InstanceId { get; set; }

        /// <summary>Name of the GameObject carrying the AudioSource.</summary>
        public string Name { get; set; }

        /// <summary>Path to the .mixer asset, e.g. "Assets/Audio/Main.mixer".</summary>
        [Required] public string MixerAssetPath { get; set; }

        /// <summary>Group name or sub-path to route to, e.g. "SFX" or "Master/SFX". Matched via
        /// AudioMixer.FindMatchingGroups — must resolve to exactly one group.</summary>
        [Required] public string GroupPath { get; set; }
    }
}
