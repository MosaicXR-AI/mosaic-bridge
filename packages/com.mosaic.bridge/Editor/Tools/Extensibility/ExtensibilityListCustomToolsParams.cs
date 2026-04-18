namespace Mosaic.Bridge.Tools.Extensibility
{
    /// <summary>
    /// Parameters for the extensibility/list_custom_tools tool.
    /// </summary>
    public sealed class ExtensibilityListCustomToolsParams
    {
        /// <summary>
        /// Optional filter — only return tools whose assembly name contains this string
        /// (case-insensitive). If null or empty, all custom tools are returned.
        /// </summary>
        public string AssemblyFilter { get; set; }
    }
}
