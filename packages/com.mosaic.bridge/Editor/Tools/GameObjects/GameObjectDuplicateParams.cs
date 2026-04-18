using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectDuplicateParams
    {
        [Required] public string Name { get; set; }
    }
}
