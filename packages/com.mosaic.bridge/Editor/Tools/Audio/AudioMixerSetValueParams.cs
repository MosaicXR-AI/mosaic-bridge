using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerSetValueParams
    {
        [Required] public string MixerAssetPath { get; set; }
        [Required] public string SnapshotName { get; set; }
        [Required] public string GroupPath { get; set; }

        /// <summary>"volume" or "pitch".</summary>
        [Required] public string ParamKind { get; set; }

        /// <summary>"set" or "get". Default "set".</summary>
        public string Operation { get; set; } = "set";

        /// <summary>Required for "set". Volume is in dB (-80..20); pitch is a multiplier (-3..3 range, 1 = unmodified).</summary>
        public float? Value { get; set; }
    }
}
