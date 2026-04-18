namespace Mosaic.Bridge.Tools.Components
{
    public sealed class ComponentSetReferenceParams
    {
        public string GameObjectName  { get; set; }
        public string ComponentType   { get; set; }
        public string PropertyPath    { get; set; }
        public string TargetObjectPath { get; set; }
        /// <summary>Optional: "Asset" or "GameObject". Defaults to trying asset first.</summary>
        public string TargetType      { get; set; }
    }
}
