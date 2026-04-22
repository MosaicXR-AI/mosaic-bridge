namespace Mosaic.Bridge.Tools.Scene
{
    public sealed class ScenePlanCompositionParams
    {
        public string SceneName       { get; set; } = "NewScene";
        public string GeographicRef   { get; set; } = "";

        // Terrain intent
        public float TerrainSizeX     { get; set; } = 1000f;
        public float TerrainSizeZ     { get; set; } = 1000f;
        public float MaxHeightMeters  { get; set; } = 200f;

        // Visual intent
        public string RenderPipeline  { get; set; } = "URP";
        public string TimeOfDay       { get; set; } = "midday";
        public string Weather         { get; set; } = "clear";
        public string PlayerType      { get; set; } = "fps";
        public string VisualStyle     { get; set; } = "realistic";

        // Scene regions (biomes + landmarks)
        public ScenePlanRegion[] Regions { get; set; }

        /// <summary>
        /// If true and an active terrain exists, the tool will sample terrain height
        /// at each landmark's XZ position to resolve world-space Y.
        /// If false (or no terrain exists), landmark Y is computed from region HeightRangeMin.
        /// </summary>
        public bool SampleExistingTerrain { get; set; } = true;
    }

    public sealed class ScenePlanRegion
    {
        public string   Id               { get; set; }
        public string   Name             { get; set; }
        public float    XMin             { get; set; }
        public float    XMax             { get; set; }
        public float    ZMin             { get; set; }
        public float    ZMax             { get; set; }
        public float    HeightRangeMin   { get; set; }
        public float    HeightRangeMax   { get; set; }
        public string   BiomeType        { get; set; } = "custom";
        public string   VegetationDensity { get; set; } = "sparse";
        public ScenePlanLandmark[] Landmarks { get; set; }
    }

    public sealed class ScenePlanLandmark
    {
        public string  Name       { get; set; }
        public string  Type       { get; set; }
        public float?  PreferredX { get; set; }
        public float?  PreferredZ { get; set; }
        public float   YOffset    { get; set; } = 0.1f;
    }
}
