using Mosaic.Bridge.Tools.Assets;
using Mosaic.Bridge.Tools.Prefabs;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.Tests.Unit.Tools.Prefabs
{
    /// <summary>
    /// L8: prefab/create and asset/create_prefab (the same logic, duplicated in two files) both
    /// used PrefabUtility.SaveAsPrefabAsset, which leaves the scene GameObject as a plain,
    /// unlinked object after the prefab asset is created — the Editor's own drag-into-Project
    /// path uses SaveAsPrefabAssetAndConnect, which turns the scene object into an instance of
    /// the prefab it just created. Both tools now do the same.
    /// </summary>
    [TestFixture]
    public class PrefabCreateConnectTests
    {
        private const string PrefabPathA = "Assets/MosaicPrefabCreateConnectA.prefab";
        private const string PrefabPathB = "Assets/MosaicPrefabCreateConnectB.prefab";
        private GameObject _goA;
        private GameObject _goB;

        [TearDown]
        public void TearDown()
        {
            if (_goA != null) Object.DestroyImmediate(_goA);
            if (_goB != null) Object.DestroyImmediate(_goB);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPathA) != null)
                AssetDatabase.DeleteAsset(PrefabPathA);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPathB) != null)
                AssetDatabase.DeleteAsset(PrefabPathB);
        }

        [Test]
        public void PrefabCreateTool_ConnectsTheSceneObjectToTheNewPrefab()
        {
            _goA = new GameObject("MosaicPrefabCreateConnectA");
            Assert.IsFalse(PrefabUtility.IsPartOfAnyPrefab(_goA), "test setup sanity check");

            var result = PrefabCreateTool.Execute(new PrefabCreateParams
            {
                GameObjectName = "MosaicPrefabCreateConnectA", PrefabPath = PrefabPathA
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(_goA),
                "the scene object must become a connected prefab instance (L8), not a plain GameObject");
            Assert.AreEqual(PrefabPathA,
                AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(_goA)));
        }

        [Test]
        public void AssetCreatePrefabTool_ConnectsTheSceneObjectToTheNewPrefab()
        {
            _goB = new GameObject("MosaicPrefabCreateConnectB");
            Assert.IsFalse(PrefabUtility.IsPartOfAnyPrefab(_goB), "test setup sanity check");

            var result = AssetCreatePrefabTool.Execute(new AssetCreatePrefabParams
            {
                GameObjectName = "MosaicPrefabCreateConnectB", PrefabPath = PrefabPathB
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(_goB),
                "the scene object must become a connected prefab instance (L8), not a plain GameObject");
            Assert.AreEqual(PrefabPathB,
                AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(_goB)));
        }
    }
}
