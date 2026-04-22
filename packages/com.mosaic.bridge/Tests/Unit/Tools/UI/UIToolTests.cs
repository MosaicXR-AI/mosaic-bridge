using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;

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
#if UNITY_2023_1_OR_NEWER
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
#else
            var eventSystems = Object.FindObjectsOfType<EventSystem>();
#endif
            foreach (var es in eventSystems)
                Object.DestroyImmediate(es.gameObject);
        }

        private void Track(int instanceId)
        {
            var go = Resources.EntityIdToObject(instanceId) as GameObject;
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
            Assert.AreEqual("Overlay", result.Data.RenderMode);
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

            var go = Resources.EntityIdToObject(result.Data.InstanceId) as GameObject;
            Assert.IsNotNull(go);
            Assert.IsNotNull(go.GetComponent<Canvas>());
            Assert.IsNotNull(go.GetComponent<CanvasScaler>());
            Assert.IsNotNull(go.GetComponent<GraphicRaycaster>());
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
            var buttonGo = Resources.EntityIdToObject(result.Data.InstanceId) as GameObject;
            Assert.IsNotNull(buttonGo);
            Assert.IsTrue(buttonGo.transform.childCount > 0, "Button should have a Text child");
        }

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

            var go = Resources.EntityIdToObject(result.Data.InstanceId) as GameObject;
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
    }
}
