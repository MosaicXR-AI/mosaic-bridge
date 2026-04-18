using Mosaic.Bridge.Contracts.Attributes;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Tools.Components
{
    public sealed class ComponentSetPropertyParams
    {
        [Required] public string GameObjectName { get; set; }
        [Required] public string ComponentType { get; set; }
        [Required] public string PropertyName { get; set; }
        public JToken Value { get; set; }
    }
}
