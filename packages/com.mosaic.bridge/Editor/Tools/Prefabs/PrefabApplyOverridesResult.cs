using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Prefabs
{
    public sealed class PrefabApplyOverridesResult
    {
        public string GameObjectName { get; set; }
        public int    AppliedCount   { get; set; }
        public string Message        { get; set; }

        /// <summary>Property paths that were applied.</summary>
        public List<string> AppliedPropertyPaths { get; set; }

        /// <summary>Component types that were added (applied to source).</summary>
        public List<string> AppliedAddedComponents { get; set; }

        /// <summary>Breakdown of override counts by category.</summary>
        public ApplyCountBreakdown Breakdown { get; set; }
    }

    public sealed class ApplyCountBreakdown
    {
        public int PropertyOverrides  { get; set; }
        public int AddedComponents    { get; set; }
        public int RemovedComponents  { get; set; }
        public int AddedGameObjects   { get; set; }
        public int RemovedGameObjects { get; set; }
    }
}
