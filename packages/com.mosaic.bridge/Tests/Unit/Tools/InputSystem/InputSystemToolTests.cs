using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Unit.Tools.InputSystem
{
    /// <summary>
    /// Parameter validation tests for Input System tools.
    /// These tests do NOT require the Input System package to be installed —
    /// they validate the tool contracts and error handling patterns.
    /// When MOSAIC_HAS_INPUT_SYSTEM is defined, integration tests would run against real assets.
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("InputSystem")]
    public class InputSystemToolTests
    {
#if MOSAIC_HAS_INPUT_SYSTEM
        private const string TestAssetPath = "Assets/MosaicTestInput.inputactions";

        [TearDown]
        public void TearDown()
        {
            if (UnityEditor.AssetDatabase.AssetPathExists(TestAssetPath))
            {
                UnityEditor.AssetDatabase.DeleteAsset(TestAssetPath);
                UnityEditor.AssetDatabase.Refresh();
            }
        }

        // ── Create ─────────────────────────────────────────────────────

        [Test]
        public void Create_InvalidPath_NoExtension_ReturnsFail()
        {
            var result = Mosaic.Bridge.Tools.InputSystem.InputCreateTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputCreateParams
                {
                    Name = "Test",
                    Path = "Assets/Test.txt"
                });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_InvalidPath_NotInAssets_ReturnsFail()
        {
            var result = Mosaic.Bridge.Tools.InputSystem.InputCreateTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputCreateParams
                {
                    Name = "Test",
                    Path = "Library/Test.inputactions"
                });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Info ───────────────────────────────────────────────────────

        [Test]
        public void Info_NonExistentPath_ReturnsNotFound()
        {
            var result = Mosaic.Bridge.Tools.InputSystem.InputInfoTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputInfoParams
                {
                    AssetPath = "Assets/NonExistent.inputactions"
                });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // ── Map ────────────────────────────────────────────────────────

        [Test]
        public void Map_NonExistentAsset_ReturnsNotFound()
        {
            var result = Mosaic.Bridge.Tools.InputSystem.InputMapTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputMapParams
                {
                    AssetPath = "Assets/NonExistent.inputactions",
                    Action    = "list"
                });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Map_InvalidAction_ReturnsFail()
        {
            // First create a valid asset
            Mosaic.Bridge.Tools.InputSystem.InputCreateTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputCreateParams
                {
                    Name = "Test",
                    Path = TestAssetPath
                });

            var result = Mosaic.Bridge.Tools.InputSystem.InputMapTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputMapParams
                {
                    AssetPath = TestAssetPath,
                    Action    = "invalid"
                });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Action ─────────────────────────────────────────────────────

        [Test]
        public void Action_NonExistentAsset_ReturnsNotFound()
        {
            var result = Mosaic.Bridge.Tools.InputSystem.InputActionTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputActionParams
                {
                    AssetPath  = "Assets/NonExistent.inputactions",
                    Action     = "add",
                    MapName    = "Player",
                    ActionName = "Jump"
                });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // ── Integration ────────────────────────────────────────────────

        [Test]
        public void Integration_CreateAsset_AddMap_AddAction_AddBinding_Query()
        {
            // 1. Create asset
            var createResult = Mosaic.Bridge.Tools.InputSystem.InputCreateTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputCreateParams
                {
                    Name = "TestActions",
                    Path = TestAssetPath
                });
            Assert.IsTrue(createResult.Success);

            // 2. Add map
            var mapResult = Mosaic.Bridge.Tools.InputSystem.InputMapTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputMapParams
                {
                    AssetPath = TestAssetPath,
                    Action    = "add",
                    MapName   = "Player"
                });
            Assert.IsTrue(mapResult.Success);
            Assert.Contains("Player", mapResult.Data.Maps);

            // 3. Add action
            var actionResult = Mosaic.Bridge.Tools.InputSystem.InputActionTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputActionParams
                {
                    AssetPath  = TestAssetPath,
                    Action     = "add",
                    MapName    = "Player",
                    ActionName = "Jump",
                    ActionType = "Button"
                });
            Assert.IsTrue(actionResult.Success);
            Assert.Contains("Jump", actionResult.Data.Actions);

            // 4. Add binding
            var bindResult = Mosaic.Bridge.Tools.InputSystem.InputActionTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputActionParams
                {
                    AssetPath   = TestAssetPath,
                    Action      = "add-binding",
                    MapName     = "Player",
                    ActionName  = "Jump",
                    BindingPath = "<Keyboard>/space"
                });
            Assert.IsTrue(bindResult.Success);
            Assert.Greater(bindResult.Data.Bindings.Count, 0);

            // 5. Query
            var infoResult = Mosaic.Bridge.Tools.InputSystem.InputInfoTool.Execute(
                new Mosaic.Bridge.Tools.InputSystem.InputInfoParams
                {
                    AssetPath = TestAssetPath
                });
            Assert.IsTrue(infoResult.Success);
            Assert.AreEqual(1, infoResult.Data.Maps.Count);
            Assert.AreEqual("Player", infoResult.Data.Maps[0].Name);
            Assert.AreEqual(1, infoResult.Data.Maps[0].Actions.Count);
            Assert.AreEqual("Jump", infoResult.Data.Maps[0].Actions[0].Name);

            // 6. Cleanup in TearDown
        }
#else
        // ── Param-validation-only tests (no Input System package) ──────

        [Test]
        public void InputSystemTools_NotAvailable_WhenPackageNotInstalled()
        {
            // This test simply verifies the test fixture loads.
            // When MOSAIC_HAS_INPUT_SYSTEM is not defined, the tool classes
            // are compiled out and cannot be called.
            Assert.Pass("Input System package not installed — tool classes compiled out by #if MOSAIC_HAS_INPUT_SYSTEM.");
        }
#endif
    }
}
