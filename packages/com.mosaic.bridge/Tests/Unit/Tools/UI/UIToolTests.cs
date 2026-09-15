using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEditor;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tests.Unit.Tools.UI
{
    [TestFixture]
    public class UIToolTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Tracks all GameObjects created during a test for cleanup.</summary>
        private readonly System.Collections.Generic.List<GameObject> _created =
            new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            _created.Clear();

            // Clean up any EventSystems we may have created
            var eventSystems = UnityIds.FindAll<EventSystem>();
            foreach (var es in eventSystems)
                Object.DestroyImmediate(es.gameObject);
        }

        private void Track(int instanceId)
        {
            var go = UnityIds.Resolve(instanceId) as GameObject;
            if (go != null)
                _created.Add(go);
        }

        // ── ui/create_canvas ─────────────────────────────────────────────────

        [Test]
        public void CreateCanvas_DefaultParams_ReturnsCanvas()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("Canvas", result.Data.Name);
            Assert.AreEqual("ScreenSpaceOverlay", result.Data.RenderMode);
            Track(result.Data.InstanceId);
        }

        [Test]
        public void CreateCanvas_WithName_UsesProvidedName()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "MyUI" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("MyUI", result.Data.Name);
            Track(result.Data.InstanceId);
        }

        [Test]
        public void CreateCanvas_WorldSpace_SetsRenderMode()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { RenderMode = "WorldSpace" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("WorldSpace", result.Data.RenderMode);
            Track(result.Data.InstanceId);
        }

        [Test]
        public void CreateCanvas_InvalidRenderMode_Fails()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { RenderMode = "BadMode" });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void CreateCanvas_CreatesEventSystem_WhenNoneExists()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.EventSystemCreated);
            Track(result.Data.InstanceId);
        }

        [Test]
        public void CreateCanvas_HasCanvasScalerAndRaycaster()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams());

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);

            var go = UnityIds.Resolve(result.Data.InstanceId) as GameObject;
            Assert.IsNotNull(go);
            Assert.IsNotNull(go.GetComponent<Canvas>());
            Assert.IsNotNull(go.GetComponent<CanvasScaler>());
            Assert.IsNotNull(go.GetComponent<GraphicRaycaster>());
        }

        // O4 L6: a hardcoded StandaloneInputModule silently does nothing in an Input System
        // project. InputModuleComponentFactory.AddInputModule (the "auto" default) is Unity's own
        // public factory — the same one its Component menu uses — and picks correctly based on
        // what's actually installed.

        [Test]
        public void CreateCanvas_DefaultInputModule_UsesFactoryNotHardcodedStandalone()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams());

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);
            Assert.IsTrue(result.Data.EventSystemCreated);
            Assert.IsNotNull(result.Data.InputModuleType,
                "InputModuleComponentFactory.AddInputModule must report back what it actually added");
            var eventSystemGo = UnityIds.FindAll<EventSystem>().First().gameObject;
            Assert.IsNotNull(eventSystemGo.GetComponent<BaseInputModule>(),
                "the factory must have added SOME BaseInputModule, whichever this environment resolves to");
        }

        [Test]
        public void CreateCanvas_InputModuleStandalone_ForcesStandaloneInputModule()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { InputModule = "standalone" });

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);
            Assert.AreEqual(nameof(StandaloneInputModule), result.Data.InputModuleType);
            var eventSystemGo = UnityIds.FindAll<EventSystem>().First().gameObject;
            Assert.IsNotNull(eventSystemGo.GetComponent<StandaloneInputModule>());
        }

#if !MOSAIC_HAS_INPUT_SYSTEM
        [Test]
        public void CreateCanvas_InputModuleInputSystem_FailsCleanlyWhenPackageAbsent()
        {
            // This test environment has no com.unity.inputsystem package — exercises the "fails
            // with a clear message" path, the one InputModule=inputsystem exists to guarantee
            // instead of a stack trace when forced but the package isn't there.
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { InputModule = "inputsystem" });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("com.unity.inputsystem", result.Error);
        }
