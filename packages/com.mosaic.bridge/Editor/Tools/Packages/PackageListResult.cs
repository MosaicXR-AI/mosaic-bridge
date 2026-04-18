using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Packages
{
    public sealed class PackageListResult
    {
        public List<PackageRef> Packages { get; set; }
        public int              Count    { get; set; }
    }
}
