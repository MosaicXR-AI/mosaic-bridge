#if MOSAIC_HAS_TMP
namespace Mosaic.Bridge.Tools.TextMeshPro
{
    public sealed class TmpInfoParams
    {
        /// <summary>
        /// Name of a specific GameObject to query. If null, returns all TMP components in the scene.
        /// </summary>
        public string GameObjectName { get; set; }
    }
}
#endif
