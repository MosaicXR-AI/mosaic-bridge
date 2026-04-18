namespace Mosaic.Bridge.Tools.Assets
{
    public sealed class AssetInstantiatePrefabResult
    {
        public string  Name       { get; set; }
        public int     InstanceId { get; set; }
        public string  PrefabPath { get; set; }
        public float[] Position   { get; set; }
    }
}
