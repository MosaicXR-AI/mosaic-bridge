using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AssemblyDefs
{
    public sealed class AsmdefCreateParams
    {
        /// <summary>Assembly definition name (e.g., "MyGame.Core").</summary>
        [Required] public string Name { get; set; }
        /// <summary>Folder path relative to Assets/ where the .asmdef file will be created.</summary>
        [Required] public string Path { get; set; }
        /// <summary>Optional assembly references (e.g., ["Unity.TextMeshPro", "Newtonsoft.Json"]).</summary>
        public string[] References { get; set; }
        /// <summary>Optional root namespace for the assembly.</summary>
        public string RootNamespace { get; set; }
        /// <summary>Optional platform include list (e.g., ["Editor", "Android"]). Empty = all platforms.</summary>
        public string[] IncludePlatforms { get; set; }
    }
}
