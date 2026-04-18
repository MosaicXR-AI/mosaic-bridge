using System;
using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Core.Licensing;

namespace Mosaic.Bridge.Tests.Licensing
{
    [TestFixture]
    public class OfflineGraceManagerTests
    {
        private OfflineGraceManager _manager;

        private static readonly string[] EditorPrefsKeys =
        {
            "MosaicBridge.LastValidatedAt",
            "MosaicBridge.LastSessionTime"
        };

        [SetUp]
        public void SetUp()
        {
            foreach (var key in EditorPrefsKeys)
                EditorPrefs.DeleteKey(key);

            _manager = new OfflineGraceManager();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var key in EditorPrefsKeys)
                EditorPrefs.DeleteKey(key);
        }

        [Test]
        public void GraceExpiresAt_NoValidation_ReturnsNull()
        {
            Assert.IsNull(_manager.GraceExpiresAt);
        }

        [Test]
        public void GraceExpiresAt_AfterValidation_Returns7DaysLater()
        {
            var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
            _manager.UtcNowProvider = () => now;
            _manager.RecordValidation();

            var expected = now.AddDays(7);
            Assert.AreEqual(expected, _manager.GraceExpiresAt);
        }

        [Test]
        public void IsGraceExpired_WithinPeriod_ReturnsFalse()
        {
            var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
            _manager.UtcNowProvider = () => now;
            _manager.RecordValidation();

            // Advance 3 days — still within 7-day window
            _manager.UtcNowProvider = () => now.AddDays(3);
            Assert.IsFalse(_manager.IsGraceExpired);
        }

        [Test]
        public void IsGraceExpired_After7Days_ReturnsTrue()
        {
            var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
            _manager.UtcNowProvider = () => now;
            _manager.RecordValidation();

            // Advance 8 days — past the 7-day window
            _manager.UtcNowProvider = () => now.AddDays(8);
            Assert.IsTrue(_manager.IsGraceExpired);
        }

        [Test]
        public void GraceDaysRemaining_FreshValidation_Returns7()
        {
            var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
            _manager.UtcNowProvider = () => now;
            _manager.RecordValidation();

            Assert.AreEqual(7, _manager.GraceDaysRemaining);
        }

        [Test]
        public void GraceDaysRemaining_After3Days_Returns4()
        {
            var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
            _manager.UtcNowProvider = () => now;
            _manager.RecordValidation();

            _manager.UtcNowProvider = () => now.AddDays(3);
            Assert.AreEqual(4, _manager.GraceDaysRemaining);
        }

        [Test]
        public void GraceDaysRemaining_Expired_Returns0()
        {
            var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
            _manager.UtcNowProvider = () => now;
            _manager.RecordValidation();

            _manager.UtcNowProvider = () => now.AddDays(10);
            Assert.AreEqual(0, _manager.GraceDaysRemaining);
        }

        [Test]
        public void IsClockTamperDetected_NormalUse_ReturnsFalse()
        {
            var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
            _manager.UtcNowProvider = () => now;
            _manager.RecordSessionTime();

            // Time moves forward normally
            _manager.UtcNowProvider = () => now.AddHours(6);
            Assert.IsFalse(_manager.IsClockTamperDetected);
        }

        [Test]
        public void IsClockTamperDetected_ClockMovedBack2Days_ReturnsTrue()
        {
            var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
            _manager.UtcNowProvider = () => now;
            _manager.RecordSessionTime();

            // Clock moved backward by 2 days
            _manager.UtcNowProvider = () => now.AddDays(-2);
            Assert.IsTrue(_manager.IsClockTamperDetected);
        }

        [Test]
        public void RecordValidation_UpdatesLastValidated()
        {
            var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
            _manager.UtcNowProvider = () => now;
            _manager.RecordValidation();

            var expected = now.AddDays(7);
            Assert.AreEqual(expected, _manager.GraceExpiresAt);

            // Record again at a later time
            var later = now.AddDays(2);
            _manager.UtcNowProvider = () => later;
            _manager.RecordValidation();

            var expectedLater = later.AddDays(7);
            Assert.AreEqual(expectedLater, _manager.GraceExpiresAt);
        }
    }
}
