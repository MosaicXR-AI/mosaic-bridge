using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Prefabs
{
    public sealed class PrefabInfoResult
    {
        public string       Path            { get; set; }
        public string       Name            { get; set; }
        public string       Guid            { get; set; }
        public string       PrefabType      { get; set; }
        public string       VariantBasePath { get; set; }
        public List<string> ComponentTypes  { get; set; }
        public int          ChildCount      { get; set; }

        // --- Override information ---

        /// <summary>True when the prefab instance has any overrides relative to its source.</summary>
        public bool HasOverrides { get; set; }

        /// <summary>Depth of the variant chain (0 = regular prefab, 1 = variant, 2+ = nested variant).</summary>
        public int VariantDepth { get; set; }

        /// <summary>Property-level overrides (component type + property path + modified value).</summary>
        public List<PropertyOverrideEntry> PropertyOverrides { get; set; }

        /// <summary>Components present on the instance but not in the source prefab.</summary>
        public List<AddedComponentEntry> AddedComponents { get; set; }

        /// <summary>Components present in the source prefab but removed on the instance.</summary>
        public List<RemovedComponentEntry> RemovedComponents { get; set; }

        /// <summary>Child GameObjects present on the instance but not in the source prefab.</summary>
        public List<AddedGameObjectEntry> AddedGameObjects { get; set; }

        /// <summary>Child GameObjects present in the source prefab but removed on the instance.</summary>
        public List<RemovedGameObjectEntry> RemovedGameObjects { get; set; }
    }

    public sealed class PropertyOverrideEntry
    {
        public string ComponentType { get; set; }
        public string PropertyPath  { get; set; }
        public string ModifiedValue { get; set; }
    }

    public sealed class AddedComponentEntry
    {
        public string ComponentType  { get; set; }
        public string GameObjectName { get; set; }
    }

    public sealed class RemovedComponentEntry
    {
        public string ComponentType  { get; set; }
        public string GameObjectName { get; set; }
    }

    public sealed class AddedGameObjectEntry
    {
        public string Name { get; set; }
    }

    public sealed class RemovedGameObjectEntry
    {
        public string Name { get; set; }
    }
}
