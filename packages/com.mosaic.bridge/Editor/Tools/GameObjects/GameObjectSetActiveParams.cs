using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectSetActiveParams
    {
        [Required] public string Name { get; set; }
        [Required] public bool Active { get; set; }
    }
}
