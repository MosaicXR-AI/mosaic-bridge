using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using Mosaic.Bridge.Tools.EditorOps;

namespace Mosaic.Bridge.Tests.Unit.Tools
{
    [TestFixture]
    public class EditorExecuteMenuItemTests
    {
        [Test]
        public void Execute_ValidMenuPath_ReturnsSuccess()
        {
            var p = new EditorExecuteMenuItemParams { MenuPath = "GameObject/Create Empty" };
            var result = EditorExecuteMenuItemTool.Execute(p);

            Assert.IsTrue(result.Success, "Expected menu item execution to succeed");
            Assert.IsTrue(result.Data.Executed);
            Assert.AreEqual("GameObject/Create Empty", result.Data.MenuPath);

            // Cleanup: destroy the newly created GameObject
            var created = GameObject.Find("GameObject");
            if (created != null)
                Undo.DestroyObjectImmediate(created);
        }

        [Test]
        public void Execute_InvalidMenuPath_ReturnsFail()
        {
            // Unity logs an error when ExecuteMenuItem fails — expect it
            LogAssert.Expect(LogType.Error,
                "ExecuteMenuItem failed because there is no menu named 'Nonexistent/Menu/Path'");

            var p = new EditorExecuteMenuItemParams { MenuPath = "Nonexistent/Menu/Path" };
            var result = EditorExecuteMenuItemTool.Execute(p);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("not found", result.Error.ToLowerInvariant());
        }

        [Test]
        public void Execute_EmptyMenuPath_ReturnsFail()
        {
            // Unity logs an error when ExecuteMenuItem is called with empty string
            LogAssert.Expect(LogType.Error,
                "ExecuteMenuItem failed because there is no menu named ''");

            var p = new EditorExecuteMenuItemParams { MenuPath = "" };
            var result = EditorExecuteMenuItemTool.Execute(p);

            Assert.IsFalse(result.Success);
        }
    }
}
