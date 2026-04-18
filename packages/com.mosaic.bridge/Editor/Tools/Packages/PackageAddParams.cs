using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Packages
{
    public sealed class PackageAddParams
    {
        /// <summary>
        /// Package identifier to install. Accepts:
        /// - Registry name: "com.unity.cinemachine"
        /// - Name with version: "com.unity.cinemachine@2.8.0"
        /// - Git URL: "https://github.com/user/repo.git"
        /// </summary>
        [Required] public string Identifier { get; set; }
    }
}
