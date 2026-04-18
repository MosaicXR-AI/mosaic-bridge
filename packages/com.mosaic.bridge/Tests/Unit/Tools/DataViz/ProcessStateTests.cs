using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.DataViz;

namespace Mosaic.Bridge.Tests.Unit.Tools.DataViz
{
    [TestFixture]
    [Category("Unit")]
    public class ProcessStateTests
    {
        private readonly List<GameObject> _gos = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gos.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _gos)
                if (go != null) Object.DestroyImmediate(go);
            _gos.Clear();
        }

        private GameObject MakeCube(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            _gos.Add(go);
            return go;
        }

        // ---------------- State color tests ----------------

        [Test]
        public void RunningState_AppliesGreenColor()
        {
            var go = MakeCube("Machine_Running_T");
            var r = ProcessStateSetTool.Execute(new ProcessStateSetParams
            {
                TargetGameObject = go.name,
                State = "running",
                DisplayMode = "color"
            });

            Assert.IsTrue(r.Success, r.Error);
            Assert.AreEqual("running", r.Data.State);
            var col = go.GetComponent<Renderer>().sharedMaterial.color;
            Assert.AreEqual(0f, col.r, 0.01f);
            Assert.AreEqual(1f, col.g, 0.01f);
            Assert.AreEqual(0f, col.b, 0.01f);
        }

        [Test]
        public void FaultState_AppliesRedColor()
        {
            var go = MakeCube("Machine_Fault_T");
            var r = ProcessStateSetTool.Execute(new ProcessStateSetParams
            {
                TargetGameObject = go.name,
                State = "fault",
                DisplayMode = "color"
            });

            Assert.IsTrue(r.Success, r.Error);
            var col = go.GetComponent<Renderer>().sharedMaterial.color;
            Assert.AreEqual(1f, col.r, 0.01f);
            Assert.AreEqual(0f, col.g, 0.01f);
            Assert.AreEqual(0f, col.b, 0.01f);
        }

        [Test]
        public void IconMode_CreatesTextMeshChild()
        {
            var go = MakeCube("Machine_Icon_T");
            var r = ProcessStateSetTool.Execute(new ProcessStateSetParams
            {
                TargetGameObject = go.name,
                State = "fault",
                DisplayMode = "icon"
            });

            Assert.IsTrue(r.Success, r.Error);
            var icon = go.transform.Find("__ProcessStateIcon");
            Assert.IsNotNull(icon, "Icon child not created");
            var tm = icon.GetComponent<TextMesh>();
            Assert.IsNotNull(tm);
            Assert.AreEqual("⚠", tm.text);
        }

        [Test]
        public void CombinedMode_CreatesBothColorAndIcon()
        {
            var go = MakeCube("Machine_Combined_T");
            var r = ProcessStateSetTool.Execute(new ProcessStateSetParams
            {
                TargetGameObject = go.name,
                State = "maintenance",
                DisplayMode = "combined"
            });

            Assert.IsTrue(r.Success, r.Error);
            // Color applied
            var col = go.GetComponent<Renderer>().sharedMaterial.color;
            Assert.AreEqual(1f, col.r, 0.01f);
            Assert.AreEqual(0.6f, col.g, 0.01f);
            // Icon child created
            Assert.IsNotNull(go.transform.Find("__ProcessStateIcon"));
        }

        [Test]
        public void CustomStateConfig_OverridesDefaults()
        {
            var go = MakeCube("Machine_Custom_T");
            var r = ProcessStateSetTool.Execute(new ProcessStateSetParams
            {
                TargetGameObject = go.name,
                State = "running",
                DisplayMode = "color",
                StateConfig = new List<StateDef>
                {
                    new StateDef
                    {
                        State = "running",
                        Color = new float[] { 0.5f, 0.25f, 0.75f, 1f },
                        IconText = "*"
                    }
                }
            });

            Assert.IsTrue(r.Success, r.Error);
            var col = go.GetComponent<Renderer>().sharedMaterial.color;
            Assert.AreEqual(0.5f, col.r, 0.01f);
            Assert.AreEqual(0.25f, col.g, 0.01f);
            Assert.AreEqual(0.75f, col.b, 0.01f);
        }

        [Test]
        public void InvalidState_UsesDefaultWhite()
        {
            var go = MakeCube("Machine_Invalid_T");
            var r = ProcessStateSetTool.Execute(new ProcessStateSetParams
            {
                TargetGameObject = go.name,
                State = "spaghettiState",
                DisplayMode = "color"
            });

            Assert.IsTrue(r.Success, r.Error);
            var col = go.GetComponent<Renderer>().sharedMaterial.color;
            Assert.AreEqual(1f, col.r, 0.01f);
            Assert.AreEqual(1f, col.g, 0.01f);
            Assert.AreEqual(1f, col.b, 0.01f);
        }

        // ---------------- Validation tests ----------------

        [Test]
        public void MissingTarget_ReturnsInvalidParam()
        {
            var r = ProcessStateSetTool.Execute(new ProcessStateSetParams
            {
                State = "running"
            });
            Assert.IsFalse(r.Success);
            Assert.AreEqual("INVALID_PARAM", r.ErrorCode);
        }

        [Test]
        public void MissingState_ReturnsInvalidParam()
        {
            var go = MakeCube("Machine_NoState_T");
            var r = ProcessStateSetTool.Execute(new ProcessStateSetParams
            {
                TargetGameObject = go.name
            });
            Assert.IsFalse(r.Success);
            Assert.AreEqual("INVALID_PARAM", r.ErrorCode);
        }

        [Test]
        public void TargetNotFound_ReturnsNotFound()
        {
            var r = ProcessStateSetTool.Execute(new ProcessStateSetParams
            {
                TargetGameObject = "NonExistent_GO_99001",
                State = "running"
            });
            Assert.IsFalse(r.Success);
            Assert.AreEqual("NOT_FOUND", r.ErrorCode);
        }

        [Test]
        public void InvalidDisplayMode_ReturnsInvalidParam()
        {
            var go = MakeCube("Machine_BadMode_T");
            var r = ProcessStateSetTool.Execute(new ProcessStateSetParams
            {
                TargetGameObject = go.name,
                State = "running",
                DisplayMode = "hologram"
            });
            Assert.IsFalse(r.Success);
            Assert.AreEqual("INVALID_PARAM", r.ErrorCode);
        }

        // ---------------- Flow visualize tests ----------------

        [Test]
        public void Flow_BetweenTwoGOs_CreatesParticleSystem()
        {
            var a = MakeCube("FlowA_T");
            var b = MakeCube("FlowB_T");
            a.transform.position = Vector3.zero;
            b.transform.position = new Vector3(5f, 0f, 0f);

            var r = ProcessFlowVisualizeTool.Execute(new ProcessFlowVisualizeParams
            {
                Flows = new List<Flow>
                {
                    new Flow { From = "FlowA_T", To = "FlowB_T", Value = 1f }
                }
            });

            Assert.IsTrue(r.Success, r.Error);
            Assert.AreEqual(1, r.Data.FlowCount);
            Assert.AreEqual(1, r.Data.ActiveParticleSystems);

            var root = GameObject.Find(r.Data.GameObjectName);
            Assert.IsNotNull(root);
            _gos.Add(root);
            var ps = root.GetComponentInChildren<ParticleSystem>();
            Assert.IsNotNull(ps, "Expected a ParticleSystem child for the flow");
        }

        [Test]
        public void EmptyFlows_ReturnsError()
        {
            var r = ProcessFlowVisualizeTool.Execute(new ProcessFlowVisualizeParams
            {
                Flows = new List<Flow>()
            });
            Assert.IsFalse(r.Success);
            Assert.AreEqual("INVALID_PARAM", r.ErrorCode);
        }

        [Test]
        public void NullFlows_ReturnsError()
        {
            var r = ProcessFlowVisualizeTool.Execute(new ProcessFlowVisualizeParams
            {
                Flows = null
            });
            Assert.IsFalse(r.Success);
            Assert.AreEqual("INVALID_PARAM", r.ErrorCode);
        }

        [Test]
        public void Flow_MissingFromGO_ReturnsNotFound()
        {
            var b = MakeCube("FlowOnlyB_T");
            var r = ProcessFlowVisualizeTool.Execute(new ProcessFlowVisualizeParams
            {
                Flows = new List<Flow>
                {
                    new Flow { From = "FlowGhost_T_zzz", To = "FlowOnlyB_T", Value = 1f }
                }
            });
            Assert.IsFalse(r.Success);
            Assert.AreEqual("NOT_FOUND", r.ErrorCode);
        }

        [Test]
        public void Flow_MissingToGO_ReturnsNotFound()
        {
            var a = MakeCube("FlowOnlyA_T");
            var r = ProcessFlowVisualizeTool.Execute(new ProcessFlowVisualizeParams
            {
                Flows = new List<Flow>
                {
                    new Flow { From = "FlowOnlyA_T", To = "FlowGhost_T_zzz", Value = 1f }
                }
            });
            Assert.IsFalse(r.Success);
            Assert.AreEqual("NOT_FOUND", r.ErrorCode);
        }
    }
}
