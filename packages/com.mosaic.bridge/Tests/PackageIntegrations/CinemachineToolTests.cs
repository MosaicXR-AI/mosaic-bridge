#if MOSAIC_HAS_CINEMACHINE
using NUnit.Framework;
using UnityEngine;
using Unity.Cinemachine;

namespace Mosaic.Bridge.Tests.Cinemachine
{
    [TestFixture]
    public class CinemachineToolTests
    {
        private GameObject _mainCameraGo;
        private GameObject _existingMainCamera;

        [SetUp]
        public void SetUp()
        {
            // Disable any existing MainCamera so our test camera is the main one
            _existingMainCamera = GameObject.FindWithTag("MainCamera");
            if (_existingMainCamera != null)
                _existingMainCamera.SetActive(false);

            _mainCameraGo = new GameObject("TestMainCamera");
            _mainCameraGo.AddComponent<Camera>();
            _mainCameraGo.tag = "MainCamera";
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up all test objects
            var testObjects = new[]
            {
                "TestMainCamera", "TestVCam", "TestVCam2", "FollowTarget",
                "LookAtTarget", "TestDollyTrack", "DollyVCam"
            };
            foreach (var name in testObjects)
            {
                var go = GameObject.Find(name);
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            if (_mainCameraGo != null)
                Object.DestroyImmediate(_mainCameraGo);

            if (_existingMainCamera != null)
                _existingMainCamera.SetActive(true);
        }

        // ── create-vcam ──

        [Test]
        public void CreateVCam_BasicCreation_ReturnsSuccess()
        {
            var p = new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam",
                Priority = 15
            };

            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("TestVCam", result.Data.Name);
            Assert.AreEqual(15, result.Data.Priority);

            var go = GameObject.Find("TestVCam");
            Assert.IsNotNull(go);
            Assert.IsNotNull(go.GetComponent<CinemachineCamera>());
        }

        [Test]
        public void CreateVCam_WithFollowTarget_SetsFollow()
        {
            var target = new GameObject("FollowTarget");

            var p = new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam",
                FollowTarget = "FollowTarget"
            };

            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            var vcam = GameObject.Find("TestVCam").GetComponent<CinemachineCamera>();
            Assert.AreEqual(target.transform, vcam.Follow);
        }

