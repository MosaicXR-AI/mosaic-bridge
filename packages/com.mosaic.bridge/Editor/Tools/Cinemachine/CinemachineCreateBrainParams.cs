#if MOSAIC_HAS_CINEMACHINE
namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineCreateBrainParams
    {
        /// <summary>Default blend duration in seconds. Default 2.</summary>
        public float DefaultBlend { get; set; } = 2f;

        /// <summary>Blend type: Cut, EaseInOut, Linear. Default EaseInOut.</summary>
        public string BlendType { get; set; } = "EaseInOut";

        /// <summary>Optional: name of the camera GameObject. Null uses Camera.main.</summary>
        public string CameraName { get; set; }

        /// <summary>"FixedUpdate", "LateUpdate", "ManualUpdate", "SmartUpdate". Null to leave unchanged.</summary>
        public string UpdateMethod { get; set; }

        /// <summary>Name of a scene GameObject whose Y axis defines world-space Up for all vcams. Null to leave unchanged.</summary>
        public string WorldUpOverrideName { get; set; }

        /// <summary>Bitmask (OutputChannels) filtering which vcams this brain recognizes. Null to leave unchanged.</summary>
        public int? ChannelMask { get; set; }

        /// <summary>Asset path for a CinemachineBlenderSettings — created if it doesn't exist, else
        /// loaded and CustomBlends appended to. Required when CustomBlends is given.</summary>
        public string CustomBlendsAssetPath { get; set; }

        public CinemachineCustomBlendInput[] CustomBlends { get; set; }
    }

    public sealed class CinemachineCustomBlendInput
    {
        /// <summary>Vcam name to blend from, or "**ANY CAMERA**" to match any source.</summary>
        public string From { get; set; }
        /// <summary>Vcam name to blend to, or "**ANY CAMERA**" to match any destination.</summary>
        public string To { get; set; }
        /// <summary>Cut, EaseInOut, Linear. Default EaseInOut.</summary>
        public string BlendType { get; set; } = "EaseInOut";
        public float BlendTime { get; set; } = 2f;
    }
}
#endif
