using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Lighting;

namespace Mosaic.Bridge.Tests.Lighting
{
    [TestFixture]
    [Category("Lighting")]
    public class LightingCreateTests
    {
        private GameObject _created;

        [TearDown]
        public void TearDown()
        {
            if (_created != null)
            {
                Object.DestroyImmediate(_created);
                _created = null;
            }
        }

        [Test]
        public void Create_DirectionalLight_ReturnsCorrectType()
        {
            var result = LightingCreateTool.Execute(new LightingCreateParams
            {
                Type = "Directional",
                Name = "__MosaicTest_DirLight__"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Directional", result.Data.LightType);
            Assert.AreEqual("__MosaicTest_DirLight__", result.Data.Name);

            _created = FindCreated(result.Data.InstanceId);
            Assert.IsNotNull(_created);
            var light = _created.GetComponent<Light>();
            Assert.IsNotNull(light);
            Assert.AreEqual(LightType.Directional, light.type);
        }

        [Test]
        public void Create_PointLight_WithCustomProperties()
        {
            var result = LightingCreateTool.Execute(new LightingCreateParams
            {
                Type      = "Point",
                Name      = "__MosaicTest_PointLight__",
                Intensity = 2.5f,
                Range     = 15f,
                Color     = new[] { 1f, 0f, 0f },
                Position  = new[] { 1f, 2f, 3f }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Point", result.Data.LightType);
            Assert.AreEqual(2.5f, result.Data.Intensity, 0.01f);

            _created = FindCreated(result.Data.InstanceId);
            var light = _created.GetComponent<Light>();
            Assert.AreEqual(LightType.Point, light.type);
            Assert.AreEqual(15f, light.range, 0.01f);
            Assert.AreEqual(1f, light.color.r, 0.01f);
            Assert.AreEqual(0f, light.color.g, 0.01f);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), _created.transform.position);
        }

        [Test]
        public void Create_SpotLight_SetsSpotAngle()
        {
            var result = LightingCreateTool.Execute(new LightingCreateParams
            {
                Type      = "Spot",
                Name      = "__MosaicTest_SpotLight__",
                SpotAngle = 45f
            });

            Assert.IsTrue(result.Success, result.Error);

            _created = FindCreated(result.Data.InstanceId);
            var light = _created.GetComponent<Light>();
            Assert.AreEqual(LightType.Spot, light.type);
            Assert.AreEqual(45f, light.spotAngle, 0.01f);
        }

        [Test]
        public void Create_InvalidType_ReturnsFail()
        {
            var result = LightingCreateTool.Execute(new LightingCreateParams
            {
                Type = "InvalidType"
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("Invalid light type"));
        }

        [Test]
        public void Create_DefaultName_UsesTypePlusLight()
        {
            var result = LightingCreateTool.Execute(new LightingCreateParams
            {
                Type = "Point"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.Name.Contains("Point Light"));

            _created = FindCreated(result.Data.InstanceId);
        }

        [Test]
        public void Create_CaseInsensitiveType_Succeeds()
        {
            var result = LightingCreateTool.Execute(new LightingCreateParams
            {
                Type = "directional",
                Name = "__MosaicTest_CaseInsensitive__"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Directional", result.Data.LightType);

            _created = FindCreated(result.Data.InstanceId);
        }

        private static GameObject FindCreated(int instanceId)
        {
#pragma warning disable CS0618
            return UnityEditor.EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#pragma warning restore CS0618
        }
    }
}
