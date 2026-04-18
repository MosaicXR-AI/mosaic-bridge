using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Components
{
    public sealed class ComponentGetPropertiesParams
    {
        [Required] public string GameObjectName { get; set; }
        [Required] public string ComponentType { get; set; }
    }
}
