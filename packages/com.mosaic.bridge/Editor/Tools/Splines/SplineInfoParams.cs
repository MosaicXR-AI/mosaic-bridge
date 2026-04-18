#if MOSAIC_HAS_SPLINES
namespace Mosaic.Bridge.Tools.Splines
{
    public sealed class SplineInfoParams
    {
        /// <summary>Optional: filter by GameObject name. Null returns all SplineContainers in the scene.</summary>
        public string GameObjectName { get; set; }
    }
}
#endif
