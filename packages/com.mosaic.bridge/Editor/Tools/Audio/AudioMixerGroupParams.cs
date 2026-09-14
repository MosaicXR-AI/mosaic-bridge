using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerGroupParams
    {
        [Required] public string MixerAssetPath { get; set; }

        /// <summary>"add", "rename", "delete", or "move".</summary>
        [Required] public string Operation { get; set; }

        /// <summary>The group to rename/delete/move. Matched via AudioMixer.FindMatchingGroups —
        /// must resolve to exactly one group. Not used for "add".</summary>
        public string GroupPath { get; set; }

        /// <summary>"add": the new group's name. "rename": its new name.</summary>
        public string Name { get; set; }

        /// <summary>"add"/"move": where to attach the group. Matched via FindMatchingGroups; empty
        /// means the mixer's master group.</summary>
        public string ParentPath { get; set; }
    }
}
