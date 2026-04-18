namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectGetInfoResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public bool ActiveSelf { get; set; }
        public bool ActiveInHierarchy { get; set; }
        public string[] Components { get; set; }
        public string Tag { get; set; }
        public string Layer { get; set; }
        public int ChildCount { get; set; }
    }
}
