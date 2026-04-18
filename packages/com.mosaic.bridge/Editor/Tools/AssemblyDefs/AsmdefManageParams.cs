using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AssemblyDefs
{
    public sealed class AsmdefManageParams
    {
        /// <summary>Asset path to the .asmdef file (e.g., "Assets/Scripts/MyGame.Core.asmdef"). Required for info/add-references/set-platforms.</summary>
        public string Path { get; set; }
        /// <summary>Action to perform: info, list, add-references, set-platforms.</summary>
        [Required] public string Action { get; set; }
        /// <summary>Assembly references to add (used with add-references action).</summary>
        public string[] References { get; set; }
        /// <summary>Platform list to set (used with set-platforms action). Empty array = all platforms.</summary>
        public string[] Platforms { get; set; }
    }
}