#else
        [Test]
        public void CreateCanvas_InputModuleInputSystem_AddsInputSystemUIInputModule()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { InputModule = "inputsystem" });

            Assert.IsTrue(result.Success, result.Error);
            var eventSystemGo = UnityIds.FindAll<EventSystem>().First().gameObject;
            Assert.IsNotNull(eventSystemGo.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>());
        }
#endif

        [Test]
        public void CreateCanvas_InvalidInputModule_Fails()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { InputModule = "NotAModule" });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void CreateCanvas_ExistingEventSystem_InputModuleParamIsIgnored()
        {
            var existingEventSystemGo = new GameObject("PreExistingEventSystem");
            existingEventSystemGo.AddComponent<EventSystem>();
            _created.Add(existingEventSystemGo);

            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { InputModule = "inputsystem" });

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);
            Assert.IsFalse(result.Data.EventSystemCreated);
        }

        // ── ui/create_canvas: CanvasScaler / Canvas extension params ──────────

        [Test]
        public void CreateCanvas_ScaleWithScreenSize_SetsScalerFields()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams
                {
                    ScaleMode = "ScaleWithScreenSize",
                    ReferenceResolution = new[] { 1920f, 1080f },
                    MatchWidthOrHeight = 0.5f,
                });

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);
            var scaler = (UnityIds.Resolve(result.Data.InstanceId) as GameObject).GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
            Assert.AreEqual(0.5f, scaler.matchWidthOrHeight, 0.001f);
        }

        [Test]
        public void CreateCanvas_SortingOrderAndPixelPerfect_AreApplied()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { SortingOrder = 7, PixelPerfect = true });

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);
            var canvas = (UnityIds.Resolve(result.Data.InstanceId) as GameObject).GetComponent<Canvas>();
            Assert.AreEqual(7, canvas.sortingOrder);
            Assert.IsTrue(canvas.pixelPerfect);
        }

        [Test]
        public void CreateCanvas_WorldSpaceWithWorldSize_SetsRectTransformSizeDelta()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams
                {
                    RenderMode = "WorldSpace", WorldSize = new[] { 4f, 3f },
                });

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);
            var rect = (UnityIds.Resolve(result.Data.InstanceId) as GameObject).GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(4f, 3f), rect.sizeDelta);
        }

        [Test]
        public void CreateCanvas_ExplicitWorldCamera_OverridesCameraMain()
        {
            var camGo = new GameObject("MyUICamera");
            camGo.AddComponent<UnityEngine.Camera>();
            _created.Add(camGo);

            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { RenderMode = "Camera", WorldCamera = "MyUICamera" });

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);
            var canvas = (UnityIds.Resolve(result.Data.InstanceId) as GameObject).GetComponent<Canvas>();
            Assert.AreEqual(camGo.GetComponent<UnityEngine.Camera>(), canvas.worldCamera);
        }

        [Test]
        public void CreateCanvas_Parent_ReparentsUnderGivenGameObject()
        {
            var parentGo = new GameObject("UIParent");
            _created.Add(parentGo);

            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Parent = "UIParent" });

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);
            var canvasGo = UnityIds.Resolve(result.Data.InstanceId) as GameObject;
            Assert.AreSame(parentGo.transform, canvasGo.transform.parent);
        }

        [Test]
        public void CreateCanvas_UnknownParent_Fails()
        {
            var result = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Parent = "DoesNotExist_12345" });

            Assert.IsFalse(result.Success);
        }

        // ── ui/add_element ───────────────────────────────────────────────────

        [Test]
        public void AddElement_Button_CreatesButtonWithTextChild()
        {
            // First create a canvas
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_AddBtn" });
            Assert.IsTrue(canvasResult.Success, canvasResult.Error);
            Track(canvasResult.Data.InstanceId);

            var result = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId,
                    ElementType = "button"
                });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("button", result.Data.ElementType);
            Assert.IsTrue(result.Data.Components.Contains("Button"));
            Track(result.Data.InstanceId);

            // Verify text child exists
            var buttonGo = UnityIds.Resolve(result.Data.InstanceId) as GameObject;
            Assert.IsNotNull(buttonGo);
            Assert.IsTrue(buttonGo.transform.childCount > 0, "Button should have a Text child");
        }

        // O4 L5: the #if UNITY_2023_1_OR_NEWER && HAS_TMPRO branches never compiled at all
        // (HAS_TMPRO was defined nowhere) — every text child was legacy Text regardless of intent,
        // and the Dropdown/InputField TMP branches were literally left as "would require
        // TMP_Dropdown"/"would require TMP_InputField" comments. This test environment has
        // com.unity.ugui 2.0.0, so MOSAIC_HAS_TMP is genuinely active here — these assert the real
        // TMP path, not just that the fallback still compiles.
