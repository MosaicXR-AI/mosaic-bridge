using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Jobs
{
    public sealed class JobCancelParams
    {
        [Required] public string JobId { get; set; }
    }
}
