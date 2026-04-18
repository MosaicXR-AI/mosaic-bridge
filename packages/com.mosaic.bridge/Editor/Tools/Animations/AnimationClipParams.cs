using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationClipParams
    {
        /// <summary>Action to perform: create, info, set-curve, add-event</summary>
        [Required] public string Action { get; set; }

        /// <summary>Asset path for the AnimationClip (e.g. "Assets/Animations/Walk.anim")</summary>
        public string Path { get; set; }

        /// <summary>Clip name (for create; defaults to filename)</summary>
        public string ClipName { get; set; }

        /// <summary>Frame rate (for create, default 60)</summary>
        public float FrameRate { get; set; } = 60f;

        /// <summary>Whether the clip should loop (for create)</summary>
        public bool Loop { get; set; } = false;

        // -- set-curve --
        /// <summary>Relative path of the animated GameObject (e.g. "" for root, "Spine/Chest")</summary>
        public string PropertyPath { get; set; }

        /// <summary>Component type name (e.g. "Transform", "SpriteRenderer")</summary>
        public string ComponentType { get; set; }

        /// <summary>Property name (e.g. "localPosition.x", "m_Color.r")</summary>
        public string PropertyName { get; set; }

        /// <summary>Keyframe times (parallel array with KeyframeValues)</summary>
        public float[] KeyframeTimes { get; set; }

        /// <summary>Keyframe values (parallel array with KeyframeTimes)</summary>
        public float[] KeyframeValues { get; set; }

        // -- add-event --
        /// <summary>Event time in seconds</summary>
        public float? EventTime { get; set; }

        /// <summary>Event function name</summary>
        public string EventFunction { get; set; }

        /// <summary>Event string parameter</summary>
        public string EventStringParam { get; set; }

        /// <summary>Event float parameter</summary>
        public float? EventFloatParam { get; set; }

        /// <summary>Event int parameter</summary>
        public int? EventIntParam { get; set; }
    }
}
