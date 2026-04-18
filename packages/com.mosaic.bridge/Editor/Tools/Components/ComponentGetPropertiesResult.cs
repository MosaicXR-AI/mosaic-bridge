using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Components
{
    public sealed class ComponentGetPropertiesResult
    {
        public string GameObjectName { get; set; }
        public string ComponentType { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }
}
