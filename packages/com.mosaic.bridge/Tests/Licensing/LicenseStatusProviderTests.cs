using System;
using Mosaic.Bridge.Core.Licensing;
using NUnit.Framework;
using UnityEditor;

namespace Mosaic.Bridge.Tests.Licensing
{
    [TestFixture]
    public class EditorPrefsLicenseStatusProviderTests
    {
        private const string KeyTrialActivatedAt = "MosaicBridge.TrialActivatedAt";
        private const string KeyDailyCallCount = "MosaicBridge.DailyCallCount";
        private const string KeyDailyCallDate = "MosaicBridge.DailyCallDate";
        private const string KeyLicenseTier = "MosaicBridge.LicenseTier";

        [SetUp]
        public void SetUp()
        {
            EditorPrefs.DeleteKey(KeyTrialActivatedAt);
            EditorPrefs.DeleteKey(KeyDailyCallCount);
            EditorPrefs.DeleteKey(KeyDailyCallDate);
            EditorPrefs.DeleteKey(KeyLicenseTier);
        }

        [TearDown]
        public void TearDown()
        {
            EditorPrefs.DeleteKey(KeyTrialActivatedAt);
            EditorPrefs.DeleteKey(KeyDailyCallCount);
            EditorPrefs.DeleteKey(KeyDailyCallDate);
            EditorPrefs.DeleteKey(KeyLicenseTier);
        }

        [Test]
        public void NewInstall_TrialTier()
        {
            var p = new EditorPrefsLicenseStatusProvider();
            Assert.AreEqual(LicenseTier.Trial, p.CurrentTier);
        }

        [Test]
        public void TrialDaysRemaining_NewInstall_Returns14()
        {
            var p = new EditorPrefsLicenseStatusProvider();
            Assert.AreEqual(14, p.TrialDaysRemaining);
        }

        [Test]
        public void IsBlocked_NewInstall_ReturnsFalse()
        {
            var p = new EditorPrefsLicenseStatusProvider();
            Assert.IsFalse(p.IsBlocked);
            Assert.IsNull(p.GetBlockReason());
        }

        [Test]
        public void DailyQuota_Trial_Returns50()
        {
            var p = new EditorPrefsLicenseStatusProvider();
            Assert.AreEqual(50, p.DailyQuota);
        }

        [Test]
        public void RecordToolCall_IncrementsDailyQuotaUsed()
        {
            var p = new EditorPrefsLicenseStatusProvider();
            _ = p.TrialDaysRemaining; // activate trial
            Assert.AreEqual(0, p.DailyQuotaUsed);

            p.RecordToolCall();
            Assert.AreEqual(1, p.DailyQuotaUsed);
        }

        [Test]
        public void IsBlocked_QuotaExhausted()
        {
            var p = new EditorPrefsLicenseStatusProvider();
            _ = p.TrialDaysRemaining;

            for (int i = 0; i < 50; i++)
                p.RecordToolCall();

            Assert.IsTrue(p.IsBlocked);
            Assert.AreEqual(BlockReason.QuotaExhausted, p.GetBlockReason());
        }

        [Test]
        public void IsBlocked_TrialExpired()
        {
            var activated = DateTime.UtcNow.AddDays(-15);
            EditorPrefs.SetString(KeyTrialActivatedAt,
                activated.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            EditorPrefs.SetString(KeyLicenseTier, "trial");

            var p = new EditorPrefsLicenseStatusProvider();
            Assert.AreEqual(0, p.TrialDaysRemaining);
            Assert.IsTrue(p.IsBlocked);
            Assert.AreEqual(BlockReason.TrialExpired, p.GetBlockReason());
        }

        [Test]
        public void DailyQuota_PaidTier_ReturnsMaxValue()
        {
            EditorPrefs.SetString(KeyLicenseTier, "pro");
            var p = new EditorPrefsLicenseStatusProvider();
            Assert.AreEqual(int.MaxValue, p.DailyQuota);
        }

        [Test]
        public void IsBlocked_ExpiredTier_ReturnsGraceExpired()
        {
            EditorPrefs.SetString(KeyLicenseTier, "expired");
            var p = new EditorPrefsLicenseStatusProvider();
            Assert.IsTrue(p.IsBlocked);
            Assert.AreEqual(BlockReason.GraceExpired, p.GetBlockReason());
        }
    }

    [TestFixture]
    public class AlwaysAllowLicenseStatusProviderTests
    {
        [Test]
        public void AlwaysAllow_NeverBlocked()
        {
            var p = new AlwaysAllowLicenseStatusProvider();
            Assert.IsFalse(p.IsBlocked);
            Assert.IsNull(p.GetBlockReason());
            Assert.AreEqual(LicenseTier.Trial, p.CurrentTier);
            Assert.AreEqual(int.MaxValue, p.DailyQuota);
        }

        [Test]
        public void AlwaysAllow_RecordToolCall_ReturnsTrue()
        {
            var p = new AlwaysAllowLicenseStatusProvider();
            for (int i = 0; i < 100; i++)
                Assert.IsTrue(p.RecordToolCall());
            Assert.IsFalse(p.IsBlocked);
        }
    }
}
