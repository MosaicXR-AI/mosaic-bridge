namespace Mosaic.Bridge.Tools.Project
{
    public sealed class PreflightResult
    {
        public string   UnityVersion      { get; set; }

        // Active pipeline = QualitySettings override if present, else GraphicsSettings default.
        // This is what materials and shaders actually resolve against.
        public string   RenderPipeline    { get; set; }  // BuiltIn | URP | HDRP | SRP
        public string   ColorProperty     { get; set; }  // _Color or _BaseColor

        // Both sources reported separately so the caller can spot mismatches:
        // m_CustomRenderPipeline in GraphicsSettings.asset vs QualitySettings render pipeline override.
        public string   GraphicsPipelineAsset { get; set; }  // path or null
        public string   QualityPipelineAsset  { get; set; }  // path or null (override for current quality level)
        public string   ActiveQualityLevel    { get; set; }
        public bool     PipelineMismatch      { get; set; }  // true when Quality override differs from Graphics default

        // Input system (depends on Unity version + project Player Settings)
        public string   InputSystem           { get; set; }  // "New" | "Legacy" | "Both" | "Unknown"
        public bool     InputSystemPackageInstalled { get; set; }

        public string   ActiveScenePath   { get; set; }
        public string   ActiveSceneName   { get; set; }
        public bool     SceneIsDirty      { get; set; }
        public string[] InstalledPackages { get; set; }  // package names present in manifest
        public int      ConsoleErrorCount  { get; set; }
        public int      ConsoleWarnCount   { get; set; }
        public string[] RecentErrors       { get; set; }  // up to 5 most recent errors
    }
}
