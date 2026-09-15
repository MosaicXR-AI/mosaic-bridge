using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerTransitionParams
    {
        [Required] public string MixerAssetPath { get; set; }

        /// <summary>Snapshot(s) to transition to. A single entry is an ordinary snapshot switch;
        /// multiple entries blend them by Weights.</summary>
        [Required] public string[] SnapshotNames { get; set; }

        /// <summary>Blend weight per SnapshotNames entry. Defaults to equal weighting (1/N each)
        /// when omitted.</summary>
        public float[] Weights { get; set; }

        /// <summary>Time in seconds to interpolate over.</summary>
        public float TimeToReach { get; set; } = 0f;
    }
}
