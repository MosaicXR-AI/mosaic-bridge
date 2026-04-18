using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Security;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Core.Runtime
{
    /// <summary>
    /// Reads and writes <see cref="DiscoveryFileData"/> atomically to the per-user runtime directory.
    /// The file is written to a .tmp path first and then renamed to prevent partial reads.
    /// </summary>
    public static class DiscoveryFile
    {
        /// <summary>
        /// Validates and atomically writes discovery data to bridge-discovery.json.
        /// Signs the file with HMAC-SHA256 using the bridge secret.
        /// On Unix, sets file permissions to 0600 after writing.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="data"/> fails validation.</exception>
        public static void Write(DiscoveryFileData data, byte[] secret, IMosaicLogger logger)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (string.IsNullOrEmpty(data.SchemaVersion) || !data.SchemaVersion.StartsWith("1."))
                throw new InvalidOperationException(
                    $"Unsupported schema version '{data.SchemaVersion}'. Expected 1.x.");

            if (data.Port < 1 || data.Port > 65535)
                throw new InvalidOperationException(
                    $"Port {data.Port} is out of valid range 1..65535.");

            if (string.IsNullOrEmpty(data.SecretBase64))
                throw new InvalidOperationException("SecretBase64 must not be empty.");

            // Compute HMAC-SHA256 signature over the JSON (without the signature field)
            data.Signature = null; // Clear before signing
            var unsignedJson = JsonConvert.SerializeObject(data, Formatting.Indented);

            if (secret != null && secret.Length > 0)
            {
                data.Signature = DiscoveryIntegrity.Sign(unsignedJson, secret);
            }

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            var target = RuntimeDirectoryResolver.GetDiscoveryFilePath();
            var tmp = target + ".tmp";

            File.WriteAllText(tmp, json);
            AtomicMove(tmp, target);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ChmodFile(target, logger);
            }

            logger?.Info("Discovery file written", ("path", (object)target));
        }

        /// <summary>
        /// Reads and validates the discovery file. Returns null if the file is absent,
        /// has an unrecognised schema version, or contains invalid field values.
        /// </summary>
        public static DiscoveryFileData Read(IMosaicLogger logger)
        {
            var path = RuntimeDirectoryResolver.GetDiscoveryFilePath();

            if (!File.Exists(path))
                return null;

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                logger?.Warn("Failed to read discovery file", ("path", (object)path), ("exception", (object)ex.Message));
                return null;
            }

            DiscoveryFileData data;
            try
            {
                data = JsonConvert.DeserializeObject<DiscoveryFileData>(json);
            }
            catch (JsonException ex)
            {
                logger?.Warn("Malformed JSON in discovery file", ("path", (object)path), ("exception", (object)ex.Message));
                return null;
            }

            if (data == null)
            {
                logger?.Warn("Discovery file deserialised to null", ("path", (object)path));
                return null;
            }

            // Accept any 1.x schema version for backward compatibility
            if (string.IsNullOrEmpty(data.SchemaVersion) || !data.SchemaVersion.StartsWith("1."))
            {
                logger?.Warn("Unsupported discovery file schema version",
                    ("schemaVersion", (object)data.SchemaVersion),
                    ("expected", (object)"1.x"));
                return null;
            }

            if (data.Port < 1 || data.Port > 65535)
            {
                logger?.Warn("Discovery file contains invalid port", ("port", (object)data.Port));
                return null;
            }

            if (string.IsNullOrEmpty(data.SecretBase64))
            {
                logger?.Warn("Discovery file contains empty secret_base64");
                return null;
            }

            return data;
        }

        /// <summary>
        /// Deletes the discovery file if it exists. Exceptions are caught and logged — this
        /// method must never throw because it runs on the shutdown path.
        /// </summary>
        public static void Delete(IMosaicLogger logger)
        {
            var path = RuntimeDirectoryResolver.GetDiscoveryFilePath();
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                logger?.Warn("Failed to delete discovery file on shutdown",
                    ("path", (object)path),
                    ("exception", (object)ex.Message));
            }
        }

        private static void AtomicMove(string src, string dst)
        {
            try
            {
                if (File.Exists(dst))
                    File.Delete(dst);
                File.Move(src, dst);
            }
            catch (IOException)
            {
                // Retry once — a concurrent reader may have recreated the file between
                // the delete and the move.
                if (File.Exists(dst))
                    File.Delete(dst);
                File.Move(src, dst);
            }
        }

        private static void ChmodFile(string path, IMosaicLogger logger)
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
                    if (proc.ExitCode != 0)
                    {
                        logger?.Warn("chmod 600 failed on discovery file",
                            ("path", (object)path),
                            ("exitCode", (object)proc.ExitCode));
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Warn("chmod 600 threw an exception for discovery file",
                    ("path", (object)path),
                    ("exception", (object)ex.Message));
            }
        }
    }
}
