namespace Mosaic.Bridge.Tools.Assets
{
    public sealed class AssetCreatePrefabResult
    {
        public string PrefabPath     { get; set; }
        public string GameObjectName { get; set; }
        public bool   Overwritten    { get; set; }
    }
}
