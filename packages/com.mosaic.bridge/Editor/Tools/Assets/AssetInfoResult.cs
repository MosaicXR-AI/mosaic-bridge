namespace Mosaic.Bridge.Tools.Assets
{
    public sealed class AssetInfoResult
    {
        public string   Path         { get; set; }
        public string   Name         { get; set; }
        public string   Type         { get; set; }
        public string   FullTypeName { get; set; }
        public string   Guid         { get; set; }
        public long     FileSize     { get; set; }
        public string[] Labels       { get; set; }
        public bool     IsPrefab     { get; set; }
    }
}
