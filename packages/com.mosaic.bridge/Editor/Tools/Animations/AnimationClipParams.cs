using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationClipParams
    {
        /// <summary>Action to perform: create, info, set-curve, set-sprite-curve, add-event,
        /// set-settings</summary>
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

        /// <summary>Component type name (e.g. "Transform", "Light"). set-curve only animates
        /// float-valued properties — a SpriteRenderer example here would mislead, since its one
        /// commonly-animated field (m_Sprite) is an Object reference; use set-sprite-curve for that.</summary>
        public string ComponentType { get; set; }

        /// <summary>Property name (e.g. "localPosition.x", "m_Color.r")</summary>
        public string PropertyName { get; set; }

        /// <summary>Keyframe times (parallel array with KeyframeValues)</summary>
        public float[] KeyframeTimes { get; set; }

        /// <summary>Keyframe values (parallel array with KeyframeTimes)</summary>
        public float[] KeyframeValues { get; set; }

        /// <summary>set-curve: tangent mode applied to every key on this curve — Free, Auto,
        /// Linear, Constant, or ClampedAuto (AnimationUtility.TangentMode). Omit to leave Unity's
        /// own default (ClampedAuto).</summary>
        public string TangentMode { get; set; }

        /// <summary>set-curve: batch form — set several curves in one call, each with its own
        /// ComponentType/PropertyName/PropertyPath/KeyframeTimes/KeyframeValues/TangentMode. When
        /// provided, the single-curve fields above are ignored.</summary>
        public CurveSpec[] Curves { get; set; }

        // -- remove-curve --
        // Uses ComponentType/PropertyName/PropertyPath above to identify the curve to remove.

        // -- set-sprite-curve --
        /// <summary>Sprite asset paths, one per keyframe (parallel array with KeyframeTimes),
        /// optionally sub-addressed as "Assets/sheet.png#Run_03" (O4 §3.1). ComponentType/
        /// PropertyName default to "SpriteRenderer"/"m_Sprite" but can be overridden — the same
        /// PPtr-curve mechanism drives Image.m_Sprite too.</summary>
        public string[] Sprites { get; set; }

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

        /// <summary>Event Object-reference parameter — an asset path (optionally sub-addressed,
        /// O4 §3.1), resolved to a UnityEngine.Object via AnimationEvent.objectReferenceParameter.</summary>
        public string EventObjectReferenceParam { get; set; }

        // -- set-settings (AnimationUtility.GetAnimationClipSettings/SetAnimationClipSettings) --
        // No ScriptReference page exists for this struct in Unity 6; field names verified against
        // Unity's own source (Editor/Mono/Animation/AnimationClipSettings.bindings.cs).
        public bool? LoopTime { get; set; }
        public bool? LoopBlend { get; set; }
        public bool? LoopBlendOrientation { get; set; }
        public bool? LoopBlendPositionY { get; set; }
        public bool? LoopBlendPositionXZ { get; set; }
        public bool? KeepOriginalOrientation { get; set; }
        public bool? KeepOriginalPositionY { get; set; }
        public bool? KeepOriginalPositionXZ { get; set; }
        public bool? HeightFromFeet { get; set; }
        public bool? Mirror { get; set; }
        /// <summary>Normalized time (0..1) the loop cycle starts at.</summary>
        public float? CycleOffset { get; set; }
        /// <summary>Trim: normalized start/stop time within the source take (for an FBX-sourced clip).</summary>
        public float? StartTime { get; set; }
        public float? StopTime { get; set; }
    }

    /// <summary>One curve in a set-curve batch call (AnimationClipParams.Curves) — the same shape
    /// as set-curve's own single-curve fields.</summary>
    public sealed class CurveSpec
    {
        public string PropertyPath { get; set; }
        public string ComponentType { get; set; }
        public string PropertyName { get; set; }
        public float[] KeyframeTimes { get; set; }
        public float[] KeyframeValues { get; set; }
        public string TangentMode { get; set; }
    }
}
