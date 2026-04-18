using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Prefabs
{
    public sealed class PrefabGetOverridesResult
    {
        public string GameObjectName { get; set; }
        public string SourcePrefabPath { get; set; }
        public bool   HasOverrides { get; set; }

        /// <summary>Property-level overrides with current and source values.</summary>
        public List<OverrideDetail> PropertyOverrides { get; set; }

        /// <summary>Components added to the instance (not present in source).</summary>
        public List<AddedComponentEntry> AddedComponents { get; set; }

        /// <summary>Components removed from the instance (present in source).</summary>
        public List<RemovedComponentEntry> RemovedComponents { get; set; }

        /// <summary>Child GameObjects added to the instance.</summary>
        public List<AddedGameObjectEntry> AddedGameObjects { get; set; }

        /// <summary>Child GameObjects removed from the instance.</summary>
        public List<RemovedGameObjectEntry> RemovedGameObjects { get; set; }
    }

    public sealed class OverrideDetail
    {
        public string ComponentType { get; set; }
        public string PropertyPath  { get; set; }
        public string CurrentValue  { get; set; }
        public string SourceValue   { get; set; }
    }
}
