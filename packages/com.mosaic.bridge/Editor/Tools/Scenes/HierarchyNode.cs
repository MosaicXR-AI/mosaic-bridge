using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Scenes
{
    public sealed class HierarchyNode
    {
        public string Name { get; set; }
        public int InstanceId { get; set; }
        public bool ActiveSelf { get; set; }
        public List<HierarchyNode> Children { get; set; }
    }
}
