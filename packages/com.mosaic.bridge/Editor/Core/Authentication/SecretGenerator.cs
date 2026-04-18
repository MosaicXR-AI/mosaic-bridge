using System;
using System.Security.Cryptography;

namespace Mosaic.Bridge.Core.Authentication
{
    /// <summary>
    /// Generates cryptographically random shared secrets for the HMAC handshake between
    /// the MCP server and the Unity Editor bridge.
    /// </summary>
    public static class SecretGenerator
    {
        private const int SecretLengthBytes = 32;

        /// <summary>
        /// Returns 32 cryptographically random bytes from the OS CSPRNG.
        /// </summary>
        public static byte[] Generate()
        {
            var bytes = new byte[SecretLengthBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return bytes;
        }

        /// <summary>
        /// Returns a 32-byte secret encoded as Base64.
        /// </summary>
        public static string GenerateBase64()
        {
            return Convert.ToBase64String(Generate());
        }
    }
}
