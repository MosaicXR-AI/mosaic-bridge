using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public sealed class NavBehaviorTreeParams
    {
        [Required] public string   TreeName        { get; set; }
        public string[]            Nodes           { get; set; }
        public string              OutputDirectory { get; set; }
    }
}
