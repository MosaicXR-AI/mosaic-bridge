#if MOSAIC_HAS_VFX
using NUnit.Framework;
using UnityEngine;
using UnityEngine.VFX;
using Mosaic.Bridge.Tools.VFX;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tests.Unit.Tools.VFX
{
    // O4 §4.8: Visual Effect component — using shipped/Asset-Store VFX assets in HDRP/URP courses.
    // No .vfx graph asset exists anywhere in this repo (graphs can't be authored programmatically
    // — the doc's own note that "graph editing is not scriptable"), so these tests exercise the
    // real, reachable surface: creation, playback on an assetless VisualEffect, and property
    // validation. Assigning a real graph property is verified by API research (docs), not by test,
    // since no real .vfx asset is available to assign one against.
    [TestFixture]
    [Category("VFX")]
    public class VfxToolTests
    {
        private GameObject _testGo;

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null) Object.DestroyImmediate(_testGo);
        }

        [Test]
        public void Create_WithoutAssetPath_AddsEmptyVisualEffect()
        {
            var result = VfxCreateTool.Execute(new VfxCreateParams { Name = "VfxTestEmpty" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNull(result.Data.AssetPath);
            _testGo = UnityIds.Resolve(result.Data.InstanceId) as GameObject;
            Assert.IsNotNull(_testGo.GetComponent<VisualEffect>());
        }

        [Test]
        public void Create_UnknownAssetPath_ReturnsNotFound()
        {
            var result = VfxCreateTool.Execute(new VfxCreateParams
            {
                Name = "VfxTestBadAsset", AssetPath = "Assets/DoesNotExist.vfx",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Create_WithPosition_SetsTransform()
        {
            var result = VfxCreateTool.Execute(new VfxCreateParams
            {
                Name = "VfxTestPositioned", Position = new[] { 1f, 2f, 3f },
            });

            Assert.IsTrue(result.Success, result.Error);
            _testGo = UnityIds.Resolve(result.Data.InstanceId) as GameObject;
            Assert.AreEqual(new Vector3(1f, 2f, 3f), _testGo.transform.position);
        }

        [Test]
        public void Create_UnknownParent_ReturnsNotFound()
        {
            var result = VfxCreateTool.Execute(new VfxCreateParams
            {
                Name = "VfxTestOrphan", ParentName = "NoSuchParent_Mosaic",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Playback_PlayStopReinit_DoNotThrowOnAssetlessEffect()
        {
            var createResult = VfxCreateTool.Execute(new VfxCreateParams { Name = "VfxTestPlayback" });
            Assert.IsTrue(createResult.Success, createResult.Error);
            _testGo = UnityIds.Resolve(createResult.Data.InstanceId) as GameObject;

            var play = VfxPlaybackTool.Execute(new VfxPlaybackParams { Name = "VfxTestPlayback", Action = "play" });
            Assert.IsTrue(play.Success, play.Error);

            var stop = VfxPlaybackTool.Execute(new VfxPlaybackParams { Name = "VfxTestPlayback", Action = "stop" });
            Assert.IsTrue(stop.Success, stop.Error);

            var reinit = VfxPlaybackTool.Execute(new VfxPlaybackParams { Name = "VfxTestPlayback", Action = "reinit" });
            Assert.IsTrue(reinit.Success, reinit.Error);
        }

        [Test]
        public void Playback_EventWithoutName_ReturnsInvalidParam()
        {
            var createResult = VfxCreateTool.Execute(new VfxCreateParams { Name = "VfxTestEvent" });
            _testGo = UnityIds.Resolve(createResult.Data.InstanceId) as GameObject;

            var result = VfxPlaybackTool.Execute(new VfxPlaybackParams { Name = "VfxTestEvent", Action = "event" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Playback_UnknownAction_ReturnsInvalidParam()
        {
            var createResult = VfxCreateTool.Execute(new VfxCreateParams { Name = "VfxTestBadAction" });
            _testGo = UnityIds.Resolve(createResult.Data.InstanceId) as GameObject;

            var result = VfxPlaybackTool.Execute(new VfxPlaybackParams { Name = "VfxTestBadAction", Action = "rewind" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetProperty_NoSuchPropertyOnAssetlessEffect_ReturnsNotFound()
        {
            var createResult = VfxCreateTool.Execute(new VfxCreateParams { Name = "VfxTestNoProp" });
            _testGo = UnityIds.Resolve(createResult.Data.InstanceId) as GameObject;

            var result = VfxSetPropertyTool.Execute(new VfxSetPropertyParams
            {
                Name = "VfxTestNoProp", PropertyName = "Rate", ValueType = "float", FloatValue = 5f,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void SetProperty_MissingFloatValue_ReturnsInvalidParam()
        {
            var createResult = VfxCreateTool.Execute(new VfxCreateParams { Name = "VfxTestMissingValue" });
            _testGo = UnityIds.Resolve(createResult.Data.InstanceId) as GameObject;

            var result = VfxSetPropertyTool.Execute(new VfxSetPropertyParams
            {
                Name = "VfxTestMissingValue", PropertyName = "Rate", ValueType = "float",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetProperty_UnknownValueType_ReturnsInvalidParam()
        {
            var createResult = VfxCreateTool.Execute(new VfxCreateParams { Name = "VfxTestBadType" });
            _testGo = UnityIds.Resolve(createResult.Data.InstanceId) as GameObject;

            var result = VfxSetPropertyTool.Execute(new VfxSetPropertyParams
            {
                Name = "VfxTestBadType", PropertyName = "Rate", ValueType = "sparkle",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetProperty_NotFoundGameObject_ReturnsNotFound()
        {
            var result = VfxSetPropertyTool.Execute(new VfxSetPropertyParams
            {
                Name = "NoSuchVfxObject_Mosaic", PropertyName = "Rate", ValueType = "float", FloatValue = 1f,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Info_ReportsNoAssetAndZeroParticles()
        {
            var createResult = VfxCreateTool.Execute(new VfxCreateParams { Name = "VfxTestInfo" });
            _testGo = UnityIds.Resolve(createResult.Data.InstanceId) as GameObject;

            var result = VfxInfoTool.Execute(new VfxInfoParams { Name = "VfxTestInfo" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(result.Data.HasAsset);
            Assert.AreEqual(0, result.Data.AliveParticleCount);
            Assert.IsNotNull(result.Data.SystemNames);
        }

        [Test]
        public void Info_NoVisualEffectComponent_ReturnsNotFound()
        {
            _testGo = new GameObject("VfxTestNoComponent");

            var result = VfxInfoTool.Execute(new VfxInfoParams { Name = "VfxTestNoComponent" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}
#endif
