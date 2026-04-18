using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Packages
{
    public sealed class PackageSearchParams
    {
        /// <summary>
        /// Search query string to find packages in the Unity registry.
        /// For example: "cinemachine", "input system", "2d".
        /// </summary>
        [Required] public string Query { get; set; }
    }
}
