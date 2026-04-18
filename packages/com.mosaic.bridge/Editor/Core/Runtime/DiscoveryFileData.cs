using Newtonsoft.Json;

namespace Mosaic.Bridge.Core.Runtime
{
    /// <summary>
    /// Schema written to bridge-discovery.json. Allows external MCP servers to discover
    /// the running bridge instance's port, authentication secret, and identity.
    /// </summary>
    public sealed class DiscoveryFileData
    {
        public const string CurrentSchemaVersion = "1.2";

        [JsonProperty("schema_version")]
        public string SchemaVersion { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("process_id")]
        public int ProcessId { get; set; }

        [JsonProperty("started_unix_seconds")]
        public long StartedUnixSeconds { get; set; }

        [JsonProperty("secret_base64")]
        public string SecretBase64 { get; set; }

        [JsonProperty("unity_project_path")]
        public string UnityProjectPath { get; set; }

        [JsonProperty("unity_version")]
        public string UnityVersion { get; set; }

        [JsonProperty("mcp_server_pid")]
        public int McpServerPid { get; set; }

        [JsonProperty("tls_enabled")]
        public bool TlsEnabled { get; set; }

        [JsonProperty("signature")]
        public string Signature { get; set; }
    }
}
