using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Constraints;

namespace Mosaic.Bridge.Tests.Unit.Tools
{
    [TestFixture]
    [Category("Unit")]
    public class ConstraintToolTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ConstraintTestGO");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void ConstraintAdd_AddsPositionConstraint()
        {
            var result = ConstraintAddTool.Add(new ConstraintAddParams
            {
                Name = "ConstraintTestGO",
                Type = "Position"
            });

            Assert.IsTrue(result.Success);
            Assert.AreEqual("Position", result.Data.ConstraintType);
            Assert.AreEqual("PositionConstraint", result.Data.ComponentType);
            Assert.IsNotNull(_go.GetComponent<UnityEngine.Animations.PositionConstraint>());
        }

        [Test]
        public void ConstraintInfo_ReturnsConstraints()
        {
            _go.AddComponent<UnityEngine.Animations.RotationConstraint>();

            var result = ConstraintInfoTool.Info(new ConstraintInfoParams
            {
                Name = "ConstraintTestGO"
            });

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Data.Constraints.Length);
            Assert.AreEqual("Rotation", result.Data.Constraints[0].Type);
        }

        [Test]
        public void ConstraintAdd_InvalidType_Fails()
        {
            var result = ConstraintAddTool.Add(new ConstraintAddParams
            {
                Name = "ConstraintTestGO",
                Type = "Invalid"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
