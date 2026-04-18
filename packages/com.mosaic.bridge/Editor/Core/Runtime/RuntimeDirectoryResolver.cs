using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Mosaic.Bridge.Contracts.Interfaces;

namespace Mosaic.Bridge.Core.Runtime
{
    /// <summary>
    /// Returns the per-user runtime directory for Mosaic Bridge, cross-platform.
    /// Creates the directory on first call and sets Unix permissions to 0700.
    /// </summary>
    public static class RuntimeDirectoryResolver
    {
        /// <summary>
        /// Returns the absolute runtime directory path, creating it if it does not exist.
        /// On Unix, sets directory permissions to 0700.
        /// </summary>
        public static string Resolve()
        {
            return Resolve(null);
        }

        /// <summary>
        /// Returns the absolute runtime directory path, creating it if it does not exist.
        /// On Unix, sets directory permissions to 0700. Chmod failures are logged and non-fatal.
        /// </summary>
        public static string Resolve(IMosaicLogger logger)
        {
            string path;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Mosaic", "Bridge");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = Path.Combine(home, "Library", "Application Support", "Mosaic", "Bridge");
            }
            else
            {
                // Linux: prefer XDG_RUNTIME_DIR if set and directory exists
                var xdgRuntime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
                if (!string.IsNullOrEmpty(xdgRuntime) && Directory.Exists(xdgRuntime))
                {
                    path = Path.Combine(xdgRuntime, "mosaic-bridge");
                }
                else
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    path = Path.Combine(home, ".cache", "mosaic-bridge");
                }
            }

            Directory.CreateDirectory(path);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ChmodDirectory(path, logger);
            }

            return path;
        }

        /// <summary>
        /// Returns the shared base path (parent of per-project directories).
        /// Used by InstanceRegistry to store the machine-wide instance-registry.json.
        /// </summary>
        public static string GetSharedBasePath()
        {
            return Resolve();
        }

        /// <summary>
        /// Returns a 16-hex-char project hash derived from Application.dataPath.
        /// Used to scope discovery files and instance registry entries per project.
        /// </summary>
        public static string GetProjectHash()
        {
            var dataPath = UnityEngine.Application.dataPath;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(dataPath);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Returns the full path to the bridge discovery file.
        /// </summary>
        public static string GetDiscoveryFilePath()
        {
            return Path.Combine(Resolve(), "bridge-discovery.json");
        }

        /// <summary>
        /// Returns the full path to the log directory, creating it if it does not exist.
        /// </summary>
        public static string GetLogDirectoryPath()
        {
            var logDir = Path.Combine(Resolve(), "logs");
            Directory.CreateDirectory(logDir);
            return logDir;
        }

        private static void ChmodDirectory(string path, IMosaicLogger logger)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"700 \"{path}\"",
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
                        logger?.Warn("chmod 700 failed on runtime directory",
                            ("path", path),
                            ("exitCode", (object)proc.ExitCode));
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Warn("chmod 700 threw an exception for runtime directory",
                    ("path", path),
                    ("exception", (object)ex.Message));
            }
        }
    }
}
