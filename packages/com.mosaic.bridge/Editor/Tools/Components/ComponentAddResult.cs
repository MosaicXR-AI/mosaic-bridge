namespace Mosaic.Bridge.Tools.Components
{
    public sealed class ComponentAddResult
    {
        public string GameObjectName { get; set; }
        public string ComponentType { get; set; }
        public bool AlreadyExisted { get; set; }
    }
}
