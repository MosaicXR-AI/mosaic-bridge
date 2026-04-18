using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Measure;

namespace Mosaic.Bridge.Tests.Unit.Tools.Measure
{
    [TestFixture]
    [Category("Unit")]
    public class SightlineTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        GameObject SpawnCube(string name, Vector3 pos, Vector3 scale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = pos;
            cube.transform.localScale = scale;
            _spawned.Add(cube);
            // Physics world doesn't auto-sync in EditMode — force sync so raycasts see the collider
            UnityEngine.Physics.SyncTransforms();
            return cube;
        }

        [Test]
        public void Sightline_UnobstructedTarget_ReturnsVisible()
        {
            var result = AnalysisSightlineTool.Execute(new AnalysisSightlineParams
            {
                ViewerPosition = new float[] { 0f, 0f, 0f },
                Mode = "sightline",
                Targets = new float[][] { new float[] { 0f, 0f, 5f } },
                MaxDistance = 100f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.TotalTargets);
            Assert.AreEqual(1, result.Data.VisibleCount);
            Assert.AreEqual(0, result.Data.BlockedCount);
            Assert.IsTrue(result.Data.Results[0].IsVisible);
        }

        [Test]
        public void Sightline_ThroughBlocker_ReturnsBlocked()
        {
            // Blocker cube between viewer (origin) and target at (0,0,10).
            SpawnCube("SightBlocker", new Vector3(0f, 0f, 5f), Vector3.one * 2f);

            var result = AnalysisSightlineTool.Execute(new AnalysisSightlineParams
            {
                ViewerPosition = new float[] { 0f, 0f, 0f },
                Mode = "sightline",
                Targets = new float[][] { new float[] { 0f, 0f, 10f } },
                MaxDistance = 50f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.TotalTargets);
            Assert.AreEqual(0, result.Data.VisibleCount);
            Assert.AreEqual(1, result.Data.BlockedCount);
            Assert.IsFalse(result.Data.Results[0].IsVisible);
            Assert.AreEqual("SightBlocker", result.Data.Results[0].BlockedBy);
        }

        [Test]
        public void Viewshed_OpenScene_ReturnsHighVisibility()
        {
            var result = AnalysisSightlineTool.Execute(new AnalysisSightlineParams
            {
                ViewerPosition = new float[] { 0f, 50f, 0f },
                Mode = "viewshed",
                ViewshedResolution = 16,
                MaxDistance = 100f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("viewshed", result.Data.Mode);
            // Open sky → expect ≥ 80% clear.
            Assert.GreaterOrEqual(result.Data.ViewshedPercent, 80f,
                $"Expected high viewshed; got {result.Data.ViewshedPercent}%");
        }

        [Test]
        public void Cone_WithTargetsInFront_FindsThem()
        {
            var result = AnalysisSightlineTool.Execute(new AnalysisSightlineParams
            {
                ViewerPosition = new float[] { 0f, 0f, 0f },
                Mode = "cone",
                FieldOfView = 60f,
                LookDirection = new float[] { 0f, 0f, 1f },
                MaxDistance = 50f,
                Targets = new float[][]
                {
                    new float[] { 0f, 0f, 10f },   // straight ahead → inside 60° cone
                    new float[] { 10f, 0f, 0f },   // to the right → outside cone
                },
            });

            Assert.IsTrue(result.Success, result.Error);
            // Only the front target should have been evaluated.
            Assert.AreEqual(1, result.Data.TotalTargets);
            Assert.AreEqual(1, result.Data.VisibleCount);
        }

        [Test]
        public void Sightline_EmptyTargets_ReturnsZeroCounts()
        {
            var result = AnalysisSightlineTool.Execute(new AnalysisSightlineParams
            {
                ViewerPosition = new float[] { 0f, 0f, 0f },
                Mode = "sightline",
                // No Targets, no TargetGameObjects.
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Data.TotalTargets);
            Assert.AreEqual(0, result.Data.VisibleCount);
            Assert.AreEqual(0, result.Data.BlockedCount);
        }

        [Test]
        public void MissingViewerPosition_ReturnsError()
        {
            var result = AnalysisSightlineTool.Execute(new AnalysisSightlineParams
            {
                Mode = "sightline",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void InvalidMode_ReturnsError()
        {
            var result = AnalysisSightlineTool.Execute(new AnalysisSightlineParams
            {
                ViewerPosition = new float[] { 0f, 0f, 0f },
                Mode = "x-ray",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
