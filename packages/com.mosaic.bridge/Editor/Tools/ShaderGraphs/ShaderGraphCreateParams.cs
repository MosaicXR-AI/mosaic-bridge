using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphCreateParams
    {
        [Required] public string Name { get; set; }

        /// <summary>Asset path, e.g. "Assets/Shaders/MyShader.shadergraph". Auto-appends extension if missing.</summary>
        [Required] public string Path { get; set; }

        /// <summary>Shader type: "Lit" or "Unlit". Defaults to "Lit".</summary>
        public string ShaderType { get; set; } = "Lit";

        /// <summary>Set to true to overwrite an existing file at the path.</summary>
        public bool OverwriteExisting { get; set; } = false;
    }
}
