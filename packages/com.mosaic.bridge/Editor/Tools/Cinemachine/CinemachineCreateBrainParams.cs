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
    }
}
#endif
