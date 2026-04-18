using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiGoapPlanParams
    {
        [Required] public string GameObjectName { get; set; }
        public int               MaxPlanDepth   { get; set; } = 10;
    }
}
