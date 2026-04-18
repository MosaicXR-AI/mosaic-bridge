using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Packages
{
    public sealed class PackageRemoveParams
    {
        /// <summary>
        /// Package name to remove, e.g. "com.unity.cinemachine".
        /// Do not include a version number.
        /// </summary>
        [Required] public string Name { get; set; }
    }
}
