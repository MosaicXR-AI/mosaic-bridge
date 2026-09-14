using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Assets
{
    public sealed class AssetInstantiatePrefabResult
    {
        public string  Name       { get; set; }
        public int     InstanceId { get; set; }
        public string  PrefabPath { get; set; }
        public float[] Position   { get; set; }

        /// <summary>Set only when an object-quality provider (Mosaic.Pro.Core) is installed.</summary>
        public ObjectQaReport QualityCheck { get; set; }
    }
}