#if UNITY_2023_1_OR_NEWER && MOSAIC_HAS_TMP

        [Test]
        public void AddElement_Button_UsesTextMeshProChild_WhenTmpAvailable()
        {
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_TmpButton" });
            Track(canvasResult.Data.InstanceId);

            var result = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId, ElementType = "button"
                });

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);
            var buttonGo = UnityIds.Resolve(result.Data.InstanceId) as GameObject;
            var tmpChild = buttonGo.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            Assert.IsNotNull(tmpChild, "MOSAIC_HAS_TMP is active in this environment — the button's " +
                "label must be TextMeshProUGUI, not legacy Text");
            Assert.AreEqual("Button", tmpChild.text);
        }

        [Test]
        public void AddElement_Dropdown_HasAWorkingTemplateChild()
        {
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_Dropdown" });
            Track(canvasResult.Data.InstanceId);

            var result = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId, ElementType = "dropdown"
                });

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);
            Assert.IsTrue(result.Data.Components.Contains("TMP_Dropdown"),
                "MOSAIC_HAS_TMP is active — this must be a real TMP_Dropdown, not legacy Dropdown");

            var dropdownGo = UnityIds.Resolve(result.Data.InstanceId) as GameObject;
            var template = dropdownGo.transform.Find("Template");
            Assert.IsNotNull(template,
                "the old hand-rolled dropdown had no Template child at all and could never actually open");
            Assert.IsNotNull(template.GetComponentInChildren<ScrollRect>(),
                "a working dropdown template needs a ScrollRect over its item list");
        }

        [Test]
        public void AddElement_InputField_WiresRealTmpTextComponentAndPlaceholder()
        {
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_InputField" });
            Track(canvasResult.Data.InstanceId);

            var result = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId, ElementType = "input-field"
                });

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);
            Assert.IsTrue(result.Data.Components.Contains("TMP_InputField"),
                "MOSAIC_HAS_TMP is active — this must be a real TMP_InputField, not legacy InputField");

            var inputGo = UnityIds.Resolve(result.Data.InstanceId) as GameObject;
            var tmpInput = inputGo.GetComponent<TMPro.TMP_InputField>();
            Assert.IsNotNull(tmpInput.textComponent,
                "the old TMP branch never wired this at all (left as a comment) — a null textComponent means typed text is never shown");
            Assert.IsNotNull(tmpInput.placeholder);
        }
