using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.Core.Security
{
    /// <summary>
    /// Windows credential store using DPAPI (<c>System.Security.Cryptography.ProtectedData</c>).
    /// Falls back to AES-encrypted EditorPrefs if ProtectedData is unavailable.
    /// Encrypted bytes are stored as base64 strings in EditorPrefs.
    /// </summary>
    public sealed class DpapiCredentialStore : ICredentialStore
    {
        private const string Prefix = "MosaicBridge.Cred.";
        private static readonly byte[] EntropyBytes = Encoding.UTF8.GetBytes("MosaicXR.Bridge.DPAPI.Entropy");

        // Lazily check whether ProtectedData is available at runtime.
        private static readonly bool HasProtectedData = CheckProtectedData();

        public bool Store(string key, string value)
        {
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(value);
                byte[] encrypted;

                if (HasProtectedData)
                {
                    encrypted = ProtectData(plainBytes, EntropyBytes);
                }
                else
                {
                    // Fallback: delegate to EditorPrefsCredentialStore behaviour
                    encrypted = EditorPrefsCredentialStore.AesEncrypt(plainBytes);
                }

                EditorPrefs.SetString(Prefix + key, Convert.ToBase64String(encrypted));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Mosaic] DPAPI store failed: {ex.Message}");
                return false;
            }
        }

        public string Retrieve(string key)
        {
            var b64 = EditorPrefs.GetString(Prefix + key, "");
            if (string.IsNullOrEmpty(b64)) return null;

            try
            {
                byte[] encrypted = Convert.FromBase64String(b64);
                byte[] plainBytes;

                if (HasProtectedData)
                {
                    plainBytes = UnprotectData(encrypted, EntropyBytes);
                }
                else
                {
                    plainBytes = EditorPrefsCredentialStore.AesDecrypt(encrypted);
                }

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Mosaic] DPAPI retrieve failed: {ex.Message}");
                return null;
            }
        }

        public bool Delete(string key)
        {
            var prefsKey = Prefix + key;
            if (!EditorPrefs.HasKey(prefsKey)) return false;
            EditorPrefs.DeleteKey(prefsKey);
            return true;
        }

        // --- ProtectedData wrappers via reflection (may not be in all Unity runtimes) ---

        private static bool CheckProtectedData()
        {
            try
            {
                var type = Type.GetType(
                    "System.Security.Cryptography.ProtectedData, System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                return type != null;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] ProtectData(byte[] data, byte[] entropy)
        {
            var type = Type.GetType(
                "System.Security.Cryptography.ProtectedData, System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            // DataProtectionScope.CurrentUser = 0
            var method = type.GetMethod("Protect", new[] { typeof(byte[]), typeof(byte[]), typeof(int).Assembly.GetType("System.Security.Cryptography.DataProtectionScope") });
            if (method == null)
            {
                // Try with enum type from the same assembly
                var scopeType = type.Assembly.GetType("System.Security.Cryptography.DataProtectionScope");
                method = type.GetMethod("Protect", new[] { typeof(byte[]), typeof(byte[]), scopeType });
                var currentUser = Enum.ToObject(scopeType, 0); // CurrentUser = 0
                return (byte[])method.Invoke(null, new object[] { data, entropy, currentUser });
            }
            return (byte[])method.Invoke(null, new object[] { data, entropy, 0 });
        }

        private static byte[] UnprotectData(byte[] data, byte[] entropy)
        {
            var type = Type.GetType(
                "System.Security.Cryptography.ProtectedData, System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            var scopeType = type.Assembly.GetType("System.Security.Cryptography.DataProtectionScope");
            var method = type.GetMethod("Unprotect", new[] { typeof(byte[]), typeof(byte[]), scopeType });
            var currentUser = Enum.ToObject(scopeType, 0);
            return (byte[])method.Invoke(null, new object[] { data, entropy, currentUser });
        }
    }
}
