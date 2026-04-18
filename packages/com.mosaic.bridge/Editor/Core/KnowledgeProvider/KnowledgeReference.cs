namespace Mosaic.Bridge.Core.Knowledge
{
    /// <summary>
    /// Builds standardized knowledge base reference strings for inclusion
    /// in ToolResult.knowledgeBaseReferences.
    /// </summary>
    public static class KnowledgeReference
    {
        /// <summary>
        /// Creates a KB reference string in the format "category/entryKey [source]".
        /// Example: "physics/steel [NIST CODATA 2022]"
        /// </summary>
        public static string Create(string category, string entryKey, string source = null)
        {
            var reference = $"{category}/{entryKey}";
            if (!string.IsNullOrEmpty(source))
                reference += $" [{source}]";
            return reference;
        }
    }
}
