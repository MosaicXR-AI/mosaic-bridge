using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Components
{
    public sealed class ComponentListResult
    {
        public string              GameObjectName { get; set; }
        public int                 InstanceId     { get; set; }
        public List<ComponentEntry> Components    { get; set; }
        public int                 TotalCount     { get; set; }
        /// <summary>Opaque cursor for the next page. Null when no more results.</summary>
        public string              NextPageToken  { get; set; }
    }

    public sealed class ComponentEntry
    {
        public string TypeName          { get; set; }
        public string FullTypeName      { get; set; }
        public int    ComponentInstanceId { get; set; }
    }
}
