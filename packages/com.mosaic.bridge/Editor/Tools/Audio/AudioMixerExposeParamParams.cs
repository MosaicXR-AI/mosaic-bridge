using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerExposeParamParams
    {
        [Required] public string MixerAssetPath { get; set; }

        /// <summary>"expose", "rename", "remove", or "list".</summary>
        [Required] public string Operation { get; set; }

        /// <summary>The group whose parameter is exposed. Matched via AudioMixer.FindMatchingGroups.
        /// Required for "expose".</summary>
        public string GroupPath { get; set; }

        /// <summary>"volume" or "pitch". Required for "expose".</summary>
        public string ParamKind { get; set; }

        /// <summary>"expose": the new exposed parameter's name (what AudioMixer.SetFloat/GetFloat
        /// use at runtime). "rename": the existing exposed parameter's current name.
        /// "remove": the exposed parameter's name to remove.</summary>
        public string Name { get; set; }

        /// <summary>"rename": the new name.</summary>
        public string NewName { get; set; }
    }
}
