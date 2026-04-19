namespace Mosaic.Bridge.Tools.Selection
{
    public sealed class SelectionSetParams
    {
        /// <summary>Instance IDs of scene GameObjects or assets to select.</summary>
        public int[] InstanceIds { get; set; }

        /// <summary>Asset paths (e.g. "Assets/Foo.prefab") to select.</summary>
        public string[] AssetPaths { get; set; }

        /// <summary>
        /// Convenience: GameObject name to select (scene hierarchy search). If multiple
        /// scene objects share this name, all are selected. Either Name or Names may be used.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Convenience: multiple GameObject names to select at once.
        /// </summary>
        public string[] Names { get; set; }
    }
}
