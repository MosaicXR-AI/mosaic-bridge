using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Core.Assets;

namespace Mosaic.Bridge.Tests.Unit.Core.Assets
{
    // O4 §3.1: resolves TargetObjectPath against the field's own type instead of loading whatever
    // is at the path and assigning it regardless. SplitSubAsset is pure string logic and is fully
    // covered without touching AssetDatabase; TryResolveAsset's asset-loading branches need a real
    // project and are exercised through ComponentSetReferenceTool/ScriptableObjectSetFieldTool's
    // own integration tests instead.
    [TestFixture]
    public class ObjectReferenceResolverTests
    {
        [Test]
        public void SplitSubAsset_NoHash_SubAssetNameIsNull()
        {
            ObjectReferenceResolver.SplitSubAsset("Assets/sheet.png", out var assetPath, out var subAssetName);

            Assert.AreEqual("Assets/sheet.png", assetPath);
            Assert.IsNull(subAssetName);
        }

        [Test]
        public void SplitSubAsset_WithHash_SplitsPathAndSubAssetName()
        {
            ObjectReferenceResolver.SplitSubAsset("Assets/sheet.png#Run_03", out var assetPath, out var subAssetName);

            Assert.AreEqual("Assets/sheet.png", assetPath);
            Assert.AreEqual("Run_03", subAssetName);
        }

        [Test]
        public void SplitSubAsset_NullInput_AssetPathIsNullSubAssetNameIsNull()
        {
            ObjectReferenceResolver.SplitSubAsset(null, out var assetPath, out var subAssetName);

            Assert.IsNull(assetPath);
            Assert.IsNull(subAssetName);
        }

        [Test]
        public void ResolveFieldType_NonObjectReferenceProperty_ReturnsNull()
        {
            Assert.IsNull(ObjectReferenceResolver.ResolveFieldType(null, _ => typeof(GameObject)));
        }

        [Test]
        public void TryResolveAsset_EmptyPath_FailsWithError()
        {
            var ok = ObjectReferenceResolver.TryResolveAsset("", typeof(GameObject), out var resolved, out var error);

            Assert.IsFalse(ok);
            Assert.IsNull(resolved);
            StringAssert.Contains("empty", error);
        }
    }
}
