#if MOSAIC_HAS_CINEMACHINE
namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineInfoParams
    {
        /// <summary>Optional: filter by virtual camera name. Null returns all.</summary>
        public string VCamName { get; set; }
    }
}
#endif
