namespace Mosaic.Bridge.Core.Knowledge
{
    /// <summary>
    /// Generates standardized warnings when knowledge base entries are missing.
    /// Per FR31: tools must surface explicit "no data available" rather than fabricating values.
    /// </summary>
    public static class KnowledgeWarnings
    {
        /// <summary>
        /// Returns a warning message for a missing KB entry.
        /// </summary>
        /// <param name="category">KB category (e.g., "physics", "rendering")</param>
        /// <param name="key">The requested entry key (e.g., "mythril")</param>
        /// <param name="defaultValue">The default value that will be used instead</param>
        public static string NoDataAvailable(string category, string key, string defaultValue = null)
        {
            var msg = $"No knowledge base data available for {category}/{key}.";
            if (!string.IsNullOrEmpty(defaultValue))
                msg += $" Using default value {defaultValue} — verify this is appropriate for your use case.";
            return msg;
        }
    }
}
