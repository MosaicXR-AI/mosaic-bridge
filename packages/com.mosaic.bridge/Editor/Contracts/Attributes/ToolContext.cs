namespace Mosaic.Bridge.Contracts.Attributes
{
    /// <summary>
    /// Declares the runtime compatibility context for a Mosaic Bridge tool.
    /// Used by tool discovery to filter which tools are available in editor vs. compiled builds.
    /// </summary>
    public enum ToolContext
    {
        /// <summary>Only available in Unity Editor (uses editor-only APIs like AssetDatabase, Undo, etc.).</summary>
        Editor = 0,

        /// <summary>Only available in compiled builds (no editor APIs).</summary>
        Runtime = 1,

        /// <summary>Available in both editor and compiled builds.</summary>
        Both = 2
    }
}
