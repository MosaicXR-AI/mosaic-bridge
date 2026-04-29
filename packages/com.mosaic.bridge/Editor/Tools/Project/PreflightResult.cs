namespace Mosaic.Bridge.Tools.Project
{
    public sealed class PreflightResult
    {
        public string   UnityVersion      { get; set; }
        public string   RenderPipeline    { get; set; }  // BuiltIn | URP | HDRP | SRP
        public string   ColorProperty     { get; set; }  // _Color or _BaseColor
        public string   ActiveScenePath   { get; set; }
        public string   ActiveSceneName   { get; set; }
        public bool     SceneIsDirty      { get; set; }
        public string[] InstalledPackages { get; set; }  // package names present in manifest
        public int      ConsoleErrorCount  { get; set; }
        public int      ConsoleWarnCount   { get; set; }
        public string[] RecentErrors       { get; set; }  // up to 5 most recent errors
    }
}
