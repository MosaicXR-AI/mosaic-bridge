using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Selection
{
    public sealed class SelectionGetResult
    {
        public List<SelectedObject> Objects { get; set; }
        public int Count { get; set; }
    }

    public sealed class SelectedObject
    {
        public string Name { get; set; }
        public int InstanceId { get; set; }
        public string HierarchyPath { get; set; }
        public string AssetPath { get; set; }
        public bool IsGameObject { get; set; }
    }
}
