#if MOSAIC_HAS_ADDRESSABLES
namespace Mosaic.Bridge.Tools.Addressables
{
    public sealed class AddressablesBuildParams
    {
        /// <summary>If true, cleans previous build content before building. Default false.</summary>
        public bool CleanBuild { get; set; }
    }
}
#endif
