namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenVoronoiParams
    {
        public float[][] Points           { get; set; }
        public int?      PointCount       { get; set; }
        public float[]   BoundsMin        { get; set; }
        public float[]   BoundsMax        { get; set; }
        public int?      Seed             { get; set; }
        public int?      RelaxIterations  { get; set; }
        public string    Output           { get; set; }
        public int?      TextureResolution { get; set; }
        public bool?     CreateGameObjects { get; set; }
        public string    SavePath         { get; set; }
    }
}
