namespace Mosaic.Bridge.Tools.Components
{
    public sealed class ComponentSetReferenceResult
    {
        public string GameObjectName  { get; set; }
        public string ComponentType   { get; set; }
        public string PropertyPath    { get; set; }
        /// <summary>Resolved serialized property path (may differ from input if m_ prefix was applied).</summary>
        public string PropertyType    { get; set; }
        /// <summary>The value that was assigned — object name for references, formatted value for primitives.</summary>
        public string ResolvedTarget  { get; set; }
    }
}
