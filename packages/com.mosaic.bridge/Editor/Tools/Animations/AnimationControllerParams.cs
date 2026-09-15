using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationControllerParams
    {
        /// <summary>Action to perform: create, info, add-parameter, remove-parameter, add-layer,
        /// set-layer, remove-layer, create-override</summary>
        [Required] public string Action { get; set; }

        /// <summary>Asset path for the controller (e.g. "Assets/Animations/MyController.controller")</summary>
        public string Path { get; set; }

        // -- create --
        // Uses Path only

        // -- add-parameter / remove-parameter --
        /// <summary>Parameter name (for add-parameter)</summary>
        public string ParameterName { get; set; }

        /// <summary>Parameter type: Float, Int, Bool, Trigger (for add-parameter)</summary>
        public string ParameterType { get; set; }

        /// <summary>Parameter index (for remove-parameter)</summary>
        public int? ParameterIndex { get; set; }

        // -- add-layer --
        /// <summary>Layer name (for add-layer)</summary>
        public string LayerName { get; set; }

        // -- set-layer / remove-layer --
        /// <summary>Index of the layer to modify (for set-layer, remove-layer)</summary>
        public int? LayerIndex { get; set; }

        /// <summary>set-layer: AnimatorControllerLayer.defaultWeight (0..1). Layer 0's own weight
        /// is always effectively 1 and this is ignored for it, matching the Editor's own Inspector.</summary>
        public float? LayerWeight { get; set; }

        /// <summary>set-layer: "Override" or "Additive".</summary>
        public string BlendingMode { get; set; }

        /// <summary>set-layer: asset path of an AvatarMask to apply (e.g. from animation/avatar-mask
        /// create), or empty string to clear the mask.</summary>
        public string AvatarMaskPath { get; set; }

        /// <summary>set-layer: whether this layer applies IK (AnimatorControllerLayer.iKPass) —
        /// only meaningful on a layer other than the base layer (index 0).</summary>
        public bool? IKPass { get; set; }

        /// <summary>set-layer: index of the layer this one syncs to (mirrors its state machine
        /// structure while overriding motions/settings per state), or -1 to un-sync.</summary>
        public int? SyncedLayerIndex { get; set; }

        /// <summary>set-layer: when synced, whether this layer's own state durations/timing are used
        /// instead of the synced-from layer's.</summary>
        public bool? SyncedLayerAffectsTiming { get; set; }

        // -- create-override --
        /// <summary>Asset path of the AnimatorController this override controller retargets clips for.</summary>
        public string BaseControllerPath { get; set; }

        /// <summary>Clip replacements to apply. Omit for an override controller with every clip
        /// still pointing at the base controller's own originals (a valid starting point to edit
        /// later, e.g. in the Inspector).</summary>
        public OverrideClipInput[] Overrides { get; set; }
    }

    public sealed class OverrideClipInput
    {
        /// <summary>Name of the original clip (as it appears in the base controller) to replace.
        /// Either this or OriginalClipPath is required.</summary>
        public string OriginalClipName { get; set; }

        /// <summary>Asset path of the original clip to replace. Either this or OriginalClipName is
        /// required — OriginalClipName is usually simpler since the base controller's own clips
        /// may be sub-assets of an FBX with no independently addressable path of their own.</summary>
        public string OriginalClipPath { get; set; }

        /// <summary>Asset path of the replacement AnimationClip.</summary>
        public string NewClipPath { get; set; }

        /// <summary>For a multi-clip container at NewClipPath (see L19), the specific clip name to use.</summary>
        public string NewClipName { get; set; }
    }
}
