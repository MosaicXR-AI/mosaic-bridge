namespace Mosaic.Bridge.Tools.Settings
{
    public sealed class SettingsGetPlayerResult
    {
        public string CompanyName { get; set; }
        public string ProductName { get; set; }
        public string Version { get; set; }
        public string BundleIdentifier { get; set; }
        public string ScriptingBackend { get; set; }
        public string ApiCompatibilityLevel { get; set; }
        public string ActiveBuildTarget { get; set; }
    }
}
