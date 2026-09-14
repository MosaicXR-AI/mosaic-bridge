using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Extensibility;
using Mosaic.Bridge.Tools.GameObjects;
using NUnit.Framework;
using UnityEngine;

namespace Mosaic.Bridge.Tests.GameObjects
{
    /// <summary>
    /// gameobject/create calls the object-quality hook right after registering Undo. These tests
    /// stand in for Mosaic.Pro.Core (not present in the free Bridge's own test run) by installing
    /// a fake provider, so the free tool's contract with that seam is covered even where Pro
    /// itself is not.
    /// </summary>
    [TestFixture]
    public class GameObjectCreateToolQaGateTests
    {
        [TearDown]
        public void TearDown()
        {
            ObjectCreationHooks.Provider = null;
            foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name.StartsWith("MosaicQaGate")) Object.DestroyImmediate(go);
        }

        [Test]
        public void NoProvider_CreatesNormally_QualityCheckIsNull()
        {
            var r = GameObjectCreateTool.Create(new GameObjectCreateParams { Name = "MosaicQaGateNoProvider" });

            Assert.IsTrue(r.Success, r.Error);
            Assert.IsNull(r.Data.QualityCheck);
            Assert.IsNotNull(GameObject.Find("MosaicQaGateNoProvider"));
        }

        [Test]
        public void ProviderReportsFailed_ToolCallFails_ObjectStaysInScene()
        {
            ObjectCreationHooks.Provider = (go, route) => new ObjectQaReport
            {
                Status = "failed",
                Violations = new[] { "pivot outside bounds" },
                CapturePaths = System.Array.Empty<string>(),
                Instruction = "fix it",
            };

            var r = GameObjectCreateTool.Create(new GameObjectCreateParams { Name = "MosaicQaGateFailed" });

            Assert.IsFalse(r.Success);
            Assert.AreEqual(ErrorCodes.OBJECT_QA_FAILED, r.ErrorCode);
            StringAssert.Contains("pivot outside bounds", r.Error);
            // The object itself is left in the scene — only the tool's success is withheld.
            Assert.IsNotNull(GameObject.Find("MosaicQaGateFailed"));
        }

        [Test]
        public void ProviderReportsPendingVisualCheck_ToolSucceeds_ReportAttached()
        {
            ObjectCreationHooks.Provider = (go, route) => new ObjectQaReport
            {
                Status = "pending_visual_check",
                Violations = System.Array.Empty<string>(),
                CapturePaths = new[] { "/tmp/angle-0-0deg.png" },
                Instruction = "look at it",
                QaId = "qa-1",
            };

            var r = GameObjectCreateTool.Create(new GameObjectCreateParams { Name = "MosaicQaGatePending" });

            Assert.IsTrue(r.Success, r.Error);
            Assert.IsNotNull(r.Data.QualityCheck);
            Assert.AreEqual("pending_visual_check", r.Data.QualityCheck.Status);
            Assert.AreEqual("qa-1", r.Data.QualityCheck.QaId);
        }

        [Test]
        public void ProviderReportsNotAvailable_ToolSucceeds_ReportAttached()
        {
            ObjectCreationHooks.Provider = (go, route) => new ObjectQaReport
            {
                Status = "not_available",
                Violations = System.Array.Empty<string>(),
                CapturePaths = System.Array.Empty<string>(),
                Instruction = "no licence",
            };

            var r = GameObjectCreateTool.Create(new GameObjectCreateParams { Name = "MosaicQaGateNotAvailable" });

            Assert.IsTrue(r.Success, r.Error);
            Assert.AreEqual("not_available", r.Data.QualityCheck.Status);
        }
    }
}
