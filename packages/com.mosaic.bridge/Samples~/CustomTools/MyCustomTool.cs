using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

// ──────────────────────────────────────────────────────────────────────────────
//  SAMPLE: Creating a custom Mosaic Bridge tool
//
//  This file demonstrates how third-party assemblies can register custom tools
//  with the Mosaic Bridge. Follow these steps:
//
//  1. Create an Assembly Definition (.asmdef) for your tools and add a
//     reference to "Mosaic.Bridge.Contracts" so you have access to
//     [MosaicTool], ToolResult<T>, and [Required].
//
//  2. Open Edit > Project Settings > Mosaic Bridge > Allowed Tool Assemblies
//     and add your assembly name (e.g. "com.acme.mosaic-tools").
//
//  3. Create a static method decorated with [MosaicTool("route", "description")].
//     The method must:
//       - Be static
//       - Accept a single parameter class (the input schema)
//       - Return ToolResult<T> (the response envelope)
//
//  4. Follow the 3-file pattern used by built-in tools:
//       - MyCustomToolParams.cs   — input parameters
//       - MyCustomToolResult.cs   — result data
//       - MyCustomTool.cs         — the tool method
//
//  5. Restart Unity (or trigger a domain reload) so the bridge discovers
//     your new tool via TypeCache.
//
//  Once registered, your tool is callable via MCP just like any built-in tool.
//  Use extensibility/list_custom_tools to verify it was discovered.
// ──────────────────────────────────────────────────────────────────────────────

namespace MyCompany.MosaicTools
{
    /// <summary>
    /// Input parameters for the custom/hello tool.
    /// Properties become the JSON input schema fields.
    /// Use [Required] to mark mandatory fields.
    /// </summary>
    public sealed class MyCustomToolParams
    {
        /// <summary>Name to greet. If null, defaults to "World".</summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// Result data returned by the custom/hello tool.
    /// This is wrapped in ToolResult&lt;T&gt; automatically.
    /// </summary>
    public sealed class MyCustomToolResult
    {
        /// <summary>The greeting message.</summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// A sample custom tool that demonstrates the extensibility pattern.
    /// When properly registered (assembly allowed in Project Settings),
    /// this tool appears as "mosaic_custom_hello" in MCP tool listings.
    /// </summary>
    public static class MyCustomTool
    {
        /// <summary>
        /// Returns a greeting message. This is the simplest possible custom tool.
        /// </summary>
        /// <param name="p">The tool parameters (Name is optional).</param>
        /// <returns>A ToolResult containing the greeting.</returns>
        [MosaicTool("custom/hello", "A sample custom tool that returns a greeting", isReadOnly: true)]
        public static ToolResult<MyCustomToolResult> Hello(MyCustomToolParams p)
        {
            var name = string.IsNullOrEmpty(p?.Name) ? "World" : p.Name;
            return ToolResult<MyCustomToolResult>.Ok(new MyCustomToolResult
            {
                Message = $"Hello, {name}! This is a custom Mosaic Bridge tool."
            });
        }
    }
}
