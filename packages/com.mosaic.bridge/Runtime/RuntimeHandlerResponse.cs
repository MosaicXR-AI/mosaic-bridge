using System.Collections.Generic;

namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// Minimal response envelope for the runtime bridge.
    /// Mirrors <c>Mosaic.Bridge.Core.Server.HandlerResponse</c> without referencing the editor assembly.
    /// </summary>
    public sealed class RuntimeHandlerResponse
    {
        public int StatusCode { get; set; }
        public string ContentType { get; set; }
        public string Body { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public static RuntimeHandlerResponse NotReady() => new RuntimeHandlerResponse
        {
            StatusCode = 503,
            ContentType = "application/json",
            Body = "{\"error\":\"bridge_not_ready\"}"
        };

        public static RuntimeHandlerResponse InternalError(string message) => new RuntimeHandlerResponse
        {
            StatusCode = 500,
            ContentType = "application/json",
            Body = $"{{\"error\":\"internal_error\",\"message\":\"{EscapeJson(message)}\"}}"
        };

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
