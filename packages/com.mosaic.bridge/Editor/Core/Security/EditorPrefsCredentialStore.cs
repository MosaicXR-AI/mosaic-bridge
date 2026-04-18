using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.Core.Security
{
    /// <summary>
    /// Fallback credential store for platforms without a native keychain.
    /// Encrypts values with AES-256-CBC using a key derived from
    /// <c>SystemInfo.deviceUniqueIdentifier</c> and a static salt.
    /// The IV is prepended to the ciphertext and the result stored as base64 in EditorPrefs.
    /// </summary>
    public sealed class EditorPrefsCredentialStore : ICredentialStore
    {
        private const string Prefix = "MosaicBridge.SecCred.";
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("MosaicXR.Bridge.AES.Salt.2026");

        public bool Store(string key, string value)
        {
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(value);
                byte[] encrypted = AesEncrypt(plainBytes);
                EditorPrefs.SetString(Prefix + key, Convert.ToBase64String(encrypted));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Mosaic] Encrypted credential store failed: {ex.Message}");
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
                byte[] plainBytes = AesDecrypt(encrypted);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Mosaic] Encrypted credential retrieve failed: {ex.Message}");
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

        // --- AES helpers (also used by DpapiCredentialStore as fallback) ---

        internal static byte[] AesEncrypt(byte[] plainBytes)
        {
            byte[] aesKey = DeriveKey();

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = aesKey;
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    // Prepend IV (16 bytes) to ciphertext
                    byte[] result = new byte[aes.IV.Length + cipher.Length];
                    Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                    Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
                    return result;
                }
            }
        }

        internal static byte[] AesDecrypt(byte[] encryptedWithIv)
        {
            byte[] aesKey = DeriveKey();

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = aesKey;

                // Extract IV from first 16 bytes
                byte[] iv = new byte[16];
                Buffer.BlockCopy(encryptedWithIv, 0, iv, 0, 16);
                aes.IV = iv;

                int cipherLen = encryptedWithIv.Length - 16;
                byte[] cipher = new byte[cipherLen];
                Buffer.BlockCopy(encryptedWithIv, 16, cipher, 0, cipherLen);

                using (var decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                }
            }
        }

        private static byte[] DeriveKey()
        {
            // Machine-specific identifier as the key material
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            byte[] keyMaterial = Encoding.UTF8.GetBytes(deviceId);

            using (var deriveBytes = new Rfc2898DeriveBytes(keyMaterial, Salt, 10000, HashAlgorithmName.SHA256))
            {
                return deriveBytes.GetBytes(32); // 256-bit key
            }
        }
    }
}
