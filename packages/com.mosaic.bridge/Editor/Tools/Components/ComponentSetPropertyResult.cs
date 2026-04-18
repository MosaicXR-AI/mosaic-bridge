namespace Mosaic.Bridge.Tools.Components
{
    public sealed class ComponentSetPropertyResult
    {
        public string GameObjectName { get; set; }
        public string ComponentType { get; set; }
        public string PropertyName { get; set; }
        public object NewValue { get; set; }
    }
}