#endif

        [Test]
        public void AddElement_Image_CreatesImage()
        {
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_AddImg" });
            Assert.IsTrue(canvasResult.Success, canvasResult.Error);
            Track(canvasResult.Data.InstanceId);

            var result = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId,
                    ElementType = "image",
                    Name = "MyImage"
                });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("MyImage", result.Data.Name);
            Track(result.Data.InstanceId);
        }

        [Test]
        public void AddElement_NoParent_Fails()
        {
            var result = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams { ElementType = "button" });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void AddElement_InvalidType_Fails()
        {
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_BadType" });
            Assert.IsTrue(canvasResult.Success, canvasResult.Error);
            Track(canvasResult.Data.InstanceId);

            var result = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId,
                    ElementType = "nonexistent"
                });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void AddElement_WithCustomPositionAndSize_AppliesValues()
        {
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_PosSize" });
            Assert.IsTrue(canvasResult.Success, canvasResult.Error);
            Track(canvasResult.Data.InstanceId);

            var result = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId,
                    ElementType = "text",
                    AnchoredPosition = new float[] { 50, 100 },
                    SizeDelta = new float[] { 200, 50 }
                });

            Assert.IsTrue(result.Success, result.Error);
            Track(result.Data.InstanceId);

            var go = UnityIds.Resolve(result.Data.InstanceId) as GameObject;
            var rect = go.GetComponent<RectTransform>();
            Assert.AreEqual(50f, rect.anchoredPosition.x, 0.01f);
            Assert.AreEqual(100f, rect.anchoredPosition.y, 0.01f);
            Assert.AreEqual(200f, rect.sizeDelta.x, 0.01f);
            Assert.AreEqual(50f, rect.sizeDelta.y, 0.01f);
        }

        // ── ui/set_rect_transform ────────────────────────────────────────────

        [Test]
        public void SetRectTransform_SetsAnchorsAndPivot()
        {
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_Rect" });
            Assert.IsTrue(canvasResult.Success, canvasResult.Error);
            Track(canvasResult.Data.InstanceId);

            var addResult = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId,
                    ElementType = "image",
                    Name = "RectTestImage"
                });
            Assert.IsTrue(addResult.Success, addResult.Error);
            Track(addResult.Data.InstanceId);

            var result = Mosaic.Bridge.Tools.UI.UISetRectTransformTool.Execute(
                new Mosaic.Bridge.Tools.UI.UISetRectTransformParams
                {
                    InstanceId = addResult.Data.InstanceId,
                    AnchorMin = new float[] { 0.1f, 0.2f },
                    AnchorMax = new float[] { 0.9f, 0.8f },
                    Pivot = new float[] { 0.5f, 0.5f },
                    SizeDelta = new float[] { 100, 50 },
                    AnchoredPosition = new float[] { 10, 20 }
                });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0.1f, result.Data.AnchorMin[0], 0.01f);
            Assert.AreEqual(0.2f, result.Data.AnchorMin[1], 0.01f);
            Assert.AreEqual(0.9f, result.Data.AnchorMax[0], 0.01f);
            Assert.AreEqual(0.8f, result.Data.AnchorMax[1], 0.01f);
            Assert.AreEqual(10f, result.Data.AnchoredPosition[0], 0.01f);
            Assert.AreEqual(20f, result.Data.AnchoredPosition[1], 0.01f);
        }

        [Test]
        public void SetRectTransform_NoTarget_Fails()
        {
            var result = Mosaic.Bridge.Tools.UI.UISetRectTransformTool.Execute(
                new Mosaic.Bridge.Tools.UI.UISetRectTransformParams());

            Assert.IsFalse(result.Success);
        }

        // ── ui/set_properties ────────────────────────────────────────────────

        [Test]
        public void SetProperties_TextElement_SetsTextAndFontSize()
        {
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_Props" });
            Assert.IsTrue(canvasResult.Success, canvasResult.Error);
            Track(canvasResult.Data.InstanceId);

            var addResult = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId,
                    ElementType = "text",
                    Name = "PropsTestText"
                });
            Assert.IsTrue(addResult.Success, addResult.Error);
            Track(addResult.Data.InstanceId);

            var result = Mosaic.Bridge.Tools.UI.UISetPropertiesTool.Execute(
                new Mosaic.Bridge.Tools.UI.UISetPropertiesParams
                {
                    InstanceId = addResult.Data.InstanceId,
                    Text = "Hello World",
                    FontSize = 24
                });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.ModifiedProperties.Contains("Text"));
            Assert.IsTrue(result.Data.ModifiedProperties.Contains("FontSize"));
        }

        [Test]
        public void SetProperties_ButtonInteractable_SetsFlag()
        {
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_BtnInt" });
            Assert.IsTrue(canvasResult.Success, canvasResult.Error);
            Track(canvasResult.Data.InstanceId);

            var addResult = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId,
                    ElementType = "button",
                    Name = "InteractTestBtn"
                });
            Assert.IsTrue(addResult.Success, addResult.Error);
            Track(addResult.Data.InstanceId);

            var result = Mosaic.Bridge.Tools.UI.UISetPropertiesTool.Execute(
                new Mosaic.Bridge.Tools.UI.UISetPropertiesParams
                {
                    InstanceId = addResult.Data.InstanceId,
                    Interactable = false
                });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.ModifiedProperties.Contains("Interactable"));
        }

        // ── ui/info ──────────────────────────────────────────────────────────

        [Test]
        public void Info_AfterCreateCanvas_ReturnsCanvasHierarchy()
        {
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_Info" });
            Assert.IsTrue(canvasResult.Success, canvasResult.Error);
            Track(canvasResult.Data.InstanceId);

            // Add a button child
            var addResult = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId,
                    ElementType = "button",
                    Name = "InfoTestButton"
                });
            Assert.IsTrue(addResult.Success, addResult.Error);
            Track(addResult.Data.InstanceId);

            // Query info
            var result = Mosaic.Bridge.Tools.UI.UIInfoTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIInfoParams
                {
                    InstanceId = canvasResult.Data.InstanceId
                });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.Canvases);
            Assert.IsTrue(result.Data.Canvases.Length > 0);
            Assert.AreEqual("TestCanvas_Info", result.Data.Canvases[0].Name);
            Assert.IsTrue(result.Data.Canvases[0].Children.Length > 0,
                "Canvas should have at least the button child");
        }

        [Test]
        public void Info_AllCanvases_ReturnsResults()
        {
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "TestCanvas_InfoAll" });
            Assert.IsTrue(canvasResult.Success, canvasResult.Error);
            Track(canvasResult.Data.InstanceId);

            var result = Mosaic.Bridge.Tools.UI.UIInfoTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIInfoParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.Canvases);
            Assert.IsTrue(result.Data.Canvases.Length >= 1);
        }

        // ── Integration: create canvas + add button + verify hierarchy ───────

        [Test]
        public void Integration_CreateCanvas_AddButton_VerifyHierarchy()
        {
            // 1. Create canvas
            var canvasResult = Mosaic.Bridge.Tools.UI.UICreateCanvasTool.Execute(
                new Mosaic.Bridge.Tools.UI.UICreateCanvasParams { Name = "IntegrationCanvas" });
            Assert.IsTrue(canvasResult.Success, canvasResult.Error);
            Track(canvasResult.Data.InstanceId);

            // 2. Add button
            var buttonResult = Mosaic.Bridge.Tools.UI.UIAddElementTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIAddElementParams
                {
                    ParentInstanceId = canvasResult.Data.InstanceId,
                    ElementType = "button",
                    Name = "IntegrationButton"
                });
            Assert.IsTrue(buttonResult.Success, buttonResult.Error);
            Track(buttonResult.Data.InstanceId);

            // 3. Verify via info
            var infoResult = Mosaic.Bridge.Tools.UI.UIInfoTool.Execute(
                new Mosaic.Bridge.Tools.UI.UIInfoParams
                {
                    InstanceId = canvasResult.Data.InstanceId
                });
            Assert.IsTrue(infoResult.Success, infoResult.Error);
            Assert.AreEqual(1, infoResult.Data.Canvases.Length);

            var canvasInfo = infoResult.Data.Canvases[0];
            Assert.AreEqual("IntegrationCanvas", canvasInfo.Name);

            // The button and its Text child should be in the children list
            var buttonChild = System.Array.Find(canvasInfo.Children,
                c => c.Name == "IntegrationButton");
            Assert.IsNotNull(buttonChild, "Button should appear in Canvas children");
            Assert.IsTrue(buttonChild.ChildCount > 0, "Button should have a Text child");
        }

        // ── ui/add_listener, ui/remove_listener ───────────────────────────────
        //
        // O4 §4.6: "the course wired GameHUD 'via SerializedObject' in a hand-written editor
        // script" because nothing else added a PERSISTENT listener (the Inspector "+"-button
        // kind, saved with the scene). UnityEvent.AddListener is runtime-only and would not have
        // helped even if exposed. These invoke the real event afterward to confirm the listener
        // actually fires — a persistent listener that was added but never invokes is a much more
        // dangerous failure than a route that errors outright.

        private ListenerProbe CreateProbe(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go.AddComponent<ListenerProbe>();
        }

        [Test]
        public void AddListener_ButtonOnClick_VoidMethod_ActuallyInvokes()
        {
            var buttonGo = new GameObject("ProbeButton");
            _created.Add(buttonGo);
            var button = buttonGo.AddComponent<Button>();
            var probe = CreateProbe("Probe1");

            var result = Mosaic.Bridge.Tools.UI.UIAddListenerTool.Execute(new Mosaic.Bridge.Tools.UI.UIAddListenerParams
            {
                GameObjectName = "ProbeButton", ComponentType = "Button", EventName = "onClick",
                TargetGameObjectName = "Probe1", TargetComponentType = "ListenerProbe", MethodName = "OnVoid",
                CallState = "EditorAndRuntime",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Data.ListenerIndex);

            button.onClick.Invoke();
            Assert.IsTrue(probe.VoidCalled);
        }

        [Test]
        public void AddListener_FloatArg_InvokesWithThePresetValueNotTheRuntimeOne()
        {
            // The defining behavior of a PERSISTENT listener: Slider.onValueChanged.Invoke(999)
            // must still call the probe with the PRESET 3.5, not the 999 passed to Invoke.
            var sliderGo = new GameObject("ProbeSlider");
            _created.Add(sliderGo);
            var slider = sliderGo.AddComponent<Slider>();
            var probe = CreateProbe("Probe2");

            var result = Mosaic.Bridge.Tools.UI.UIAddListenerTool.Execute(new Mosaic.Bridge.Tools.UI.UIAddListenerParams
            {
                GameObjectName = "ProbeSlider", ComponentType = "Slider", EventName = "onValueChanged",
                TargetGameObjectName = "Probe2", TargetComponentType = "ListenerProbe", MethodName = "OnFloat",
                FloatArg = 3.5f, CallState = "EditorAndRuntime",
            });

            Assert.IsTrue(result.Success, result.Error);
            slider.onValueChanged.Invoke(999f);
            Assert.AreEqual(3.5f, probe.FloatArg, 0.001f);
        }

        [Test]
        public void AddListener_CallState_ReflectsRequestedValue()
        {
            var buttonGo = new GameObject("ProbeButtonState");
            _created.Add(buttonGo);
            var button = buttonGo.AddComponent<Button>();
            var probe = CreateProbe("Probe3");

            var result = Mosaic.Bridge.Tools.UI.UIAddListenerTool.Execute(new Mosaic.Bridge.Tools.UI.UIAddListenerParams
            {
                GameObjectName = "ProbeButtonState", ComponentType = "Button", EventName = "onClick",
                TargetGameObjectName = "Probe3", TargetComponentType = "ListenerProbe", MethodName = "OnVoid",
                CallState = "EditorAndRuntime",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(UnityEventCallState.EditorAndRuntime,
                button.onClick.GetPersistentListenerState(result.Data.ListenerIndex));
        }

        [Test]
        public void AddListener_UnknownEventName_Fails()
        {
            var buttonGo = new GameObject("ProbeButtonBadEvent");
            _created.Add(buttonGo);
            buttonGo.AddComponent<Button>();
            CreateProbe("Probe4");

            var result = Mosaic.Bridge.Tools.UI.UIAddListenerTool.Execute(new Mosaic.Bridge.Tools.UI.UIAddListenerParams
            {
                GameObjectName = "ProbeButtonBadEvent", ComponentType = "Button", EventName = "notAnEvent",
                TargetGameObjectName = "Probe4", TargetComponentType = "ListenerProbe", MethodName = "OnVoid",
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void AddListener_UnknownMethod_Fails()
        {
            var buttonGo = new GameObject("ProbeButtonBadMethod");
            _created.Add(buttonGo);
            buttonGo.AddComponent<Button>();
            CreateProbe("Probe5");

            var result = Mosaic.Bridge.Tools.UI.UIAddListenerTool.Execute(new Mosaic.Bridge.Tools.UI.UIAddListenerParams
            {
                GameObjectName = "ProbeButtonBadMethod", ComponentType = "Button", EventName = "onClick",
                TargetGameObjectName = "Probe5", TargetComponentType = "ListenerProbe", MethodName = "NotAMethod",
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void AddListener_TwoArgMethod_RejectedAsUnsupportedArity()
        {
            var buttonGo = new GameObject("ProbeButtonTwoArg");
            _created.Add(buttonGo);
            buttonGo.AddComponent<Button>();
            CreateProbe("Probe6");

            var result = Mosaic.Bridge.Tools.UI.UIAddListenerTool.Execute(new Mosaic.Bridge.Tools.UI.UIAddListenerParams
            {
                GameObjectName = "ProbeButtonTwoArg", ComponentType = "Button", EventName = "onClick",
                TargetGameObjectName = "Probe6", TargetComponentType = "ListenerProbe", MethodName = "OnTwoArgs",
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void RemoveListener_RemovesTheListener_AndItNoLongerInvokes()
        {
            var buttonGo = new GameObject("ProbeButtonRemove");
            _created.Add(buttonGo);
            var button = buttonGo.AddComponent<Button>();
            var probe = CreateProbe("Probe7");

            var addResult = Mosaic.Bridge.Tools.UI.UIAddListenerTool.Execute(new Mosaic.Bridge.Tools.UI.UIAddListenerParams
            {
                GameObjectName = "ProbeButtonRemove", ComponentType = "Button", EventName = "onClick",
                TargetGameObjectName = "Probe7", TargetComponentType = "ListenerProbe", MethodName = "OnVoid",
                CallState = "EditorAndRuntime",
            });
            Assert.IsTrue(addResult.Success, addResult.Error);

            var removeResult = Mosaic.Bridge.Tools.UI.UIRemoveListenerTool.Execute(new Mosaic.Bridge.Tools.UI.UIRemoveListenerParams
            {
                GameObjectName = "ProbeButtonRemove", ComponentType = "Button", EventName = "onClick",
                Index = addResult.Data.ListenerIndex,
            });

            Assert.IsTrue(removeResult.Success, removeResult.Error);
            Assert.AreEqual(0, removeResult.Data.RemainingListenerCount);
            button.onClick.Invoke();
            Assert.IsFalse(probe.VoidCalled, "the listener was removed and must not fire");
        }

        [Test]
        public void RemoveListener_OutOfRangeIndex_Fails()
        {
            var buttonGo = new GameObject("ProbeButtonRemoveBad");
            _created.Add(buttonGo);
            buttonGo.AddComponent<Button>();

            var result = Mosaic.Bridge.Tools.UI.UIRemoveListenerTool.Execute(new Mosaic.Bridge.Tools.UI.UIRemoveListenerParams
            {
                GameObjectName = "ProbeButtonRemoveBad", ComponentType = "Button", EventName = "onClick", Index = 0,
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void RemoveListener_MissingIndex_Fails()
        {
            var buttonGo = new GameObject("ProbeButtonNoIndex");
            _created.Add(buttonGo);
            buttonGo.AddComponent<Button>();

            var result = Mosaic.Bridge.Tools.UI.UIRemoveListenerTool.Execute(new Mosaic.Bridge.Tools.UI.UIRemoveListenerParams
            {
                GameObjectName = "ProbeButtonNoIndex", ComponentType = "Button", EventName = "onClick",
            });

            Assert.IsFalse(result.Success);
        }

        /// <summary>Target for add_listener/remove_listener tests — public methods spanning the
        /// arities/types persistent listeners must support.</summary>
        internal class ListenerProbe : MonoBehaviour
        {
            public bool VoidCalled;
            public float FloatArg;
            public int IntArg;
            public bool BoolArg;
            public string StringArg;

            public void OnVoid() => VoidCalled = true;
            public void OnFloat(float v) => FloatArg = v;
            public void OnInt(int v) => IntArg = v;
            public void OnBool(bool v) => BoolArg = v;
            public void OnString(string v) => StringArg = v;
            public void OnTwoArgs(int a, int b) { }
        }
    }
}
