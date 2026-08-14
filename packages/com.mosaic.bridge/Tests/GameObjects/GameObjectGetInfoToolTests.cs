using Mosaic.Bridge.Tools.GameObjects;
using NUnit.Framework;
using UnityEngine;

namespace Mosaic.Bridge.Tests.GameObjects
{
    /// <summary>
    /// `gameobject/get_info` returned components, tag and layer but no transform, which made a
    /// whole class of question unanswerable from outside the Editor: "did the player actually
    /// move?" could not be checked at all.
    ///
    /// That gap has a cost. A Play-Mode recording was accepted as gameplay because its pixels
    /// changed, when what changed was the scenery and the player never moved — and there was no
    /// way to measure the difference. Reading a position is the cheapest possible verification.
    /// </summary>
    [TestFixture]
    public class GameObjectGetInfoToolTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("MosaicGetInfoProbe");
            _go.transform.position = new Vector3(1.5f, 2.5f, -3.5f);
            _go.transform.localScale = new Vector3(2f, 2f, 2f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private static GameObjectGetInfoResult Info(string name)
        {
            var r = GameObjectGetInfoTool.GetInfo(new GameObjectGetInfoParams { Name = name });
            Assert.IsTrue(r.Success, r.Error);
            return r.Data;
        }

        [Test]
        public void GetInfo_ReturnsWorldPosition()
        {
            var d = Info("MosaicGetInfoProbe");
            Assert.IsNotNull(d.Position, "position was missing — the reason nothing could verify movement");
            Assert.AreEqual(1.5f, d.Position[0], 0.001f);
            Assert.AreEqual(2.5f, d.Position[1], 0.001f);
            Assert.AreEqual(-3.5f, d.Position[2], 0.001f);
        }

        [Test]
        public void GetInfo_ReturnsScaleAndRotation()
        {
            var d = Info("MosaicGetInfoProbe");
            Assert.AreEqual(2f, d.LocalScale[0], 0.001f);
            Assert.IsNotNull(d.Rotation);
            Assert.AreEqual(3, d.Rotation.Length);
        }

        [Test]
        public void GetInfo_PositionChangesWhenTheObjectMoves()
        {
            // The actual use: sample, act, sample again, subtract.
            var before = Info("MosaicGetInfoProbe").Position;
            _go.transform.position += new Vector3(5f, 0f, 0f);
            var after = Info("MosaicGetInfoProbe").Position;
            Assert.AreEqual(5f, after[0] - before[0], 0.001f);
        }

        [Test]
        public void GetInfo_FindsAnInactiveObject()
        {
            // GameObject.Find skips inactive objects, and a caller may well be asking about one
            // during Play Mode. Answering "not found" would be a lie.
            _go.SetActive(false);
            var d = Info("MosaicGetInfoProbe");
            Assert.IsFalse(d.ActiveSelf);
            Assert.IsNotNull(d.Position);
        }

        [Test]
        public void GetInfo_StillReportsTheExistingFields()
        {
            // The addition must not disturb what callers already read.
            var d = Info("MosaicGetInfoProbe");
            Assert.AreEqual("MosaicGetInfoProbe", d.Name);
            Assert.IsNotNull(d.Components);
            Assert.IsNotEmpty(d.Tag);
        }
    }
}
