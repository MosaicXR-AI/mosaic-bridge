using System;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Core.Licensing;

namespace Mosaic.Bridge.Tests.Licensing
{
    [TestFixture]
    public class EntitlementClaimsTests
    {
        private const string KeyActivationToken = "MosaicBridge.ActivationToken";

        [TearDown]
        public void TearDown()
        {
            EditorPrefs.DeleteKey(KeyActivationToken);
        }

        [Test]
        public void Parse_ValidJwt_ExtractsTier()
        {
            var payload = new JObject
            {
                ["sub"] = "user@example.com",
                ["tier"] = "Pro",
                ["exp"] = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
                ["entitlement.tier1Tools"] = true,
                ["entitlement.tier2Tools"] = true
            };
            var jwt = CreateTestJwt(payload);

            var claims = EntitlementClaims.Parse(jwt);

            Assert.IsTrue(claims.IsValid);
            Assert.AreEqual(LicenseTier.Pro, claims.Tier);
            Assert.AreEqual("user@example.com", claims.Subject);
        }

        [Test]
        public void Parse_ExpiredJwt_IsExpiredTrue()
        {
            var payload = new JObject
            {
                ["sub"] = "user@example.com",
                ["tier"] = "Indie",
                ["exp"] = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
                ["entitlement.tier1Tools"] = true
            };
            var jwt = CreateTestJwt(payload);

            var claims = EntitlementClaims.Parse(jwt);

            Assert.IsTrue(claims.IsExpired);
            Assert.IsFalse(claims.IsValid);
        }

        [Test]
        public void Parse_ValidJwt_Tier1ToolsTrue()
        {
            var payload = new JObject
            {
                ["sub"] = "user@example.com",
                ["tier"] = "Indie",
                ["exp"] = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
                ["entitlement.tier1Tools"] = true,
                ["entitlement.tier2Tools"] = false
            };
            var jwt = CreateTestJwt(payload);

            var claims = EntitlementClaims.Parse(jwt);

            Assert.IsTrue(claims.Tier1Tools);
            Assert.IsFalse(claims.Tier2Tools);
        }

        [Test]
        public void Parse_InvalidFormat_IsValidFalse()
        {
            var claims = EntitlementClaims.Parse("not-a-jwt");

            Assert.IsFalse(claims.IsValid);
        }

        [Test]
        public void Parse_MalformedBase64_IsValidFalse()
        {
            var claims = EntitlementClaims.Parse("header.!!!invalid-base64!!!.signature");

            Assert.IsFalse(claims.IsValid);
        }

        [Test]
        public void IsToolTierAllowed_Tier1_ReturnsTrue()
        {
            var payload = new JObject
            {
                ["sub"] = "user@example.com",
                ["tier"] = "Indie",
                ["exp"] = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
                ["entitlement.tier1Tools"] = true,
                ["entitlement.tier2Tools"] = false
            };
            var jwt = CreateTestJwt(payload);

            var claims = EntitlementClaims.Parse(jwt);

            Assert.IsTrue(claims.IsToolTierAllowed(1));
        }

        [Test]
        public void IsToolTierAllowed_Tier2_WhenNotEntitled_ReturnsFalse()
        {
            var payload = new JObject
            {
                ["sub"] = "user@example.com",
                ["tier"] = "Indie",
                ["exp"] = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
                ["entitlement.tier1Tools"] = true,
                ["entitlement.tier2Tools"] = false
            };
            var jwt = CreateTestJwt(payload);

            var claims = EntitlementClaims.Parse(jwt);

            Assert.IsFalse(claims.IsToolTierAllowed(2));
        }

        [Test]
        public void LoadFromEditorPrefs_NoToken_ReturnsNull()
        {
            EditorPrefs.DeleteKey(KeyActivationToken);

            var claims = EntitlementClaims.LoadFromEditorPrefs();

            Assert.IsNull(claims);
        }

        private static string CreateTestJwt(JObject payload)
        {
            var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
            var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.ToString()));
            var sig = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-signature"));
            return $"{header}.{body}.{sig}";
        }
    }
}
