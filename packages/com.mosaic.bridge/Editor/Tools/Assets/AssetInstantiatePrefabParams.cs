namespace Mosaic.Bridge.Tools.Assets
{
    public sealed class AssetInstantiatePrefabParams
    {
        public string  PrefabPath { get; set; }
        public string  Name       { get; set; }
        public float[] Position   { get; set; }
        public float[] Rotation   { get; set; }
    }
}
