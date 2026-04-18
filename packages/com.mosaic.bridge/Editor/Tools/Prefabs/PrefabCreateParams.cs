namespace Mosaic.Bridge.Tools.Prefabs
{
    public sealed class PrefabCreateParams
    {
        public string GameObjectName    { get; set; }
        public string PrefabPath        { get; set; }
        public bool   OverwriteExisting { get; set; } = false;
    }
}
