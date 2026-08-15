using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectRenameParams
    {
        [Required] public string Name { get; set; }
        [Required] public string NewName { get; set; }
    }
}
