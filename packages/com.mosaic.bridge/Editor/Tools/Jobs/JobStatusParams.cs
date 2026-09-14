using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Jobs
{
    public sealed class JobStatusParams
    {
        [Required] public string JobId { get; set; }
    }
}
