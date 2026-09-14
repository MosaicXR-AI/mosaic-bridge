using System;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Core.Extensibility;
using NUnit.Framework;
using UnityEngine;

namespace Mosaic.Bridge.Tests.Extensibility
{
    /// <summary>
    /// The seam gameobject/create, probuilder/create and asset/instantiate_prefab all call after
    /// creating an object. With no provider installed (the free Bridge alone) this must be a
    /// true no-op; with one installed, a defect in it must never surface as a failure of the
    /// (free, unrelated) creation tool that happened to trigger it.
    /// </summary>
    [TestFixture]
    public class ObjectCreationHooksTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("MosaicQaHookProbe");
        }

        [TearDown]
        public void TearDown()
        {
            ObjectCreationHooks.Provider = null;
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void NoProviderInstalled_ReturnsNull()
        {
            Assert.IsNull(ObjectCreationHooks.TryRun(_go, "gameobject/create"));
        }

        [Test]
        public void ProviderInstalled_ReturnsItsReport()
        {
            ObjectCreationHooks.Provider = (go, route) => new ObjectQaReport
            {
                Status = "pending_visual_check",
                Violations = Array.Empty<string>(),
                CapturePaths = new[] { "/tmp/angle-0.png" },
                Instruction = "look at it",
                QaId = "abc123",
            };

            var report = ObjectCreationHooks.TryRun(_go, "gameobject/create");

            Assert.IsNotNull(report);
            Assert.AreEqual("pending_visual_check", report.Status);
            Assert.AreEqual("abc123", report.QaId);
        }

        [Test]
        public void ProviderThrows_ReturnsNotAvailable_NeverPropagates()
        {
            ObjectCreationHooks.Provider = (go, route) => throw new InvalidOperationException("boom");

            ObjectQaReport report = null;
            Assert.DoesNotThrow(() => report = ObjectCreationHooks.TryRun(_go, "gameobject/create"));

            Assert.IsNotNull(report);
            Assert.AreEqual("not_available", report.Status);
        }

        [Test]
        public void PassesThroughTheGameObjectAndRouteName()
        {
            GameObject seen = null;
            string seenRoute = null;
            ObjectCreationHooks.Provider = (go, route) =>
            {
                seen = go;
                seenRoute = route;
                return null;
            };

            ObjectCreationHooks.TryRun(_go, "asset/instantiate_prefab");

            Assert.AreSame(_go, seen);
            Assert.AreEqual("asset/instantiate_prefab", seenRoute);
        }
    }
}
