using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// Writes the runtime bridge discovery file to <c>Application.persistentDataPath/MosaicBridge/</c>.
    /// External tools read this file to discover the running bridge instance's port and secret.
    /// </summary>
    public static class RuntimeDiscoveryWriter
    {
        /// <summary>
        /// Returns the directory where the runtime discovery file is written.
        /// </summary>
        public static string GetDiscoveryDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "MosaicBridge");
        }

        /// <summary>
        /// Returns the full path to the runtime discovery file.
        /// </summary>
        public static string GetDiscoveryFilePath()
        {
            return Path.Combine(GetDiscoveryDirectory(), "bridge-discovery.json");
        }

        /// <summary>
        /// Writes the discovery file atomically.
        /// </summary>
        public static void Write(int port, string secretBase64, RuntimeLogger logger)
        {
            var dir = GetDiscoveryDirectory();
            Directory.CreateDirectory(dir);

            var data = new RuntimeDiscoveryData
            {
                schema_version = "1.2",
                port = port,
                process_id = GetProcessId(),
                started_unix_seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                secret_base64 = secretBase64,
                unity_version = Application.unityVersion,
                mode = "runtime"
            };

            var json = JsonUtility.ToJson(data, true);
            var target = GetDiscoveryFilePath();
            var tmp = target + ".tmp";

            File.WriteAllText(tmp, json);

            // Atomic rename
            try
            {
                if (File.Exists(target))
                    File.Delete(target);
                File.Move(tmp, target);
            }
            catch (IOException)
            {
                if (File.Exists(target))
                    File.Delete(target);
                File.Move(tmp, target);
            }

            // Set restrictive permissions on Unix
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ChmodFile(target, logger);
            }

            logger?.Info($"Runtime discovery file written: {target}");
        }

        /// <summary>
        /// Deletes the discovery file if it exists. Never throws.
        /// </summary>
        public static void Delete(RuntimeLogger logger)
        {
            try
            {
                var path = GetDiscoveryFilePath();
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                logger?.Warn($"Failed to delete runtime discovery file: {ex.Message}");
            }
        }

        private static int GetProcessId()
        {
            try
            {
                return Process.GetCurrentProcess().Id;
            }
            catch
            {
                return -1;
            }
        }

        private static void ChmodFile(string path, RuntimeLogger logger)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"600 \"{path}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                logger?.Warn($"chmod 600 failed for discovery file: {ex.Message}");
            }
        }

        [Serializable]
        private struct RuntimeDiscoveryData
        {
            public string schema_version;
            public int port;
            public int process_id;
            public long started_unix_seconds;
            public string secret_base64;
            public string unity_version;
            public string mode;
        }
    }
}
