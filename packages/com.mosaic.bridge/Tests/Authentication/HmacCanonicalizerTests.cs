using System.Text;
using Mosaic.Bridge.Core.Authentication;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Authentication
{
    [TestFixture]
    public class HmacCanonicalizerTests
    {
        [Test]
        public void Canonicalize_StandardInput_ProducesExpectedFormat()
        {
            var canonical = HmacCanonicalizer.Canonicalize(
                nonce: "abc",
                timestamp: "1700000000",
                method: "POST",
                path: "/api/run-tool",
                bodySha256: "deadbeef");

            const string expected =
                "v1\n" +
                "3:abc\n" +
                "10:1700000000\n" +
                "4:POST\n" +
                "13:/api/run-tool\n" +
                "8:deadbeef";

            Assert.AreEqual(expected, canonical);
        }

        [Test]
        public void Canonicalize_PathContainingEmbeddedNewline_LengthPrefixCoversFullByteCount()
        {
            // The path contains a literal \n byte (\u000A) — this is the parser-not-formatter
            // guarantee. The length prefix must reflect the full byte count including the
            // embedded newline so that a consumer reads exactly N bytes and is not fooled
            // into thinking the field ends at the embedded \n.
            const string evilPath = "/api/run-tool\u000Ainjected:line";

            var canonical = HmacCanonicalizer.Canonicalize(
                nonce: "n",
                timestamp: "1",
                method: "GET",
                path: evilPath,
                bodySha256: "h");

            var expectedPathLen = Encoding.UTF8.GetByteCount(evilPath);
            Assert.AreEqual(27, expectedPathLen, "sanity check on UTF-8 byte count of evil path");

            // The canonical string must contain "\n29:/api/run-tool\u000Ainjected:line"
            var fragment = "\n" + expectedPathLen + ":" + evilPath;
            StringAssert.Contains(fragment, canonical);

            // And the substring of length expectedPathLen following "29:" must equal the
            // entire path verbatim — newline included.
            var marker = "\n" + expectedPathLen + ":";
            var startIdx = canonical.IndexOf(marker, System.StringComparison.Ordinal) + marker.Length;
            var pathBytes = Encoding.UTF8.GetBytes(canonical.Substring(startIdx, evilPath.Length));
            Assert.AreEqual(Encoding.UTF8.GetBytes(evilPath), pathBytes);
        }

        [Test]
        public void Canonicalize_NonAsciiPath_UsesUtf8ByteCountNotCharCount()
        {
            // Arabic + emoji: char count != UTF-8 byte count
            const string path = "/مرحبا/🎮";
            var charCount = path.Length;
            var byteCount = Encoding.UTF8.GetByteCount(path);
            Assert.AreNotEqual(charCount, byteCount, "test premise: chars and bytes differ for this path");

            var canonical = HmacCanonicalizer.Canonicalize(
                nonce: "n",
                timestamp: "1",
                method: "GET",
                path: path,
                bodySha256: "h");

            // Length prefix must be the byte count, not the char count.
            StringAssert.Contains("\n" + byteCount + ":" + path, canonical);
            StringAssert.DoesNotContain("\n" + charCount + ":" + path, canonical);
        }

        [Test]
        public void ComputeBodySha256_EmptyBody_ReturnsSha256OfEmptyByteArray()
        {
            // SHA-256 of zero-length input is well-known.
            const string sha256OfEmpty = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

            Assert.AreEqual(sha256OfEmpty, HmacCanonicalizer.ComputeBodySha256(null));
            Assert.AreEqual(sha256OfEmpty, HmacCanonicalizer.ComputeBodySha256(new byte[0]));
        }

        [Test]
        public void ComputeBodySha256_KnownInput_ReturnsLowercaseHex()
        {
            // SHA-256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
            var hash = HmacCanonicalizer.ComputeBodySha256(Encoding.UTF8.GetBytes("abc"));
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
        }
    }
}
