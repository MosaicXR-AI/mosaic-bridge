namespace Mosaic.Bridge.Core.Server
{
    public sealed class HandlerResponse
    {
        public int StatusCode { get; set; }
        public string ContentType { get; set; }  // e.g. "application/json"
        public string Body { get; set; }          // UTF-8 string; may be empty
        public System.Collections.Generic.Dictionary<string, string> Headers { get; set; }

        public static HandlerResponse NotReady() => new HandlerResponse
        {
            StatusCode = 503,
            ContentType = "application/json",
            Body = "{\"error\":\"bridge_not_ready\"}"
        };

        public static HandlerResponse InternalError(string message) => new HandlerResponse
        {
            StatusCode = 500,
            ContentType = "application/json",
            Body = $"{{\"error\":\"internal_error\",\"message\":{Newtonsoft.Json.JsonConvert.SerializeObject(message)}}}"
        };
    }
}
