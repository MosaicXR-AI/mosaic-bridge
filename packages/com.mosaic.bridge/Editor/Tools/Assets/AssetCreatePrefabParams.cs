namespace Mosaic.Bridge.Tools.Assets
{
    public sealed class AssetCreatePrefabParams
    {
        public string GameObjectName    { get; set; }
        public string PrefabPath        { get; set; }
        public bool   OverwriteExisting { get; set; } = false;
    }
}
