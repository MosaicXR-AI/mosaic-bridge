using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Lighting;

namespace Mosaic.Bridge.Tests.Lighting
{
    [TestFixture]
    [Category("Lighting")]
    public class LightingInfoTests
    {
        private GameObject _testLight;

        [SetUp]
        public void SetUp()
        {
            _testLight = new GameObject("__MosaicTest_InfoLight__");
            var light = _testLight.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 3.5f;
            light.range = 20f;
            light.color = Color.green;
        }

        [TearDown]
        public void TearDown()
        {
            if (_testLight != null)
            {
                Object.DestroyImmediate(_testLight);
                _testLight = null;
            }
        }

        [Test]
        public void Info_SpecificLight_ReturnsProperties()
        {
            var result = LightingInfoTool.Execute(new LightingInfoParams
            {
                InstanceId = _testLight.GetInstanceID()
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Lights.Length);

            var info = result.Data.Lights[0];
            Assert.AreEqual("__MosaicTest_InfoLight__", info.Name);
            Assert.AreEqual("Point", info.LightType);
            Assert.AreEqual(3.5f, info.Intensity, 0.01f);
            Assert.AreEqual(20f, info.Range, 0.01f);
            Assert.AreEqual(0f, info.Color[0], 0.01f); // r
            Assert.AreEqual(1f, info.Color[1], 0.01f); // g
            Assert.AreEqual(0f, info.Color[2], 0.01f); // b
        }

        [Test]
        public void Info_ByName_ReturnsProperties()
        {
            var result = LightingInfoTool.Execute(new LightingInfoParams
            {
                Name = "__MosaicTest_InfoLight__"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Lights.Length);
            Assert.AreEqual("Point", result.Data.Lights[0].LightType);
        }

        [Test]
        public void Info_AllLights_IncludesTestLight()
        {
            var result = LightingInfoTool.Execute(new LightingInfoParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.GreaterOrEqual(result.Data.Lights.Length, 1);

            bool found = false;
            foreach (var l in result.Data.Lights)
            {
                if (l.Name == "__MosaicTest_InfoLight__")
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "Test light should appear in all-lights query");
        }

        [Test]
        public void Info_IncludesEnvironmentSettings()
        {
            var result = LightingInfoTool.Execute(new LightingInfoParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.Environment);
            Assert.IsNotNull(result.Data.Environment.AmbientMode);
            Assert.IsNotNull(result.Data.Environment.AmbientColor);
            Assert.AreEqual(3, result.Data.Environment.AmbientColor.Length);
        }

        [Test]
        public void Info_NotFound_ReturnsFail()
        {
            var result = LightingInfoTool.Execute(new LightingInfoParams
            {
                Name = "__NonExistent_Light_12345__"
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("not found"));
        }
    }
}
