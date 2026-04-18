using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Contracts.Envelopes
{
    /// <summary>
    /// Standardized result wrapper for paginated list/query tool responses.
    /// Per FR9: responses include total, offset, limit, and hasMore for cursor-based navigation.
    /// </summary>
    /// <typeparam name="T">The item type in the result list.</typeparam>
    public class PaginatedResult<T>
    {
        /// <summary>The current page of items.</summary>
        [JsonProperty("items")]
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>Total number of items available across all pages.</summary>
        [JsonProperty("total")]
        public int Total { get; set; }

        /// <summary>The offset used to fetch this page.</summary>
        [JsonProperty("offset")]
        public int Offset { get; set; }

        /// <summary>The limit used to fetch this page.</summary>
        [JsonProperty("limit")]
        public int Limit { get; set; }

        /// <summary>True if more pages are available (Offset + Items.Count &lt; Total).</summary>
        [JsonProperty("hasMore")]
        public bool HasMore => Offset + (Items?.Count ?? 0) < Total;

        public PaginatedResult() { }

        public PaginatedResult(List<T> items, int total, int offset, int limit)
        {
            Items = items ?? new List<T>();
            Total = total;
            Offset = offset;
            Limit = limit;
        }
    }
}
