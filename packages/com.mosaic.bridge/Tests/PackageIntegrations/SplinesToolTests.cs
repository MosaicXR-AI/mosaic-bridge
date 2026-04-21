#if MOSAIC_HAS_SPLINES
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Splines;

namespace Mosaic.Bridge.Tests.PackageIntegrations
{
    [TestFixture]
    [Category("PackageIntegration")]
    public class SplinesToolTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var name in new[] { "TestSpline", "TestSpline2" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Create_BasicSpline_ReturnsSuccess()
        {
            var result = SplineCreateTool.Execute(new SplineCreateParams
            {
                Name = "TestSpline",
                Knots = new[]
                {
                    new SplineKnotData { Position = new float[] { 0, 0, 0 } },
                    new SplineKnotData { Position = new float[] { 5, 2, 0 } },
                    new SplineKnotData { Position = new float[] { 10, 0, 5 } }
                }
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.KnotCount);
            Assert.IsNotNull(GameObject.Find("TestSpline"));
        }

        [Test]
        public void Create_ClosedSpline_ReturnsSuccess()
        {
            var result = SplineCreateTool.Execute(new SplineCreateParams
            {
                Name = "TestSpline2",
                Knots = new[]
                {
                    new SplineKnotData { Position = new float[] { 0, 0, 0 } },
                    new SplineKnotData { Position = new float[] { 5, 0, 0 } },
                    new SplineKnotData { Position = new float[] { 5, 0, 5 } }
                },
                Closed = true
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.Closed);
        }

        [Test]
        public void Create_TooFewKnots_ReturnsFail()
        {
            var result = SplineCreateTool.Execute(new SplineCreateParams
            {
                Name = "TestSpline",
                Knots = new[]
                {
                    new SplineKnotData { Position = new float[] { 0, 0, 0 } }
                }
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Info_AllSplines_ReturnsSuccess()
        {
            var result = SplineInfoTool.Execute(new SplineInfoParams());
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.Splines);
        }

        [Test]
        public void Info_AfterCreate_FindsSpline()
        {
            SplineCreateTool.Execute(new SplineCreateParams
            {
                Name = "TestSpline",
                Knots = new[]
                {
                    new SplineKnotData { Position = new float[] { 0, 0, 0 } },
                    new SplineKnotData { Position = new float[] { 5, 0, 0 } }
                }
            });
            var result = SplineInfoTool.Execute(new SplineInfoParams { GameObjectName = "TestSpline" });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Splines.Length);
        }

        [Test]
        public void AddKnot_Append_ReturnsSuccess()
        {
            SplineCreateTool.Execute(new SplineCreateParams
            {
                Name = "TestSpline",
                Knots = new[]
                {
                    new SplineKnotData { Position = new float[] { 0, 0, 0 } },
                    new SplineKnotData { Position = new float[] { 5, 0, 0 } }
                }
            });
            var result = SplineAddKnotTool.Execute(new SplineAddKnotParams
            {
                GameObjectName = "TestSpline",
                Action = "add",
                KnotData = new SplineKnotData { Position = new float[] { 10, 0, 0 } }
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.KnotCount);
        }

        [Test]
        public void AddKnot_NonExistentGO_ReturnsFail()
        {
            var result = SplineAddKnotTool.Execute(new SplineAddKnotParams
            {
                GameObjectName = "NonExistent",
                Action = "add",
                KnotData = new SplineKnotData { Position = new float[] { 0, 0, 0 } }
            });
            Assert.IsFalse(result.Success);
        }
    }
}
#endif
