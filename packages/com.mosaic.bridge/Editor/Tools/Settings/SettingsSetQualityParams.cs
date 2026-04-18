namespace Mosaic.Bridge.Tools.Settings
{
    public sealed class SettingsSetQualityParams
    {
        public int? LevelIndex { get; set; }
        public string LevelName { get; set; }
        public bool ApplyExpensiveChanges { get; set; } = true;
    }
}
