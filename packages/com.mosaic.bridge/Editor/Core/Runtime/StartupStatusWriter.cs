using System;
using System.IO;
using Mosaic.Bridge.Core.Bootstrap;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Core.Runtime
{
    /// <summary>
    /// Writes a machine-readable status.json to the runtime directory so that external
    /// consumers (MCP server, setup CLI, AI Chat panel) can detect whether the bridge
    /// started successfully or failed with a categorized error.
    ///
    /// The file is written atomically (write to .tmp, then rename) to prevent partial reads.
    /// Per NFR48 and Story 1.12.
    /// </summary>
    public static class StartupStatusWriter
    {
        /// <summary>Name of the status file within the runtime directory.</summary>
        public const string StatusFileName = "status.json";

        private const string TempSuffix = ".tmp";

        /// <summary>
        /// Write a success status indicating the bridge is running and accepting connections.
        /// </summary>
        public static void WriteSuccess(string runtimeDir, int port, int toolCount)
        {
            var status = new StartupStatus
            {
                State = BridgeState.Running.ToString(),
                Port = port,
                ToolCount = toolCount,
                Pid = GetCurrentPid(),
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                Error = null,
                ErrorCode = null,
                SuggestedFix = null
            };

            WriteAtomic(runtimeDir, status);
        }

        /// <summary>
        /// Write an error status indicating the bridge failed to start.
        /// </summary>
        public static void WriteError(string runtimeDir, string errorCode, string errorMessage,
            string suggestedFix = null)
        {
            var status = new StartupStatus
            {
                State = BridgeState.Error.ToString(),
                Port = 0,
                ToolCount = 0,
                Pid = GetCurrentPid(),
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                Error = errorMessage,
                ErrorCode = errorCode,
                SuggestedFix = suggestedFix
            };

            WriteAtomic(runtimeDir, status);
        }

        /// <summary>
        /// Reads and deserializes the current status.json from the runtime directory.
        /// Returns null if the file does not exist or cannot be parsed.
        /// </summary>
        public static StartupStatus ReadStatus(string runtimeDir)
        {
            string filePath = Path.Combine(runtimeDir, StatusFileName);
            if (!File.Exists(filePath))
                return null;

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<StartupStatus>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Deletes the status file during clean shutdown.
        /// </summary>
        public static void CleanUp(string runtimeDir)
        {
            string filePath = Path.Combine(runtimeDir, StatusFileName);
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // Best-effort cleanup; don't throw during shutdown.
            }
        }

        internal static void WriteAtomic(string runtimeDir, StartupStatus status)
        {
            if (!Directory.Exists(runtimeDir))
                Directory.CreateDirectory(runtimeDir);

            string targetPath = Path.Combine(runtimeDir, StatusFileName);
            string tempPath = targetPath + TempSuffix;

            string json = JsonConvert.SerializeObject(status, Formatting.Indented);

            File.WriteAllText(tempPath, json);

            if (File.Exists(targetPath))
                File.Delete(targetPath);
            File.Move(tempPath, targetPath);
        }

        private static int GetCurrentPid()
        {
            try
            {
                return System.Diagnostics.Process.GetCurrentProcess().Id;
            }
            catch
            {
                return -1;
            }
        }
    }

    /// <summary>
    /// Serializable status payload written to status.json.
    /// </summary>
    [Serializable]
    public class StartupStatus
    {
        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("toolCount")]
        public int ToolCount { get; set; }

        [JsonProperty("pid")]
        public int Pid { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; set; }

        [JsonProperty("errorCode", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorCode { get; set; }

        [JsonProperty("suggestedFix", NullValueHandling = NullValueHandling.Ignore)]
        public string SuggestedFix { get; set; }
    }
}
