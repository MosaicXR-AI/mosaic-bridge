namespace Mosaic.Bridge.Tools.Settings
{
    public sealed class SettingsGetRenderResult
    {
        // Canonical pipeline identifier: "BuiltIn" | "URP" | "HDRP" | "Custom".
        // Derived from the asset type name of GraphicsSettings.currentRenderPipeline.
        public string Pipeline { get; set; }

        // Asset type name (e.g. "UniversalRenderPipelineAsset", "HDRenderPipelineAsset")
        // or "Built-in" when no SRP is active. Stable across asset file renames.
        public string RenderPipelineAssetType { get; set; }

        // Asset file name (e.g. user-named "URP-HighFidelity"). May differ from type name.
        public string RenderPipelineAsset { get; set; }

        public string ColorSpace { get; set; }
        public bool HdrEnabled { get; set; }
        public string ActiveBuildTarget { get; set; }
        public string GraphicsApi { get; set; }
    }
}