        [Test]
        public void CreateVCam_WithBodyType_AddsComponent()
        {
            var p = new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam",
                BodyType = "ThirdPersonFollow"
            };

            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("ThirdPersonFollow", result.Data.BodyType);
            var go = GameObject.Find("TestVCam");
            Assert.IsNotNull(go.GetComponent<CinemachineThirdPersonFollow>());
        }

        [Test]
        public void CreateVCam_InvalidBodyType_ReturnsFail()
        {
            var p = new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam",
                BodyType = "InvalidType"
            };

            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(p);

            Assert.IsFalse(result.Success);
            Assert.IsNull(GameObject.Find("TestVCam"));
        }

        [Test]
        public void CreateVCam_InvalidFollowTarget_ReturnsFail()
        {
            var p = new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam",
                FollowTarget = "NonExistentTarget"
            };

            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(p);

            Assert.IsFalse(result.Success);
            Assert.IsNull(GameObject.Find("TestVCam"));
        }

        // ── create-brain ──

        [Test]
        public void CreateBrain_OnMainCamera_ReturnsSuccess()
        {
            var p = new Tools.Cinemachine.CinemachineCreateBrainParams
            {
                DefaultBlend = 1.5f,
                BlendType = "EaseInOut"
            };

            var result = Tools.Cinemachine.CinemachineCreateBrainTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("TestMainCamera", result.Data.CameraName);
            Assert.AreEqual(1.5f, result.Data.DefaultBlend, 0.01f);
            Assert.IsFalse(result.Data.AlreadyExisted);

            var brain = _mainCameraGo.GetComponent<CinemachineBrain>();
            Assert.IsNotNull(brain);
        }

        [Test]
        public void CreateBrain_InvalidBlendType_ReturnsFail()
        {
            var p = new Tools.Cinemachine.CinemachineCreateBrainParams
            {
                BlendType = "InvalidBlend"
            };

            var result = Tools.Cinemachine.CinemachineCreateBrainTool.Execute(p);

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void CreateBrain_AlreadyExists_SetsAlreadyExisted()
        {
            _mainCameraGo.AddComponent<CinemachineBrain>();

            var p = new Tools.Cinemachine.CinemachineCreateBrainParams();
            var result = Tools.Cinemachine.CinemachineCreateBrainTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.AlreadyExisted);
        }

        // ── info ──

        [Test]
        public void Info_NoVCams_ReturnsEmptyList()
        {
            var p = new Tools.Cinemachine.CinemachineInfoParams();

            var result = Tools.Cinemachine.CinemachineInfoTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.VirtualCameras);
        }

        [Test]
        public void Info_AfterCreateVCam_ReturnsCreatedCamera()
        {
            // Create a vcam first
            var createP = new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam",
                Priority = 20
            };
            var createResult = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(createP);
            Assert.IsTrue(createResult.Success);

            var p = new Tools.Cinemachine.CinemachineInfoParams();
            var result = Tools.Cinemachine.CinemachineInfoTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.GreaterOrEqual(result.Data.VirtualCameras.Length, 1);

            bool found = false;
            foreach (var vcam in result.Data.VirtualCameras)
            {
                if (vcam.Name == "TestVCam")
                {
                    found = true;
                    Assert.AreEqual(20, vcam.Priority);
                    break;
                }
            }
            Assert.IsTrue(found, "TestVCam not found in info results");
        }

        [Test]
        public void Info_FilterByName_ReturnsOnlyMatching()
        {
            // Create two vcams
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam2" });

            var p = new Tools.Cinemachine.CinemachineInfoParams { VCamName = "TestVCam" };
            var result = Tools.Cinemachine.CinemachineInfoTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.VirtualCameras.Length);
            Assert.AreEqual("TestVCam", result.Data.VirtualCameras[0].Name);
        }

        // ── set-properties ──

        [Test]
        public void SetProperties_Priority_UpdatesValue()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam", Priority = 10 });

            var p = new Tools.Cinemachine.CinemachineSetPropertiesParams
            {
                VCamName = "TestVCam",
                Priority = 50
            };

            var result = Tools.Cinemachine.CinemachineSetPropertiesTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.Contains("Priority", result.Data.PropertiesSet);

            var vcam = GameObject.Find("TestVCam").GetComponent<CinemachineCamera>();
            Assert.AreEqual(50, (int)vcam.Priority.Value);
        }

        [Test]
        public void SetProperties_NonExistentVCam_ReturnsFail()
        {
            var p = new Tools.Cinemachine.CinemachineSetPropertiesParams
            {
                VCamName = "NonExistentVCam",
                Priority = 50
            };

            var result = Tools.Cinemachine.CinemachineSetPropertiesTool.Execute(p);

            Assert.IsFalse(result.Success);
        }

        // ── create-dolly ──

        [Test]
        public void CreateDolly_BasicTrack_ReturnsSuccess()
        {
            var p = new Tools.Cinemachine.CinemachineCreateDollyParams
            {
                Name = "TestDollyTrack",
                Waypoints = new float[] { 0, 0, 0, 5, 2, 0, 10, 0, 5 }
            };

            var result = Tools.Cinemachine.CinemachineCreateDollyTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("TestDollyTrack", result.Data.TrackName);
            Assert.AreEqual(3, result.Data.WaypointCount);
            Assert.IsNull(result.Data.AttachedToVCam);
        }

        [Test]
        public void CreateDolly_TooFewWaypoints_ReturnsFail()
        {
            var p = new Tools.Cinemachine.CinemachineCreateDollyParams
            {
                Name = "TestDollyTrack",
                Waypoints = new float[] { 0, 0, 0 } // only 1 waypoint
            };

            var result = Tools.Cinemachine.CinemachineCreateDollyTool.Execute(p);

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void CreateDolly_AttachToVCam_SetsSplineDolly()
        {
            // Create a vcam first
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "DollyVCam" });

            var p = new Tools.Cinemachine.CinemachineCreateDollyParams
            {
                Name = "TestDollyTrack",
                Waypoints = new float[] { 0, 0, 0, 10, 5, 10 },
                VCamName = "DollyVCam",
                AutoDolly = true
            };

            var result = Tools.Cinemachine.CinemachineCreateDollyTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("DollyVCam", result.Data.AttachedToVCam);
            Assert.IsTrue(result.Data.AutoDollyEnabled);

            var vcamGo = GameObject.Find("DollyVCam");
            Assert.IsNotNull(vcamGo.GetComponent<CinemachineSplineDolly>());
        }
    }
}
#endif
