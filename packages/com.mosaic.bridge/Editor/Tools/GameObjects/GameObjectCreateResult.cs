using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectCreateResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }

        /// <summary>Set only when an object-quality provider (Mosaic.Pro.Core) is installed.</summary>
        public ObjectQaReport QualityCheck { get; set; }
    }
}
