using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.EditorOps
{
    public sealed class EditorExecuteMenuItemParams
    {
        [Required] public string MenuPath { get; set; }
    }
}
