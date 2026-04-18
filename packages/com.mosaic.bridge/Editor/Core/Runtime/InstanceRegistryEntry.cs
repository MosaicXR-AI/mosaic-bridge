using System;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Core.Runtime
{
    /// <summary>
    /// A single entry in the shared Mosaic Bridge instance registry.
    /// Each running Unity Editor with an active bridge registers one entry.
    /// Per FR18 and NFR58: supports up to 5 concurrent instances on the same machine.
    /// </summary>
    [Serializable]
    public sealed class InstanceRegistryEntry
    {
        /// <summary>OS process ID of the Unity Editor that owns this entry.</summary>
        [JsonProperty("pid")]
        public int Pid { get; set; }

        /// <summary>The port the bridge HTTP listener is bound to.</summary>
        [JsonProperty("port")]
        public int Port { get; set; }

        /// <summary>
        /// The 16-hex-char project hash (SHA-256 of Application.dataPath, truncated).
        /// Used to correlate entries with their per-project runtime directory.
        /// </summary>
        [JsonProperty("projectHash")]
        public string ProjectHash { get; set; }

        /// <summary>Full path to the Unity project (Application.dataPath without trailing /Assets).</summary>
        [JsonProperty("projectPath")]
        public string ProjectPath { get; set; }

        /// <summary>ISO 8601 UTC timestamp of when this entry was created or last refreshed.</summary>
        [JsonProperty("registeredAt")]
        public string RegisteredAt { get; set; }

        /// <summary>
        /// Creates a new entry with the current UTC timestamp.
        /// </summary>
        public static InstanceRegistryEntry Create(int pid, int port, string projectHash, string projectPath)
        {
            return new InstanceRegistryEntry
            {
                Pid = pid,
                Port = port,
                ProjectHash = projectHash,
                ProjectPath = projectPath,
                RegisteredAt = DateTime.UtcNow.ToString("o")
            };
        }
    }
}
