using System.Linq;
using UnityEditor;
using UnityEngine.Audio;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioMixerInfoTool
    {
        // O4 §4.4 (G4): "QA gate for 3.3 ('SFX exists and routes to Master?')" and build-manifest
        // provenance both need this. GroupNames (public FindMatchingGroups) is unconditional;
        // Hierarchy (internal masterGroup/children) degrades to unavailable rather than failing
        // the whole call, per O4's own "ship flat mode even if the probe fails" note.
        [MosaicTool("audio/mixer-info",
                    "Inspects an AudioMixer asset. GroupNames is a flat list of every group (always " +
                    "available). Hierarchy is the group tree rooted at the master group — set only when " +
                    "Unity's internal AudioMixerController API is reachable on this version; check " +
                    "HierarchyAvailable before relying on it.",
                    isReadOnly: true)]
        public static ToolResult<AudioMixerInfoResult> Execute(AudioMixerInfoParams p)
        {
            if (string.IsNullOrEmpty(p.MixerAssetPath))
                return ToolResult<AudioMixerInfoResult>.Fail("MixerAssetPath is required", ErrorCodes.INVALID_PARAM);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(p.MixerAssetPath);
            if (mixer == null)
                return ToolResult<AudioMixerInfoResult>.Fail(
                    $"AudioMixer not found at '{p.MixerAssetPath}'", ErrorCodes.NOT_FOUND);

            var flat = mixer.FindMatchingGroups("");
            var groupNames = flat != null ? flat.Select(g => g.name).ToArray() : System.Array.Empty<string>();

            AudioMixerGroupNode hierarchy = null;
            bool hierarchyAvailable = false;
            string note = null;
            if (AudioMixerReflection.TryGetMasterGroup(mixer, out var master, out var masterError))
            {
                hierarchy = BuildNode(master);
                hierarchyAvailable = true;
            }
            else
            {
                note = $"Hierarchy unavailable — {masterError} GroupNames is still complete.";
            }

            return ToolResult<AudioMixerInfoResult>.Ok(new AudioMixerInfoResult
            {
                MixerName = mixer.name,
                GroupNames = groupNames,
                Hierarchy = hierarchy,
                HierarchyAvailable = hierarchyAvailable,
                Note = note,
            });
        }

        private static AudioMixerGroupNode BuildNode(AudioMixerGroup group)
        {
            var node = new AudioMixerGroupNode { Name = group.name };
            if (AudioMixerReflection.TryGetChildren(group, out var children, out _) && children.Length > 0)
                node.Children = children.Where(c => c != null).Select(BuildNode).ToArray();
            return node;
        }
    }
}
