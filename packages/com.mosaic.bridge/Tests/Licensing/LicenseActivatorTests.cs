using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Core.Licensing;
using Mosaic.Bridge.Core.Security;

namespace Mosaic.Bridge.Tests.Licensing
{
    [TestFixture]
    public class LicenseActivatorTests
    {
        private LicenseActivator _activator;
        private FakeCredentialStore _fakeStore;

        [SetUp]
        public void SetUp()
        {
            ClearEditorPrefs();
            _fakeStore = new FakeCredentialStore();
            CredentialStoreFactory.SetOverride(_fakeStore);
            _activator = new LicenseActivator();
        }

        [TearDown]
        public void TearDown()
        {
            ClearEditorPrefs();
            CredentialStoreFactory.SetOverride(null);
        }

        [Test]
        public void Activate_ValidKey_ReturnsSuccess()
        {
            var result = _activator.Activate("MOSAIC-INDIE-1234-5678");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void Activate_EmptyKey_ReturnsFail()
        {
            var result = _activator.Activate("");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("License key cannot be empty.", result.ErrorMessage);
        }

        [Test]
        public void Activate_ShortKey_ReturnsFail()
        {
            var result = _activator.Activate("SHORT");

            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("Invalid license key format", result.ErrorMessage);
        }

        [Test]
        public void Activate_PilotKey_ReturnsPilotTier()
        {
            var result = _activator.Activate("MOSAIC-PILOT-ABCD-EFGH");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(LicenseTier.Pilot, result.Tier);
        }

        [Test]
        public void Activate_ProKey_ReturnsProTier()
        {
            var result = _activator.Activate("MOSAIC-PRO-ABCD-EFGH-1234");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(LicenseTier.Pro, result.Tier);
        }

        [Test]
        public void Activate_TeamKey_ReturnsTeamTier()
        {
            var result = _activator.Activate("MOSAIC-TEAM-ABCD-EFGH");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(LicenseTier.Team, result.Tier);
        }

        [Test]
        public void Activate_GenericKey_ReturnsIndieTier()
        {
            var result = _activator.Activate("MOSAIC-ABCD-EFGH-1234");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(LicenseTier.Indie, result.Tier);
        }

        [Test]
        public void MaskedLicenseKey_ShowsFirstAndLastFour()
        {
            _activator.Activate("MOSAIC-INDIE-1234-5678");

            var masked = _activator.MaskedLicenseKey;

            Assert.AreEqual("MOSA****5678", masked);
        }

        [Test]
        public void Deactivate_RevertsToTrial()
        {
            _activator.Activate("MOSAIC-PRO-ABCD-EFGH-1234");
            _activator.Deactivate();

            Assert.IsFalse(_activator.HasLicenseKey);
            Assert.AreEqual("", _activator.MaskedLicenseKey);
            Assert.AreEqual("trial", EditorPrefs.GetString("MosaicBridge.LicenseTier", ""));
        }

        [Test]
        public void LicenseChanged_FiresOnActivate()
        {
            LicenseTier? firedTier = null;
            _activator.LicenseChanged += tier => firedTier = tier;

            _activator.Activate("MOSAIC-PRO-ABCD-EFGH-1234");

            Assert.IsNotNull(firedTier);
            Assert.AreEqual(LicenseTier.Pro, firedTier.Value);
        }

        [Test]
        public void Migration_MovesLegacyKeyToCredentialStore()
        {
            // Arrange: simulate a legacy plaintext key in EditorPrefs
            EditorPrefs.DeleteKey("MosaicBridge.CredentialMigrated");
            EditorPrefs.SetString("MosaicBridge.LicenseKey", "MOSAIC-INDIE-MIGR-TEST");
            var freshStore = new FakeCredentialStore();
            CredentialStoreFactory.SetOverride(freshStore);

            // Act: creating a new activator triggers migration
            var activator = new LicenseActivator();

            // Assert: key moved to credential store, cleared from EditorPrefs
            Assert.AreEqual("MOSAIC-INDIE-MIGR-TEST", freshStore.Retrieve("LicenseKey"));
            Assert.IsFalse(EditorPrefs.HasKey("MosaicBridge.LicenseKey"));
            Assert.IsTrue(EditorPrefs.GetBool("MosaicBridge.CredentialMigrated", false));
        }

        private static void ClearEditorPrefs()
        {
            EditorPrefs.DeleteKey("MosaicBridge.LicenseKey");
            EditorPrefs.DeleteKey("MosaicBridge.LicenseTier");
            EditorPrefs.DeleteKey("MosaicBridge.ActivationToken");
            EditorPrefs.DeleteKey("MosaicBridge.LastValidatedAt");
            EditorPrefs.DeleteKey("MosaicBridge.CredentialMigrated");
        }

        /// <summary>In-memory credential store for deterministic testing.</summary>
        private class FakeCredentialStore : ICredentialStore
        {
            private readonly Dictionary<string, string> _store = new Dictionary<string, string>();

            public bool Store(string key, string value)
            {
                _store[key] = value;
                return true;
            }

            public string Retrieve(string key)
            {
                return _store.TryGetValue(key, out var v) ? v : null;
            }

            public bool Delete(string key)
            {
                return _store.Remove(key);
            }
        }
    }
}
