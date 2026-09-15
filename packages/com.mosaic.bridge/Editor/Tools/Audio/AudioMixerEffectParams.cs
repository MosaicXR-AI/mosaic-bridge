using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerEffectParams
    {
        [Required] public string MixerAssetPath { get; set; }
        [Required] public string GroupPath { get; set; }

        /// <summary>"add", "remove", "set-value", "get-value", "send-to", "list" (the group's current
        /// effect chain with indices/types), or "list-types" (every valid EffectType name for this
        /// Unity install — never hardcode one).</summary>
        [Required] public string Operation { get; set; }

        /// <summary>"add": the effect type name, e.g. "Lowpass", "SFX Reverb", "Send", "Receive".
        /// Must be one of list-types' EffectTypeNames.</summary>
        public string EffectType { get; set; }

        /// <summary>Index into the group's effect chain (from "list" or the "add" result).
        /// Required for remove/set-value/get-value/send-to.</summary>
        public int? EffectIndex { get; set; }

        /// <summary>set-value/get-value: which snapshot the value is authored into/read from.</summary>
        public string SnapshotName { get; set; }

        /// <summary>set-value/get-value: the effect's named parameter (from list's ParameterNames),
        /// or null/empty for the effect's overall Mix Level (wet/dry) knob.</summary>
        public string ParamName { get; set; }

        /// <summary>Required for set-value.</summary>
        public float? Value { get; set; }

        /// <summary>send-to: the group whose effect receives this effect's sidechain send.</summary>
        public string SendTargetGroupPath { get; set; }

        /// <summary>send-to: the receiving effect's index within SendTargetGroupPath.</summary>
        public int? SendTargetEffectIndex { get; set; }
    }
}
