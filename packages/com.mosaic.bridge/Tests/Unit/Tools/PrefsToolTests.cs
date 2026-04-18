using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Tools.Prefs;

namespace Mosaic.Bridge.Tests.Unit.Tools
{
    [TestFixture]
    [Category("Unit")]
    public class PrefsToolTests
    {
        private const string TestKey = "MosaicBridge_TestPref_Unit";

        [TearDown]
        public void TearDown()
        {
            if (EditorPrefs.HasKey(TestKey))
                EditorPrefs.DeleteKey(TestKey);
            if (PlayerPrefs.HasKey(TestKey))
            {
                PlayerPrefs.DeleteKey(TestKey);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void EditorPrefs_SetGetDelete_RoundTrip()
        {
            // Set
            var setResult = PrefsEditorTool.EditorPrefsAction(new PrefsEditorParams
            {
                Key = TestKey, Value = "hello_mosaic", Action = "set"
            });
            Assert.IsTrue(setResult.Success);
            Assert.AreEqual("hello_mosaic", setResult.Data.Value);

            // Get
            var getResult = PrefsEditorTool.EditorPrefsAction(new PrefsEditorParams
            {
                Key = TestKey, Action = "get"
            });
            Assert.IsTrue(getResult.Success);
            Assert.IsTrue(getResult.Data.Existed);
            Assert.AreEqual("hello_mosaic", getResult.Data.Value);

            // Delete
            var delResult = PrefsEditorTool.EditorPrefsAction(new PrefsEditorParams
            {
                Key = TestKey, Action = "delete"
            });
            Assert.IsTrue(delResult.Success);
            Assert.IsTrue(delResult.Data.Existed);

            // Verify deleted
            var verifyResult = PrefsEditorTool.EditorPrefsAction(new PrefsEditorParams
            {
                Key = TestKey, Action = "get"
            });
            Assert.IsTrue(verifyResult.Success);
            Assert.IsFalse(verifyResult.Data.Existed);
        }

        [Test]
        public void PlayerPrefs_SetGetDelete_RoundTrip()
        {
            // Set
            var setResult = PrefsPlayerTool.PlayerPrefsAction(new PrefsPlayerParams
            {
                Key = TestKey, Value = "player_val", Action = "set"
            });
            Assert.IsTrue(setResult.Success);

            // Get
            var getResult = PrefsPlayerTool.PlayerPrefsAction(new PrefsPlayerParams
            {
                Key = TestKey, Action = "get"
            });
            Assert.IsTrue(getResult.Success);
            Assert.AreEqual("player_val", getResult.Data.Value);

            // Delete
            var delResult = PrefsPlayerTool.PlayerPrefsAction(new PrefsPlayerParams
            {
                Key = TestKey, Action = "delete"
            });
            Assert.IsTrue(delResult.Success);
        }
    }
}
