namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerInfoResult
    {
        public string MixerName { get; set; }

        /// <summary>Every group's own name, flat — always available (public AudioMixer.FindMatchingGroups).</summary>
        public string[] GroupNames { get; set; }

        /// <summary>The group tree rooted at the master group. Null when HierarchyAvailable is false.</summary>
        public AudioMixerGroupNode Hierarchy { get; set; }

        /// <summary>False if Unity's internal AudioMixerController API isn't reachable on this
        /// version — GroupNames is still complete either way (O4's own "ship flat mode even if the
        /// probe fails" rule).</summary>
        public bool HierarchyAvailable { get; set; }

        public string Note { get; set; }
    }

    public sealed class AudioMixerGroupNode
    {
        public string Name { get; set; }
        public AudioMixerGroupNode[] Children { get; set; }
    }
}
