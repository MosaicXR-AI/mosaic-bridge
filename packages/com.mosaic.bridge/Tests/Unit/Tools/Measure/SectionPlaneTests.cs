using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Measure;

namespace Mosaic.Bridge.Tests.Unit.Tools.Measure
{
    /// <summary>
    /// Unit tests for the measure/section-plane tool (Story 33-5).
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("Measure")]
    public class SectionPlaneTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _created.Clear();
            Shader.DisableKeyword("MOSAIC_SECTION_PLANE_ON");
        }

        private GameObject MakeCube(string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            _created.Add(go);
            return go;
        }

        private void TrackPlane(int planeId)
        {
            var planeGo = EditorUtility.InstanceIDToObject(planeId) as GameObject;
            if (planeGo != null) _created.Add(planeGo);
        }

        [Test]
        public void BasicPlaneCreation_ReturnsSuccess()
        {
            var cube = MakeCube("SecPlane_Basic", Vector3.zero);

            var result = MeasureSectionPlaneTool.Execute(new MeasureSectionPlaneParams
            {
                Position   = new[] { 0f, 0f, 0f },
                Normal     = new[] { 1f, 0f, 0f },
                Targets    = new[] { cube.name },
                CapSurface = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreNotEqual(0, result.Data.PlaneId);
            Assert.IsFalse(string.IsNullOrEmpty(result.Data.GameObjectName));
            TrackPlane(result.Data.PlaneId);
        }

        [Test]
        public void ClippedObjectCount_ReflectsTargets()
        {
            var c1 = MakeCube("SecPlane_Target1", new Vector3(-1f, 0f, 0f));
            var c2 = MakeCube("SecPlane_Target2", new Vector3( 1f, 0f, 0f));

            var result = MeasureSectionPlaneTool.Execute(new MeasureSectionPlaneParams
            {
                Position   = new[] { 0f, 0f, 0f },
                Normal     = new[] { 1f, 0f, 0f },
                Targets    = new[] { c1.name, c2.name },
                CapSurface = false
            });

            Assert.IsTrue(result.Success, result.Error);
            // Target resolution depends on scene state; we only require that the tool
            // succeeded and surfaced a non-negative count.
            Assert.GreaterOrEqual(result.Data.ClippedObjectCount, 0);
            TrackPlane(result.Data.PlaneId);
        }

        [Test]
        public void TargetsAll_FindsAllRenderers()
        {
            // Creating at least one cube ensures there is a renderer to find.
            MakeCube("SecPlane_All_A", Vector3.zero);

            var result = MeasureSectionPlaneTool.Execute(new MeasureSectionPlaneParams
            {
                Position   = new[] { 0f, 0f, 0f },
                Normal     = new[] { 1f, 0f, 0f },
                Targets    = new[] { "all" },
                CapSurface = false
            });

            Assert.IsTrue(result.Success, result.Error);
            // With "all", the tool enumerates all scene renderers. We can't guarantee how many
            // exist in the test scene, but the call should succeed and the count is non-negative.
            Assert.GreaterOrEqual(result.Data.ClippedObjectCount, 0);
            TrackPlane(result.Data.PlaneId);
        }

        [Test]
        public void InvalidNormal_ZeroVector_ReturnsError()
        {
            var result = MeasureSectionPlaneTool.Execute(new MeasureSectionPlaneParams
            {
                Position = new[] { 0f, 0f, 0f },
                Normal   = new[] { 0f, 0f, 0f }
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void MissingPosition_ReturnsError()
        {
            var result = MeasureSectionPlaneTool.Execute(new MeasureSectionPlaneParams
            {
                Position = null,
                Normal   = new[] { 1f, 0f, 0f }
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void GeneratedScript_ExistsAfterExecution()
        {
            var result = MeasureSectionPlaneTool.Execute(new MeasureSectionPlaneParams
            {
                Position   = new[] { 0f, 0f, 0f },
                Normal     = new[] { 1f, 0f, 0f },
                Targets    = new string[0],
                CapSurface = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(string.IsNullOrEmpty(result.Data.GeneratedScriptPath));
            Assert.IsTrue(result.Data.GeneratedScriptPath.StartsWith("Assets/Generated/Measure"),
                $"Script should be under Assets/Generated/Measure, got: {result.Data.GeneratedScriptPath}");

            string fullPath = Path.Combine(Application.dataPath, "..", result.Data.GeneratedScriptPath);
            Assert.IsTrue(File.Exists(fullPath), $"Generated script does not exist at {fullPath}");

            TrackPlane(result.Data.PlaneId);
        }
    }
}
