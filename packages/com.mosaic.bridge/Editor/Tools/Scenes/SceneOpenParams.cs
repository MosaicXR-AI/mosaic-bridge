using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Scenes
{
    public sealed class SceneOpenParams
    {
        [Required] public string Path { get; set; }
    }
}
