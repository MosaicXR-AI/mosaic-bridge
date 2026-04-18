namespace Mosaic.Bridge.Tools.AssemblyDefs
{
    public sealed class AsmdefManageResult
    {
        public string Action { get; set; }
        public string Name { get; set; }
        public string FilePath { get; set; }
        public string[] References { get; set; }
        public string[] IncludePlatforms { get; set; }
        public string RootNamespace { get; set; }
        /// <summary>Only set for the list action — all asmdef paths found.</summary>
        public string[] AllPaths { get; set; }
    }
}
