using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectFindByNameParams
    {
        [Required] public string Name { get; set; }
        public bool ExactMatch { get; set; } = true;
    }
}
