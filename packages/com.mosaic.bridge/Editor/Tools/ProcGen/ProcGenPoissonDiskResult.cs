namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenPoissonDiskResult
    {
        public float[][] Points          { get; set; }
        public int       Count           { get; set; }
        public string[]  GameObjectNames { get; set; }
        public float[]   BoundsUsedMin   { get; set; }
        public float[]   BoundsUsedMax   { get; set; }
    }
}
