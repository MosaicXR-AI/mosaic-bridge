namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// Minimal request envelope for the runtime dispatcher.
    /// Mirrors <c>Mosaic.Bridge.Core.Server.HandlerRequest</c> without referencing the editor assembly.
    /// </summary>
    public sealed class RuntimeHandlerRequest
    {
        public string Method { get; set; }     // uppercase, e.g. "POST"
        public string RawUrl { get; set; }     // includes query string
        public byte[] Body { get; set; }       // may be empty, never null
        public string ClientId { get; set; }   // from HMAC auth header
    }
}
