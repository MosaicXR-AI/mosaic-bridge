using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.TagsLayers;

namespace Mosaic.Bridge.Tests.Unit.Tools.TagsLayers
{
    [TestFixture]
    public class TagLayerStaticTests
    {
        // ── Get ──────────────────────────────────────────────────────────────

        [Test]
        public void Get_ReturnsCurrentFlags()
        {
            var go = new GameObject("StaticGetTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                // Default is no flags (Nothing)
                var result = TagLayerStaticTool.Execute(new TagLayerStaticParams
                {
                    Action = "get",
                    GameObjectName = "StaticGetTest"
                });

                Assert.IsTrue(result.Success);
                Assert.AreEqual("StaticGetTest", result.Data.GameObjectName);
                Assert.AreEqual("Nothing", result.Data.Flags);
                Assert.AreEqual(0, result.Data.RawValue);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Get_ByInstanceId_ReturnsFlags()
        {
            var go = new GameObject("StaticGetIdTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerStaticTool.Execute(new TagLayerStaticParams
                {
                    Action = "get",
                    InstanceId = go.GetInstanceID()
                });

                Assert.IsTrue(result.Success);
                Assert.AreEqual("StaticGetIdTest", result.Data.GameObjectName);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── Set ──────────────────────────────────────────────────────────────

        [Test]
        public void Set_SingleFlag_AppliesFlag()
        {
            var go = new GameObject("StaticSetSingleTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerStaticTool.Execute(new TagLayerStaticParams
                {
                    Action = "set",
                    GameObjectName = "StaticSetSingleTest",
                    Flags = "BatchingStatic"
                });

                Assert.IsTrue(result.Success);
                Assert.AreEqual("StaticSetSingleTest", result.Data.GameObjectName);

                var actual = GameObjectUtility.GetStaticEditorFlags(go);
                Assert.IsTrue((actual & StaticEditorFlags.BatchingStatic) != 0,
                    "BatchingStatic flag should be set");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Set_MultipleFlags_AppliesAll()
        {
            var go = new GameObject("StaticSetMultiTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerStaticTool.Execute(new TagLayerStaticParams
                {
                    Action = "set",
                    GameObjectName = "StaticSetMultiTest",
                    Flags = "BatchingStatic, OccludeeStatic"
                });

                Assert.IsTrue(result.Success);

                var actual = GameObjectUtility.GetStaticEditorFlags(go);
                Assert.IsTrue((actual & StaticEditorFlags.BatchingStatic) != 0);
                Assert.IsTrue((actual & StaticEditorFlags.OccludeeStatic) != 0);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Set_Everything_SetsAllFlags()
        {
            var go = new GameObject("StaticSetEverythingTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerStaticTool.Execute(new TagLayerStaticParams
                {
                    Action = "set",
                    GameObjectName = "StaticSetEverythingTest",
                    Flags = "Everything"
                });

                Assert.IsTrue(result.Success);
                Assert.IsTrue(result.Data.RawValue != 0, "Everything should set flags to non-zero");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Set_Nothing_ClearsAllFlags()
        {
            var go = new GameObject("StaticSetNothingTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                // First set some flags
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

                var result = TagLayerStaticTool.Execute(new TagLayerStaticParams
                {
                    Action = "set",
                    GameObjectName = "StaticSetNothingTest",
                    Flags = "Nothing"
                });

                Assert.IsTrue(result.Success);
                Assert.AreEqual("Nothing", result.Data.Flags);
                Assert.AreEqual(0, result.Data.RawValue);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── Error cases ──────────────────────────────────────────────────────

        [Test]
        public void Get_MissingGameObject_ReturnsFail()
        {
            var result = TagLayerStaticTool.Execute(new TagLayerStaticParams
            {
                Action = "get",
                GameObjectName = "NonExistent_StaticTest_12345"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Set_MissingFlags_ReturnsFail()
        {
            var go = new GameObject("StaticMissingFlagsTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerStaticTool.Execute(new TagLayerStaticParams
                {
                    Action = "set",
                    GameObjectName = "StaticMissingFlagsTest"
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
        public void Set_InvalidFlagName_ReturnsFail()
        {
            var go = new GameObject("StaticInvalidFlagTest");
            Undo.RegisterCreatedObjectUndo(go, "test");

            try
            {
                var result = TagLayerStaticTool.Execute(new TagLayerStaticParams
                {
                    Action = "set",
                    GameObjectName = "StaticInvalidFlagTest",
                    Flags = "NotARealFlag"
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
            var result = TagLayerStaticTool.Execute(new TagLayerStaticParams
            {
                Action = "clear"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
