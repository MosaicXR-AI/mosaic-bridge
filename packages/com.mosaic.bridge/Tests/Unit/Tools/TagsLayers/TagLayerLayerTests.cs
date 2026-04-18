using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.TagsLayers;

namespace Mosaic.Bridge.Tests.Unit.Tools.TagsLayers
{
    [TestFixture]
    public class TagLayerLayerTests
    {
        // ── List ─────────────────────────────────────────────────────────────

        [Test]
        public void List_Returns32Entries()
        {
            var result = TagLayerLayerTool.Execute(new TagLayerLayerParams { Action = "list" });

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data.Layers);
            Assert.AreEqual(32, result.Data.Layers.Length, "Unity always has exactly 32 layer slots");
        }

        [Test]
        public void List_ContainsDefaultLayer()
        {
            var result = TagLayerLayerTool.Execute(new TagLayerLayerParams { Action = "list" });

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Data.Layers[0].Index);
            Assert.AreEqual("Default", result.Data.Layers[0].Name);
        }

        // ── Set by name ──────────────────────────────────────────────────────

        [Test]
        public void Set_ByLayerName_AssignsLayer()
        {
            var go = new GameObject("LayerSetNameTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerLayerTool.Execute(new TagLayerLayerParams
                {
                    Action = "set",
                    GameObjectName = "LayerSetNameTest",
                    LayerName = "UI"
                });

                Assert.IsTrue(result.Success);
                Assert.AreEqual("LayerSetNameTest", result.Data.GameObjectName);
                Assert.AreEqual("UI", result.Data.AssignedLayerName);
                Assert.AreEqual(LayerMask.NameToLayer("UI"), go.layer);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── Set by index ─────────────────────────────────────────────────────

        [Test]
        public void Set_ByLayerIndex_AssignsLayer()
        {
            var go = new GameObject("LayerSetIndexTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerLayerTool.Execute(new TagLayerLayerParams
                {
                    Action = "set",
                    GameObjectName = "LayerSetIndexTest",
                    LayerIndex = 5 // Built-in "UI" layer is 5
                });

                Assert.IsTrue(result.Success);
                Assert.AreEqual(5, result.Data.AssignedLayerIndex);
                Assert.AreEqual(5, go.layer);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Set_ByInstanceId_AssignsLayer()
        {
            var go = new GameObject("LayerSetIdTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerLayerTool.Execute(new TagLayerLayerParams
                {
                    Action = "set",
                    InstanceId = go.GetInstanceID(),
                    LayerName = "UI"
                });

                Assert.IsTrue(result.Success);
                Assert.AreEqual("UI", result.Data.AssignedLayerName);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── Error cases ──────────────────────────────────────────────────────

        [Test]
        public void Set_MissingGameObject_ReturnsFail()
        {
            var result = TagLayerLayerTool.Execute(new TagLayerLayerParams
            {
                Action = "set",
                GameObjectName = "NonExistent_LayerTest_12345",
                LayerName = "Default"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Set_InvalidLayerName_ReturnsFail()
        {
            var go = new GameObject("LayerInvalidNameTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerLayerTool.Execute(new TagLayerLayerParams
                {
                    Action = "set",
                    GameObjectName = "LayerInvalidNameTest",
                    LayerName = "NonExistentLayer_12345"
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual("NOT_FOUND", result.ErrorCode);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Set_OutOfRangeIndex_ReturnsFail()
        {
            var go = new GameObject("LayerOutOfRangeTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerLayerTool.Execute(new TagLayerLayerParams
                {
                    Action = "set",
                    GameObjectName = "LayerOutOfRangeTest",
                    LayerIndex = 32
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual("OUT_OF_RANGE", result.ErrorCode);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Set_NoLayerSpecified_ReturnsFail()
        {
            var go = new GameObject("LayerNoSpecTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerLayerTool.Execute(new TagLayerLayerParams
                {
                    Action = "set",
                    GameObjectName = "LayerNoSpecTest"
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void InvalidAction_ReturnsFail()
        {
            var result = TagLayerLayerTool.Execute(new TagLayerLayerParams
            {
                Action = "remove"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
