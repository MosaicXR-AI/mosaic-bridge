using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectReparentParams
    {
        [Required] public string Name { get; set; }
        public string NewParent { get; set; }   // null or empty = move to scene root
    }
}
