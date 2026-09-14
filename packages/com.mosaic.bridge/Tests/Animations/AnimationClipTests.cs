using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Animations
{
    [TestFixture]
    [Category("Animation")]
    public class AnimationClipTests
    {
        private const string TestDir = "Assets/MosaicTestTemp";
        private const string ClipPath = TestDir + "/TestClip.anim";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.CreateFolder("Assets", "MosaicTestTemp");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.DeleteAsset(TestDir);
        }

        [Test]
        public void Create_ReturnsOk_And_AssetExists()
        {
            var result = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action    = "create",
                Path      = ClipPath,
                FrameRate = 30f,
                Loop      = true
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("create", result.Data.Action);
            Assert.AreEqual(ClipPath, result.Data.Path);
            // Unity sets clip.name to the filename when creating an asset
            Assert.AreEqual("TestClip", result.Data.ClipName);
            Assert.AreEqual(30f, result.Data.FrameRate);
            Assert.IsTrue(result.Data.IsLooping);
            Assert.IsTrue(AssetDatabase.AssetPathExists(ClipPath));
        }

        [Test]
        public void Create_MissingPath_ReturnsFail()
        {
            var result = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "create",
                Path   = null
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("Path is required"));
        }

        [Test]
        public void SetCurve_And_VerifyViaInfo()
        {
            // Create clip
            AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "create",
                Path   = ClipPath
            });

            // Set a position.x curve
            var setCurveResult = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action        = "set-curve",
                Path          = ClipPath,
                PropertyPath  = "",
                ComponentType = "Transform",
                PropertyName  = "localPosition.x",
                KeyframeTimes  = new float[] { 0f, 0.5f, 1f },
                KeyframeValues = new float[] { 0f, 5f, 0f }
            });

            Assert.IsTrue(setCurveResult.Success, setCurveResult.Error);
            Assert.AreEqual("set-curve", setCurveResult.Data.Action);

            // Verify via info
            var infoResult = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "info",
                Path   = ClipPath
            });

            Assert.IsTrue(infoResult.Success, infoResult.Error);
            Assert.AreEqual("info", infoResult.Data.Action);
            Assert.IsTrue(infoResult.Data.CurveCount >= 1, "Should have at least 1 curve");
            Assert.AreEqual("localPosition.x", infoResult.Data.Curves[0].PropertyName);
            Assert.AreEqual(3, infoResult.Data.Curves[0].KeyframeCount);
        }

        [Test]
        public void SetCurve_MismatchedArrays_ReturnsFail()
        {
            AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "create",
                Path   = ClipPath
            });

            var result = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action         = "set-curve",
                Path           = ClipPath,
                PropertyPath   = "",
                ComponentType  = "Transform",
                PropertyName   = "localPosition.x",
                KeyframeTimes  = new float[] { 0f, 1f },
                KeyframeValues = new float[] { 0f }
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("same length"));
        }

        // ── set-sprite-curve (D7 — "four clips, 64 keyframes had to be scripted") ────

        private string[] CreateTwoTestSprites()
        {
            var paths = new[] { TestDir + "/Frame0.png", TestDir + "/Frame1.png" };
            foreach (var path in paths)
            {
                var tex = new UnityEngine.Texture2D(4, 4);
                var pixels = new UnityEngine.Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new UnityEngine.Color32(255, 0, 255, 255);
                tex.SetPixels32(pixels);
                tex.Apply();
                System.IO.File.WriteAllBytes(path, UnityEngine.ImageConversion.EncodeToPNG(tex));
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            return paths;
        }

        [Test]
        public void SetSpriteCurve_DefaultsToSpriteRendererMSprite_AndVerifiesViaInfo()
        {
            AnimationClipTool.Execute(new AnimationClipParams { Action = "create", Path = ClipPath });
            var sprites = CreateTwoTestSprites();

            var result = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "set-sprite-curve", Path = ClipPath,
                KeyframeTimes = new float[] { 0f, 0.1f }, Sprites = sprites,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("set-sprite-curve", result.Data.Action);

            var info = AnimationClipTool.Execute(new AnimationClipParams { Action = "info", Path = ClipPath });
            Assert.IsTrue(info.Success, info.Error);
            Assert.AreEqual(1, info.Data.CurveCount, "a PPtr curve must show up in info, not just float curves");
            var curveInfo = info.Data.Curves[0];
            Assert.IsTrue(curveInfo.IsObjectReferenceCurve);
            Assert.AreEqual("m_Sprite", curveInfo.PropertyName);
            Assert.AreEqual("SpriteRenderer", curveInfo.Type);
            Assert.AreEqual(2, curveInfo.KeyframeCount);
        }

        [Test]
        public void SetSpriteCurve_OverridingComponentAndProperty_DrivesImageMSprite()
        {
            AnimationClipTool.Execute(new AnimationClipParams { Action = "create", Path = ClipPath });
            var sprites = CreateTwoTestSprites();

            var result = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "set-sprite-curve", Path = ClipPath,
                ComponentType = "UnityEngine.UI.Image", PropertyName = "m_Sprite",
                KeyframeTimes = new float[] { 0f, 0.2f }, Sprites = sprites,
            });

            Assert.IsTrue(result.Success, result.Error);
            var info = AnimationClipTool.Execute(new AnimationClipParams { Action = "info", Path = ClipPath });
            Assert.AreEqual("Image", info.Data.Curves[0].Type);
        }

        [Test]
        public void SetSpriteCurve_ResolvesRealSpriteValuesOnTheCurve()
        {
            AnimationClipTool.Execute(new AnimationClipParams { Action = "create", Path = ClipPath });
            var sprites = CreateTwoTestSprites();

            var result = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "set-sprite-curve", Path = ClipPath,
                KeyframeTimes = new float[] { 0f, 0.1f }, Sprites = sprites,
            });
            Assert.IsTrue(result.Success, result.Error);

            var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>(ClipPath);
            var binding = UnityEditor.EditorCurveBinding.PPtrCurve("", typeof(UnityEngine.SpriteRenderer), "m_Sprite");
            var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            Assert.IsNotNull(curve);
            Assert.AreEqual(2, curve.Length);
            Assert.IsInstanceOf<UnityEngine.Sprite>(curve[0].value);
            Assert.AreEqual("Frame1", curve[1].value.name);
        }

        [Test]
        public void SetSpriteCurve_MismatchedArrayLengths_Fails()
        {
            AnimationClipTool.Execute(new AnimationClipParams { Action = "create", Path = ClipPath });
            var sprites = CreateTwoTestSprites();

            var result = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "set-sprite-curve", Path = ClipPath,
                KeyframeTimes = new float[] { 0f }, Sprites = sprites,
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void SetSpriteCurve_MissingSprite_Fails()
        {
            AnimationClipTool.Execute(new AnimationClipParams { Action = "create", Path = ClipPath });

            var result = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "set-sprite-curve", Path = ClipPath,
                KeyframeTimes = new float[] { 0f }, Sprites = new[] { "Assets/DoesNotExist_555.png" },
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void AddEvent_And_VerifyViaInfo()
        {
            // Create clip
            AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "create",
                Path   = ClipPath
            });

            // Need to add a curve so the clip has length > 0
            AnimationClipTool.Execute(new AnimationClipParams
            {
                Action         = "set-curve",
                Path           = ClipPath,
                PropertyPath   = "",
                ComponentType  = "Transform",
                PropertyName   = "localPosition.x",
                KeyframeTimes  = new float[] { 0f, 1f },
                KeyframeValues = new float[] { 0f, 1f }
            });

            // Add event
            var addResult = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action        = "add-event",
                Path          = ClipPath,
                EventTime     = 0.5f,
                EventFunction = "OnFootstep",
                EventStringParam = "left"
            });

            Assert.IsTrue(addResult.Success, addResult.Error);
            Assert.AreEqual("add-event", addResult.Data.Action);
            Assert.AreEqual(1, addResult.Data.EventCount);

            // Verify via info
            var infoResult = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "info",
                Path   = ClipPath
            });

            Assert.IsTrue(infoResult.Success, infoResult.Error);
            Assert.AreEqual(1, infoResult.Data.EventCount);
            Assert.AreEqual("OnFootstep", infoResult.Data.Events[0].FunctionName);
            Assert.AreEqual(0.5f, infoResult.Data.Events[0].Time, 0.001f);
            Assert.AreEqual("left", infoResult.Data.Events[0].StringParameter);
        }

        [Test]
        public void InvalidAction_ReturnsFail_WithValidActions()
        {
            var result = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "bad-action"
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("create"), "Error should list valid actions");
            Assert.IsTrue(result.Error.Contains("set-curve"), "Error should list valid actions");
        }

        [Test]
        public void Info_NotFound_ReturnsFail()
        {
            var result = AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "info",
                Path   = "Assets/DoesNotExist.anim"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}
