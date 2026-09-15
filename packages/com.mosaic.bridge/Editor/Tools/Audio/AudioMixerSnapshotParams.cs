using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerSnapshotParams
    {
        [Required] public string MixerAssetPath { get; set; }

        /// <summary>"add", "rename", "set-target", or "list".</summary>
        [Required] public string Operation { get; set; }

        /// <summary>"add": the new snapshot's name (starts with default values, same as Unity's
        /// own "Add Snapshot" button — no clone-from-current-state operation exists). "rename"/
        /// "set-target": the existing snapshot's current name.</summary>
        public string Name { get; set; }

        /// <summary>"rename": the new name.</summary>
        public string NewName { get; set; }
    }
}
