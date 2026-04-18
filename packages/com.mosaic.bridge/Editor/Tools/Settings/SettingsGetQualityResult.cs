namespace Mosaic.Bridge.Tools.Settings
{
    public sealed class SettingsGetQualityResult
    {
        public string[] LevelNames { get; set; }
        public int CurrentLevelIndex { get; set; }
        public string CurrentLevelName { get; set; }
        public string[] VsyncOptions { get; set; }
    }
}
