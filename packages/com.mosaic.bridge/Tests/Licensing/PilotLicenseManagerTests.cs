using System;
using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Core.Licensing;

namespace Mosaic.Bridge.Tests.Licensing
{
    [TestFixture]
    public class PilotLicenseManagerTests
    {
        private PilotLicenseManager _manager;

        [SetUp]
        public void SetUp()
        {
            ClearEditorPrefs();
            _manager = new PilotLicenseManager();
        }

        [TearDown]
        public void TearDown()
        {
            ClearEditorPrefs();
        }

        [Test]
        public void IsPilot_WhenTierIsPilot_ReturnsTrue()
        {
            EditorPrefs.SetString("MosaicBridge.LicenseTier", "pilot");

            Assert.IsTrue(_manager.IsPilot);
        }

        [Test]
        public void IsPilot_WhenTierIsTrial_ReturnsFalse()
        {
            EditorPrefs.SetString("MosaicBridge.LicenseTier", "trial");

            Assert.IsFalse(_manager.IsPilot);
        }

        [Test]
        public void TelemetryEnabled_PilotTier_DefaultsTrue()
        {
            EditorPrefs.SetString("MosaicBridge.LicenseTier", "pilot");

            Assert.IsTrue(_manager.TelemetryEnabled);
        }

        [Test]
        public void TelemetryEnabled_NonPilot_ReturnsFalse()
        {
            EditorPrefs.SetString("MosaicBridge.LicenseTier", "trial");

            Assert.IsFalse(_manager.TelemetryEnabled);
        }

        [Test]
        public void PilotExpiresAt_SetAndGet_RoundTrips()
        {
            var expected = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

            _manager.PilotExpiresAt = expected;
            var actual = _manager.PilotExpiresAt;

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual.Value);
        }

        [Test]
        public void IsPilotExpired_BeforeExpiry_ReturnsFalse()
        {
            EditorPrefs.SetString("MosaicBridge.LicenseTier", "pilot");
            _manager.PilotExpiresAt = DateTime.UtcNow.AddDays(30);

            Assert.IsFalse(_manager.IsPilotExpired);
        }

        [Test]
        public void IsPilotExpired_AfterExpiry_ReturnsTrue()
        {
            EditorPrefs.SetString("MosaicBridge.LicenseTier", "pilot");
            _manager.PilotExpiresAt = DateTime.UtcNow.AddDays(-1);

            Assert.IsTrue(_manager.IsPilotExpired);
        }

        [Test]
        public void IsPilotExpired_NoExpiry_ReturnsFalse()
        {
            EditorPrefs.SetString("MosaicBridge.LicenseTier", "pilot");
            // No expiry set — permanent pilot

            Assert.IsFalse(_manager.IsPilotExpired);
        }

        [Test]
        public void ActivatePilot_SetsTierAndTelemetry()
        {
            var expiry = DateTime.UtcNow.AddDays(90);
            _manager.ActivatePilot(expiry);

            Assert.AreEqual("pilot", EditorPrefs.GetString("MosaicBridge.LicenseTier", ""));
            Assert.IsTrue(_manager.IsPilot);
            Assert.IsTrue(_manager.TelemetryEnabled);
            Assert.IsNotNull(_manager.PilotExpiresAt);
        }

        private static void ClearEditorPrefs()
        {
            EditorPrefs.DeleteKey("MosaicBridge.LicenseTier");
            EditorPrefs.DeleteKey("MosaicBridge.PilotExpiryAt");
            EditorPrefs.DeleteKey("MosaicBridge.PilotTelemetryEnabled");
        }
    }
}
