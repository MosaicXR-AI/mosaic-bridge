using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Packages
{
    public sealed class PackageSearchResult
    {
        public List<PackageRef> Packages { get; set; }
        public int              Count    { get; set; }
        public string           Query    { get; set; }
    }
}
