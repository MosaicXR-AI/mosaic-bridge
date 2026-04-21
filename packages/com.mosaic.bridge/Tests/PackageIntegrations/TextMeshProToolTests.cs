#if MOSAIC_HAS_TMP
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.TextMeshPro;

namespace Mosaic.Bridge.Tests.PackageIntegrations
{
    [TestFixture]
    [Category("PackageIntegration")]
    public class TextMeshProToolTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var name in new[] { "TMP_UIText", "TMP_WorldText", "TMP_Props", "Canvas" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Create_UIText_ReturnsSuccess()
        {
            var result = TmpCreateTool.Execute(new TmpCreateParams
            {
                Text = "Hello World", Name = "TMP_UIText", Context = "ui"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("TMP_UIText", result.Data.Name);
        }

        [Test]
        public void Create_WorldText_ReturnsSuccess()
        {
            var result = TmpCreateTool.Execute(new TmpCreateParams
            {
                Text = "3D Text", Name = "TMP_WorldText", Context = "world"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(GameObject.Find("TMP_WorldText"));
        }

        [Test]
        public void Create_InvalidContext_ReturnsFail()
        {
            var result = TmpCreateTool.Execute(new TmpCreateParams
            {
                Text = "Bad", Context = "invalid"
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void SetProperties_Text_UpdatesValue()
        {
            TmpCreateTool.Execute(new TmpCreateParams
            {
                Text = "Original", Name = "TMP_Props", Context = "world"
            });
            var result = TmpSetPropertiesTool.Execute(new TmpSetPropertiesParams
            {
                GameObjectName = "TMP_Props", Text = "Updated"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.AppliedCount > 0);
        }

        [Test]
        public void SetProperties_NonExistentGO_ReturnsFail()
        {
            var result = TmpSetPropertiesTool.Execute(new TmpSetPropertiesParams
            {
                GameObjectName = "NonExistent", Text = "Test"
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Info_AllScene_ReturnsSuccess()
        {
            var result = TmpInfoTool.Execute(new TmpInfoParams());
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.Components);
        }

        [Test]
        public void Info_AfterCreate_FindsComponent()
        {
            TmpCreateTool.Execute(new TmpCreateParams
            {
                Text = "Find Me", Name = "TMP_Props", Context = "world"
            });
            var result = TmpInfoTool.Execute(new TmpInfoParams { GameObjectName = "TMP_Props" });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Components.Length);
        }

        [Test]
        public void SetProperties_FontSize_UpdatesValue()
        {
            TmpCreateTool.Execute(new TmpCreateParams
            {
                Text = "Size Test", Name = "TMP_Props", Context = "world"
            });
            var result = TmpSetPropertiesTool.Execute(new TmpSetPropertiesParams
            {
                GameObjectName = "TMP_Props", FontSize = 72f
            });
            Assert.IsTrue(result.Success, result.Error);
        }
    }
}
#endif
