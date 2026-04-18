using System.Diagnostics;
using UnityEngine;

namespace Mosaic.Bridge.Core.Security
{
    /// <summary>
    /// macOS Keychain credential store using the <c>security</c> CLI.
    /// Each credential is stored as a generic password with service
    /// <c>com.mosaicxr.bridge</c> and the caller-supplied key as the account name.
    /// </summary>
    public sealed class KeychainCredentialStore : ICredentialStore
    {
        private const string ServiceName = "com.mosaicxr.bridge";

        public bool Store(string key, string value)
        {
            // -U flag updates the entry if it already exists.
            var (exitCode, _) = RunSecurity(
                $"add-generic-password -a \"{Escape(key)}\" -s \"{ServiceName}\" -w \"{Escape(value)}\" -U");
            return exitCode == 0;
        }

        public string Retrieve(string key)
        {
            var (exitCode, output) = RunSecurity(
                $"find-generic-password -a \"{Escape(key)}\" -s \"{ServiceName}\" -w");
            if (exitCode != 0)
                return null;
            return output?.Trim();
        }

        public bool Delete(string key)
        {
            var (exitCode, _) = RunSecurity(
                $"delete-generic-password -a \"{Escape(key)}\" -s \"{ServiceName}\"");
            return exitCode == 0;
        }

        private static (int exitCode, string output) RunSecurity(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/security",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    var stdout = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(5000);
                    return (proc.ExitCode, stdout);
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Mosaic] Keychain access failed: {ex.Message}");
                return (-1, null);
            }
        }

        /// <summary>Escapes double-quotes and backslashes for shell arguments.</summary>
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
