using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiGoapValidateParams
    {
        [Required] public string GameObjectName { get; set; }
    }
}
