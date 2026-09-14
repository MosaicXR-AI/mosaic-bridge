using Mosaic.Bridge.Tools.GameObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.Tests.GameObjects
{
    /// <summary>
    /// L7: gameobject/duplicate used Object.Instantiate, so duplicating a prefab instance
    /// produced a plain, disconnected copy — Apply/Revert on it did nothing, and it silently
    /// diverged from the prefab the moment the source prefab asset changed. It now goes through
    /// Unsupported.DuplicateGameObjectsUsingPasteboard, the exact mechanism behind the Editor's
    /// own Ctrl+D, which keeps a prefab instance connected.
    /// </summary>
    [TestFixture]
    public class GameObjectDuplicateToolTests
    {
        private const string PrefabPath = "Assets/MosaicDuplicateProbe.prefab";
        private GameObject _plain;
        private GameObject _instance;
        private GameObject _parent;

        [TearDown]
        public void TearDown()
        {
            foreach (var name in new[]
                     {
                         "MosaicDuplicateProbe", "MosaicDuplicateProbe (1)", "MosaicDuplicateRenamed",
                         "MosaicDuplicateParent", "MosaicDuplicateProbePlain", "MosaicDuplicateProbePlain (1)"
                     })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }
            if (_plain != null) Object.DestroyImmediate(_plain);
            if (_instance != null) Object.DestroyImmediate(_instance);
            if (_parent != null) Object.DestroyImmediate(_parent);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                AssetDatabase.DeleteAsset(PrefabPath);
        }

        [Test]
        public void DuplicatingAPrefabInstance_KeepsThePrefabConnection()
        {
            var seed = new GameObject("MosaicDuplicateProbeSeed");
            GameObject prefabAsset;
            try { prefabAsset = PrefabUtility.SaveAsPrefabAsset(seed, PrefabPath); }
            finally { Object.DestroyImmediate(seed); }
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            _instance.name = "MosaicDuplicateProbe";
            Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(_instance), "test setup sanity check");

            var result = GameObjectDuplicateTool.Duplicate(new GameObjectDuplicateParams
            {
                Name = "MosaicDuplicateProbe"
            });

            Assert.IsTrue(result.Success, result.Error);
            var dupe = GameObject.Find(result.Data.Name);
            Assert.IsNotNull(dupe);
            Assert.AreNotSame(_instance, dupe, "must be a new object, not the source");
            Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(dupe),
                "the duplicate must still be a connected prefab instance (L7)");
            Assert.AreEqual(prefabAsset, PrefabUtility.GetCorrespondingObjectFromSource(dupe));
        }

        [Test]
        public void DuplicatingAPlainGameObject_StillWorks()
        {
            _plain = new GameObject("MosaicDuplicateProbePlain");

            var result = GameObjectDuplicateTool.Duplicate(new GameObjectDuplicateParams
            {
                Name = "MosaicDuplicateProbePlain"
            });

            Assert.IsTrue(result.Success, result.Error);
            var dupe = GameObject.Find(result.Data.Name);
            Assert.IsNotNull(dupe);
            Assert.AreNotSame(_plain, dupe);
        }

        [Test]
        public void ExplicitNewName_Applied()
        {
            _plain = new GameObject("MosaicDuplicateProbePlain");

            var result = GameObjectDuplicateTool.Duplicate(new GameObjectDuplicateParams
            {
                Name = "MosaicDuplicateProbePlain", NewName = "MosaicDuplicateRenamed"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("MosaicDuplicateRenamed", result.Data.Name);
            Assert.IsNotNull(GameObject.Find("MosaicDuplicateRenamed"));
        }

        [Test]
        public void EmptyParent_Unparents()
        {
            _parent = new GameObject("MosaicDuplicateParent");
            _plain = new GameObject("MosaicDuplicateProbePlain");
            _plain.transform.SetParent(_parent.transform);

            var result = GameObjectDuplicateTool.Duplicate(new GameObjectDuplicateParams
            {
                Name = "MosaicDuplicateProbePlain", Parent = ""
            });

            Assert.IsTrue(result.Success, result.Error);
            var dupe = GameObject.Find(result.Data.Name);
            Assert.IsNull(dupe.transform.parent);
        }

        [Test]
        public void UnknownParent_FailsAndUndoesTheDuplicate()
        {
            _plain = new GameObject("MosaicDuplicateProbePlain");

            var result = GameObjectDuplicateTool.Duplicate(new GameObjectDuplicateParams
            {
                Name = "MosaicDuplicateProbePlain", Parent = "MosaicNoSuchParent_zzz"
            });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("not found", result.Error);
        }

        [Test]
        public void MissingSource_Fails()
        {
            var result = GameObjectDuplicateTool.Duplicate(new GameObjectDuplicateParams
            {
                Name = "MosaicNoSuchObject_zzz"
            });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("not found", result.Error);
        }
    }
}
