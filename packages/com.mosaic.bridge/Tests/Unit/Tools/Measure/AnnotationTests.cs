using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Measure;

namespace Mosaic.Bridge.Tests.Unit.Tools.Measure
{
    [TestFixture]
    [Category("Unit")]
    public class AnnotationTests
    {
        private GameObject _annotation;
        private GameObject _target;

        [TearDown]
        public void TearDown()
        {
            if (_annotation != null) Object.DestroyImmediate(_annotation);
            if (_target != null) Object.DestroyImmediate(_target);
            _annotation = null;
            _target = null;
        }

        private GameObject ResolveGO(int id)
        {
#pragma warning disable CS0618
            return EditorUtility.InstanceIDToObject(id) as GameObject;
#pragma warning restore CS0618
        }

        [Test]
        public void TextType_CreatesTextMeshAnnotation()
        {
            var result = AnnotationCreateTool.Execute(new AnnotationCreateParams
            {
                Type = "text",
                Text = "Hello",
                Position = new float[] { 1f, 2f, 3f }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("text", result.Data.Type);
            _annotation = ResolveGO(result.Data.AnnotationId);
            Assert.IsNotNull(_annotation);
            Assert.IsNotNull(_annotation.GetComponent<TextMesh>());
            Assert.AreEqual(result.Data.AnnotationId, _annotation.GetInstanceID());
        }

        [Test]
        public void LeaderLineType_CreatesLineRendererAndLabel()
        {
            var result = AnnotationCreateTool.Execute(new AnnotationCreateParams
            {
                Type = "leader_line",
                Text = "Leader",
                Position = new float[] { 0f, 0f, 0f },
                TargetPoint = new float[] { 2f, 2f, 0f }
            });

            Assert.IsTrue(result.Success, result.Error);
            _annotation = ResolveGO(result.Data.AnnotationId);
            Assert.IsNotNull(_annotation);
            Assert.IsNotNull(_annotation.GetComponent<LineRenderer>());
            // Label child
            Assert.GreaterOrEqual(_annotation.transform.childCount, 1);
        }

        [Test]
        public void ArrowType_CreatesArrowheadChild()
        {
            var result = AnnotationCreateTool.Execute(new AnnotationCreateParams
            {
                Type = "arrow",
                Text = "Arrow",
                Position = new float[] { 0f, 0f, 0f },
                TargetPoint = new float[] { 1f, 0f, 0f }
            });

            Assert.IsTrue(result.Success, result.Error);
            _annotation = ResolveGO(result.Data.AnnotationId);
            Assert.IsNotNull(_annotation);
            Assert.IsNotNull(_annotation.GetComponent<LineRenderer>());
            Assert.IsNotNull(_annotation.transform.Find("Arrowhead"));
        }

        [Test]
        public void DimensionType_CreatesTicks()
        {
            var result = AnnotationCreateTool.Execute(new AnnotationCreateParams
            {
                Type = "dimension",
                Position = new float[] { 0f, 0f, 0f },
                TargetPoint = new float[] { 5f, 0f, 0f }
            });

            Assert.IsTrue(result.Success, result.Error);
            _annotation = ResolveGO(result.Data.AnnotationId);
            Assert.IsNotNull(_annotation);
            Assert.IsNotNull(_annotation.transform.Find("TickStart"));
            Assert.IsNotNull(_annotation.transform.Find("TickEnd"));
        }

        [Test]
        public void CalloutType_CreatesBackgroundChild()
        {
            var result = AnnotationCreateTool.Execute(new AnnotationCreateParams
            {
                Type = "callout",
                Text = "Note",
                Position = new float[] { 0f, 1f, 0f },
                BackgroundColor = new float[] { 0f, 0f, 0f, 0.5f }
            });

            Assert.IsTrue(result.Success, result.Error);
            _annotation = ResolveGO(result.Data.AnnotationId);
            Assert.IsNotNull(_annotation);
            Assert.IsNotNull(_annotation.transform.Find("Background"));
        }

        [Test]
        public void PinType_CreatesVerticalLine()
        {
            var result = AnnotationCreateTool.Execute(new AnnotationCreateParams
            {
                Type = "pin",
                Text = "Spot",
                Position = new float[] { 3f, 5f, 2f }
            });

            Assert.IsTrue(result.Success, result.Error);
            _annotation = ResolveGO(result.Data.AnnotationId);
            Assert.IsNotNull(_annotation);
            var lr = _annotation.GetComponent<LineRenderer>();
            Assert.IsNotNull(lr);
            Assert.AreEqual(2, lr.positionCount);
            Vector3 p0 = lr.GetPosition(0);
            Vector3 p1 = lr.GetPosition(1);
            Assert.AreEqual(0f, p0.y, 1e-4f);
            Assert.AreEqual(5f, p1.y, 1e-4f);
        }

        [Test]
        public void InvalidType_ReturnsError()
        {
            var result = AnnotationCreateTool.Execute(new AnnotationCreateParams
            {
                Type = "hologram",
                Position = new float[] { 0f, 0f, 0f }
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void MissingPosition_ReturnsError()
        {
            var result = AnnotationCreateTool.Execute(new AnnotationCreateParams
            {
                Type = "text",
                Text = "NoPos"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void MissingText_ForTextType_UsesEmptyAndSucceeds()
        {
            var result = AnnotationCreateTool.Execute(new AnnotationCreateParams
            {
                Type = "text",
                Position = new float[] { 0f, 0f, 0f }
            });

            Assert.IsTrue(result.Success, result.Error);
            _annotation = ResolveGO(result.Data.AnnotationId);
            Assert.IsNotNull(_annotation);
            var tm = _annotation.GetComponent<TextMesh>();
            Assert.IsNotNull(tm);
            Assert.AreEqual(string.Empty, tm.text);
        }

        [Test]
        public void TargetGameObject_ParentsAnnotation()
        {
            _target = new GameObject("AnnotationTargetGO");
            _target.transform.position = new Vector3(10f, 0f, 0f);

            var result = AnnotationCreateTool.Execute(new AnnotationCreateParams
            {
                Type = "text",
                Text = "Pinned",
                Position = new float[] { 10f, 1f, 0f },
                TargetGameObject = "AnnotationTargetGO"
            });

            Assert.IsTrue(result.Success, result.Error);
            _annotation = ResolveGO(result.Data.AnnotationId);
            Assert.IsNotNull(_annotation);
            Assert.AreEqual(_target.transform, _annotation.transform.parent);
        }

        [Test]
        public void AnnotationId_MatchesInstanceId()
        {
            var result = AnnotationCreateTool.Execute(new AnnotationCreateParams
            {
                Type = "text",
                Text = "IdCheck",
                Position = new float[] { 0f, 0f, 0f },
                Name = "IdCheckAnnotation"
            });

            Assert.IsTrue(result.Success, result.Error);
            _annotation = ResolveGO(result.Data.AnnotationId);
            Assert.IsNotNull(_annotation);
            Assert.AreEqual(_annotation.GetInstanceID(), result.Data.AnnotationId);
            Assert.AreEqual("IdCheckAnnotation", result.Data.GameObjectName);
        }
    }
}
