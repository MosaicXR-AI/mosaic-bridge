using System;
using System.Security.Cryptography;
using System.Text;

namespace Mosaic.Bridge.Core.Authentication
{
    /// <summary>
    /// Builds the canonical string that is HMAC-signed for each bridge request, plus the
    /// SHA-256 body hash that goes into it.
    /// </summary>
    /// <remarks>
    /// The canonical format is length-prefixed (UTF-8 byte count) for every field, so a
    /// consumer parses each field by reading exactly N bytes rather than scanning until the
    /// next newline. This defends against injection attacks where a field (notably the
    /// request path) contains a literal newline byte that would otherwise let an attacker
    /// forge field boundaries.
    /// </remarks>
    public static class HmacCanonicalizer
    {
        private const string Version = "v1";

        /// <summary>
        /// Builds the canonical signing string. Format:
        /// <code>
        /// v1\n&lt;len(nonce)&gt;:&lt;nonce&gt;\n&lt;len(timestamp)&gt;:&lt;timestamp&gt;\n&lt;len(method)&gt;:&lt;method&gt;\n&lt;len(path)&gt;:&lt;path&gt;\n&lt;len(bodySha256)&gt;:&lt;bodySha256&gt;
        /// </code>
        /// where each <c>&lt;len(x)&gt;</c> is the UTF-8 byte length of x (NOT character count).
        /// </summary>
        public static string Canonicalize(
            string nonce,
            string timestamp,
            string method,
            string path,
            string bodySha256)
        {
            if (nonce == null) throw new ArgumentNullException(nameof(nonce));
            if (timestamp == null) throw new ArgumentNullException(nameof(timestamp));
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (bodySha256 == null) throw new ArgumentNullException(nameof(bodySha256));

            var sb = new StringBuilder();
            sb.Append(Version);
            AppendLengthPrefixedField(sb, nonce);
            AppendLengthPrefixedField(sb, timestamp);
            AppendLengthPrefixedField(sb, method);
            AppendLengthPrefixedField(sb, path);
            AppendLengthPrefixedField(sb, bodySha256);
            return sb.ToString();
        }

        /// <summary>
        /// Returns the lowercase hex SHA-256 of the request body. An empty body hashes the
        /// empty byte array (never null), so the result is deterministic and unambiguous.
        /// </summary>
        public static string ComputeBodySha256(byte[] body)
        {
            var input = body ?? Array.Empty<byte>();
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(input);
                return ToLowerHex(hash);
            }
        }

        private static void AppendLengthPrefixedField(StringBuilder sb, string field)
        {
            sb.Append('\n');
            sb.Append(Encoding.UTF8.GetByteCount(field));
            sb.Append(':');
            sb.Append(field);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            const string hex = "0123456789abcdef";
            var chars = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = hex[bytes[i] >> 4];
                chars[i * 2 + 1] = hex[bytes[i] & 0x0F];
            }
            return new string(chars);
        }
    }
}
