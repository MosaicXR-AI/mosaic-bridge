using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Scripts
{
    public sealed class ScriptCreateParams
    {
        [Required] public string Path      { get; set; }
        [Required] public string Content   { get; set; }
        public bool              Overwrite { get; set; }
    }
}
