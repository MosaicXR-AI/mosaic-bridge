namespace Mosaic.Bridge.Core.Server
{
    public sealed class HandlerRequest
    {
        public string Method { get; set; }     // uppercase, e.g. "POST"
        public string RawUrl { get; set; }     // includes query string
        public byte[] Body { get; set; }       // may be empty, never null
        public string ClientId { get; set; }   // populated from HMAC auth; "default" until Story 8.2
    }
}
