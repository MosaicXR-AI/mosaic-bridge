namespace Mosaic.Bridge.Contracts.Errors
{
    /// <summary>
    /// Canonical error code constants returned in ToolResult.ErrorCode.
    /// Per FR15 (ToolResult envelope) and FR25 (MCP error code mapping).
    /// </summary>
    /// <remarks>
    /// These are string constants rather than an enum so they remain stable in the wire format
    /// across plugin versions. New codes are added at the end; existing codes never change values.
    /// </remarks>
    public static class ErrorCodes
    {
        // === Parameter validation ===

        /// <summary>Required parameter is missing or null.</summary>
        public const string INVALID_PARAM = "INVALID_PARAM";

        /// <summary>Parameter type does not match the expected type.</summary>
        public const string TYPE_MISMATCH = "TYPE_MISMATCH";

        /// <summary>Parameter value is outside the allowed range or set.</summary>
        public const string OUT_OF_RANGE = "OUT_OF_RANGE";

        // === Tool execution ===

        /// <summary>Target object (GameObject, asset, component) was not found.</summary>
        public const string NOT_FOUND = "NOT_FOUND";

        /// <summary>Operation is not permitted in the current state.</summary>
        public const string NOT_PERMITTED = "NOT_PERMITTED";

        /// <summary>Operation conflicts with existing state.</summary>
        public const string CONFLICT = "CONFLICT";

        /// <summary>Internal bridge error (caught exception, unexpected state).</summary>
        public const string INTERNAL_ERROR = "INTERNAL_ERROR";

        // === Authentication and authorization ===

        /// <summary>Authentication token missing or invalid.</summary>
        public const string UNAUTHORIZED = "UNAUTHORIZED";

        /// <summary>Authentication valid but feature not enabled by license tier.</summary>
        public const string ENTITLEMENT_DENIED = "ENTITLEMENT_DENIED";

        /// <summary>Trial expired or quota exhausted.</summary>
        public const string TRIAL_EXPIRED = "TRIAL_EXPIRED";

        // === Rate limiting and backpressure ===

        /// <summary>Request rate limit exceeded for this client.</summary>
        public const string RATE_LIMITED = "RATE_LIMITED";

        /// <summary>Bridge queue is full; request rejected.</summary>
        public const string BRIDGE_BUSY = "BRIDGE_BUSY";

        // === Cancellation and lifecycle ===

        /// <summary>Tool execution cancelled (per FR10 cancellation tokens).</summary>
        public const string CANCELLED = "CANCELLED";

        /// <summary>Tool interrupted by Unity domain reload.</summary>
        public const string DOMAIN_RELOAD = "DOMAIN_RELOAD";

        /// <summary>Bridge is unavailable (initializing, shutting down, or in failed state).</summary>
        public const string BRIDGE_UNAVAILABLE = "BRIDGE_UNAVAILABLE";

        // === Script tool safety (NFR31-34) ===

        /// <summary>Script tool requires human approval; pending preview token returned.</summary>
        public const string APPROVAL_REQUIRED = "APPROVAL_REQUIRED";

        /// <summary>Script tool target path is outside the Assets/ allowlist.</summary>
        public const string PATH_NOT_ALLOWED = "PATH_NOT_ALLOWED";

        // === Tool execution failures (Story 1.7) ===

        /// <summary>Tool method threw an unhandled exception during execution.</summary>
        public const string TOOL_EXECUTION_FAILED = "TOOL_EXECUTION_FAILED";

        // === Backpressure (Story 1.11) ===

        /// <summary>Bridge queue at capacity; request rejected due to backpressure.</summary>
        public const string BRIDGE_BACKPRESSURE = "BRIDGE_BACKPRESSURE";

        /// <summary>Main thread is blocked; request accepted but deferred.</summary>
        public const string MAIN_THREAD_BLOCKED = "MAIN_THREAD_BLOCKED";

        // === Startup failures (Story 1.12) ===

        /// <summary>HttpListener failed to bind to loopback port.</summary>
        public const string PORT_BIND_FAILED = "PORT_BIND_FAILED";

        /// <summary>Discovery file has insecure ownership or permissions.</summary>
        public const string DISCOVERY_FILE_SECURITY = "DISCOVERY_FILE_SECURITY";

        /// <summary>TypeCache tool discovery failed or returned no methods.</summary>
        public const string TYPECACHE_FAILED = "TYPECACHE_FAILED";

        /// <summary>No tools found after TypeCache discovery completed.</summary>
        public const string NO_TOOLS_FOUND = "NO_TOOLS_FOUND";

        /// <summary>Unclassified bootstrap failure.</summary>
        public const string BOOTSTRAP_UNKNOWN = "BOOTSTRAP_UNKNOWN";
    }
}
