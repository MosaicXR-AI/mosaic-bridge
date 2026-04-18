using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Contracts.Interfaces
{
    /// <summary>
    /// In-process client interface for invoking Mosaic Bridge tools without going through HTTP.
    /// Per FR34: the existing com.mosaic.ai chat window calls the bridge via this interface
    /// (referenced through the shared Mosaic.Bridge.Contracts assembly), bypassing HTTP serialization
    /// and HMAC authentication for in-process callers.
    /// </summary>
    /// <remarks>
    /// SECURITY NOTE: Per NFR24a, the trust boundary is the Unity Editor AppDomain, not the loopback
    /// network interface. Anything inside Unity's AppDomain is trusted (it has full Unity API access
    /// regardless of Mosaic Bridge). Cross-process callers (the Node.js MCP server, external MCP clients)
    /// MUST go through the HTTP listener with HMAC authentication. Do not expose this interface
    /// over IPC, gRPC, named pipes, or any other cross-process mechanism — that would break the trust model.
    /// </remarks>
    public interface IMosaicBridgeClient
    {
        /// <summary>
        /// Invoke a tool by its canonical route (e.g., "gameobject/create") with the given parameters.
        /// </summary>
        /// <param name="route">Canonical tool route from MosaicToolAttribute.Route.</param>
        /// <param name="parameters">Tool parameters as a typed object (will be marshalled to the tool's parameter class).</param>
        /// <param name="cancellationToken">Cancellation token. Per FR10, tools must respect cancellation within 500ms.</param>
        /// <returns>The tool result envelope.</returns>
        Task<ToolResult<object>> InvokeAsync(string route, object parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invoke a tool with a strongly-typed result.
        /// </summary>
        Task<ToolResult<T>> InvokeAsync<T>(string route, object parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// List all registered tools (the route table from TypeCache discovery).
        /// </summary>
        IReadOnlyList<ToolMetadata> ListTools();

        /// <summary>
        /// Check whether the bridge is currently ready to accept tool calls.
        /// Returns false during domain reload, initialization, or critical failure.
        /// </summary>
        bool IsReady { get; }
    }

    /// <summary>
    /// Metadata about a registered tool, returned from IMosaicBridgeClient.ListTools().
    /// </summary>
    public class ToolMetadata
    {
        public string Route { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public bool IsReadOnly { get; set; }
        public string ParameterTypeName { get; set; }
        public string ResultTypeName { get; set; }
    }
}
