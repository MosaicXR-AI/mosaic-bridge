using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.TagsLayers;

namespace Mosaic.Bridge.Tests.Unit.Tools.TagsLayers
{
    [TestFixture]
    public class TagLayerTagTests
    {
        // ── List ─────────────────────────────────────────────────────────────

        [Test]
        public void List_ReturnsStandardTags()
        {
            var result = TagLayerTagTool.Execute(new TagLayerTagParams { Action = "list" });

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data.Tags);
            Assert.IsTrue(result.Data.Tags.Length > 0, "Should return at least the built-in tags");

            // Unity ships with these standard tags
            CollectionAssert.Contains(result.Data.Tags, "Untagged");
            CollectionAssert.Contains(result.Data.Tags, "MainCamera");
            CollectionAssert.Contains(result.Data.Tags, "Player");
        }

        // ── Set ──────────────────────────────────────────────────────────────

        [Test]
        public void Set_AssignsTagToGameObject()
        {
            var go = new GameObject("TagSetTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerTagTool.Execute(new TagLayerTagParams
                {
                    Action = "set",
                    GameObjectName = "TagSetTest",
                    TagName = "Player"
                });

                Assert.IsTrue(result.Success);
                Assert.AreEqual("Player", result.Data.AssignedTag);
                Assert.AreEqual("TagSetTest", result.Data.GameObjectName);
                Assert.AreEqual("Player", go.tag);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Set_ByInstanceId_AssignsTag()
        {
            var go = new GameObject("TagSetIdTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerTagTool.Execute(new TagLayerTagParams
                {
                    Action = "set",
                    InstanceId = go.GetInstanceID(),
                    TagName = "MainCamera"
                });

                Assert.IsTrue(result.Success);
                Assert.AreEqual("MainCamera", go.tag);
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
            var result = TagLayerTagTool.Execute(new TagLayerTagParams
            {
                Action = "set",
                GameObjectName = "NonExistent_TagTest_12345",
                TagName = "Player"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Set_MissingTagName_ReturnsFail()
        {
            var result = TagLayerTagTool.Execute(new TagLayerTagParams
            {
                Action = "set",
                GameObjectName = "SomeObject"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Set_NonExistentTag_ReturnsFail()
        {
            var go = new GameObject("TagNonExistentTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerTagTool.Execute(new TagLayerTagParams
                {
                    Action = "set",
                    GameObjectName = "TagNonExistentTest",
                    TagName = "ThisTagDoesNotExist_12345"
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
        public void Add_MissingTagName_ReturnsFail()
        {
            var result = TagLayerTagTool.Execute(new TagLayerTagParams
            {
                Action = "add"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Add_DuplicateTag_ReturnsConflict()
        {
            var result = TagLayerTagTool.Execute(new TagLayerTagParams
            {
                Action = "add",
                TagName = "Untagged" // built-in tag, always exists
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("CONFLICT", result.ErrorCode);
        }

        [Test]
        public void InvalidAction_ReturnsFail()
        {
            var result = TagLayerTagTool.Execute(new TagLayerTagParams
            {
                Action = "delete"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
