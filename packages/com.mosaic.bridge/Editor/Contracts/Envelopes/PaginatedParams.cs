using Newtonsoft.Json;

namespace Mosaic.Bridge.Contracts.Envelopes
{
    /// <summary>
    /// Base class for pagination parameters on list/query tools.
    /// Per FR9: all list operations support cursor-based pagination via limit and offset.
    /// </summary>
    /// <remarks>
    /// Tool parameter classes that need pagination should inherit from this class.
    /// Defaults: limit=50, max=500, offset=0.
    /// </remarks>
    public class PaginatedParams
    {
        /// <summary>Maximum number of items to return. Default 50, maximum 500.</summary>
        [JsonProperty("limit")]
        public int Limit { get; set; } = 50;

        /// <summary>Number of items to skip from the beginning of the result set. Default 0.</summary>
        [JsonProperty("offset")]
        public int Offset { get; set; } = 0;

        /// <summary>Validate and clamp pagination parameters to safe values.</summary>
        public void ValidateAndClamp()
        {
            if (Limit < 1) Limit = 1;
            if (Limit > 500) Limit = 500;
            if (Offset < 0) Offset = 0;
        }
    }
}
