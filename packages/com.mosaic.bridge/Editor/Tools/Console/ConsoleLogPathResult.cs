namespace Mosaic.Bridge.Tools.ConsoleTools
{
    public sealed class ConsoleLogPathResult
    {
        public string Path { get; set; }
        public bool Exists { get; set; }
        public int TotalLines { get; set; }
        public string[] RecentLines { get; set; }
    }
}
