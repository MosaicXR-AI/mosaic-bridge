using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Prefabs
{
    public sealed class PrefabRevertResult
    {
        public string GameObjectName { get; set; }
        public string Message        { get; set; }

        /// <summary>Total number of overrides that were reverted.</summary>
        public int RevertedCount { get; set; }

        /// <summary>Property paths that were reverted.</summary>
        public List<string> RevertedPropertyPaths { get; set; }

        /// <summary>Component types that were removed (added components reverted).</summary>
        public List<string> RevertedAddedComponents { get; set; }

        /// <summary>Component types that were restored (removed components reverted).</summary>
        public List<string> RevertedRemovedComponents { get; set; }

        /// <summary>GameObjects that were removed (added GOs reverted).</summary>
        public List<string> RevertedAddedGameObjects { get; set; }

        /// <summary>GameObjects that were restored (removed GOs reverted).</summary>
        public List<string> RevertedRemovedGameObjects { get; set; }
    }
}
