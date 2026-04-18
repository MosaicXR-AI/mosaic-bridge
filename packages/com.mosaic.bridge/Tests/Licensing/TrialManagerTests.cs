using System;
using Mosaic.Bridge.Core.Licensing;
using NUnit.Framework;
using UnityEditor;

namespace Mosaic.Bridge.Tests.Licensing
{
    [TestFixture]
    public class TrialManagerTests
    {
        private const string ActivatedAtKey = "MosaicBridge.TrialActivatedAt";
        private const string DailyCountKey = "MosaicBridge.DailyCallCount";
        private const string DailyDateKey = "MosaicBridge.DailyCallDate";

        [SetUp]
        public void SetUp()
        {
            ClearEditorPrefs();
        }

        [TearDown]
        public void TearDown()
        {
            ClearEditorPrefs();
        }

        [Test]
        public void FirstAccess_InitializesTrialWithCurrentDate()
        {
            var before = DateTime.UtcNow;
            var manager = new TrialManager();

            // Accessing CurrentTier triggers initialization
            var tier = manager.CurrentTier;

            var stored = EditorPrefs.GetString(ActivatedAtKey, "");
            Assert.IsFalse(string.IsNullOrEmpty(stored), "ActivatedAt should be set after first access");

            var activatedAt = DateTime.Parse(stored, null, System.Globalization.DateTimeStyles.RoundtripKind);
            Assert.GreaterOrEqual(activatedAt, before.AddSeconds(-1));
            Assert.LessOrEqual(activatedAt, DateTime.UtcNow.AddSeconds(1));
            Assert.AreEqual(LicenseTier.Trial, tier);
        }

        [Test]
        public void TrialDaysRemaining_ReturnsCorrectValue()
        {
            // Set activation to 5 days ago
            var fiveDaysAgo = DateTime.UtcNow.AddDays(-5);
            EditorPrefs.SetString(ActivatedAtKey, fiveDaysAgo.ToString("O"));

            var manager = new TrialManager();
            Assert.AreEqual(9, manager.TrialDaysRemaining);
        }

        [Test]
        public void DailyQuota_ResetsAtMidnight()
        {
            var manager = new TrialManager();

            // Simulate calls from yesterday
            EditorPrefs.SetString(DailyDateKey, DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"));
            EditorPrefs.SetInt(DailyCountKey, 30);

            // Accessing DailyQuotaUsed should trigger reset
            Assert.AreEqual(0, manager.DailyQuotaUsed, "Daily count should reset when date changes");
        }

        [Test]
        public void RecordToolCall_IncrementsDailyCount()
        {
            var manager = new TrialManager();

            var result = manager.RecordToolCall();

            Assert.IsTrue(result, "First call should be allowed");
            Assert.AreEqual(1, manager.DailyQuotaUsed);
        }

        [Test]
        public void RecordToolCall_ReturnsFalseWhenQuotaExhausted()
        {
            var manager = new TrialManager();

            // Set daily count to the quota limit
            EditorPrefs.SetString(DailyDateKey, DateTime.Now.ToString("yyyy-MM-dd"));
            EditorPrefs.SetInt(DailyCountKey, 50);

            var result = manager.RecordToolCall();

            Assert.IsFalse(result, "Should be blocked when quota exhausted");
        }

        [Test]
        public void IsBlocked_WhenTrialExpired()
        {
            // Set activation to 15 days ago
            var fifteenDaysAgo = DateTime.UtcNow.AddDays(-15);
            EditorPrefs.SetString(ActivatedAtKey, fifteenDaysAgo.ToString("O"));

            var manager = new TrialManager();

            Assert.IsTrue(manager.IsBlocked, "Should be blocked when trial expired");
            Assert.AreEqual(BlockReason.TrialExpired, manager.GetBlockReason());
        }

        [Test]
        public void IsBlocked_WhenQuotaExhausted()
        {
            var manager = new TrialManager();

            // Exhaust the quota
            EditorPrefs.SetString(DailyDateKey, DateTime.Now.ToString("yyyy-MM-dd"));
            EditorPrefs.SetInt(DailyCountKey, 50);

            Assert.IsTrue(manager.IsBlocked, "Should be blocked when quota exhausted");
            Assert.AreEqual(BlockReason.QuotaExhausted, manager.GetBlockReason());
        }

        [Test]
        public void CurrentTier_ReturnsExpiredAfter14Days()
        {
            // Set activation to 15 days ago
            var fifteenDaysAgo = DateTime.UtcNow.AddDays(-15);
            EditorPrefs.SetString(ActivatedAtKey, fifteenDaysAgo.ToString("O"));

            var manager = new TrialManager();

            Assert.AreEqual(LicenseTier.Expired, manager.CurrentTier);
            Assert.AreEqual(0, manager.TrialDaysRemaining);
            Assert.IsFalse(manager.IsTrialActive);
        }

        private static void ClearEditorPrefs()
        {
            EditorPrefs.DeleteKey(ActivatedAtKey);
            EditorPrefs.DeleteKey(DailyCountKey);
            EditorPrefs.DeleteKey(DailyDateKey);
        }
    }
}
