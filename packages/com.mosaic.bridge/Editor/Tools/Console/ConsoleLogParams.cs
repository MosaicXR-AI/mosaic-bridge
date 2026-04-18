using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.ConsoleTools
{
    public sealed class ConsoleLogParams
    {
        [Required] public string Message { get; set; }
        public string Level { get; set; } = "info";
    }
}
