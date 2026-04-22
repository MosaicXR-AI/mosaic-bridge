namespace Mosaic.Bridge.Tools.Scene
{
    public sealed class ScenePlanCompositionResult
    {
        public string   PlanId           { get; set; }
        public string   SceneName        { get; set; }
        public string   GeographicRef    { get; set; }
        public string[] Warnings         { get; set; }

        // Resolved lighting
        public SceneLightingParams Lighting        { get; set; }
        public float[]             CameraStart     { get; set; }
        public float[]             CameraLookAt    { get; set; }

        // Resolved landmark placements with sampled Y
        public ScenePlacedObject[] ObjectPlacements { get; set; }

        // Ordered execution plan
        public SceneExecutionPhase[] ExecutionPhases { get; set; }
    }

    public sealed class SceneLightingParams
    {
        public float   DirectionalAngle  { get; set; }
        public float[] DirectionalColor  { get; set; }
        public float   Intensity         { get; set; }
        public string  SkyboxPreset      { get; set; }
        public float   FogDensity        { get; set; }
    }

    public sealed class ScenePlacedObject
    {
        public string LandmarkName  { get; set; }
        public string RegionId      { get; set; }
        public float  WorldX        { get; set; }
        public float  WorldY        { get; set; }
        public float  WorldZ        { get; set; }
        public float  TerrainHeight { get; set; }
        public float  YOffset       { get; set; }
        public bool   HeightSampled { get; set; }
    }

    public sealed class SceneExecutionPhase
    {
        public int      Phase       { get; set; }
        public string   Name        { get; set; }
        public string[] ToolHints   { get; set; }
        public string   Note        { get; set; }
    }
}
