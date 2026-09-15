#if MOSAIC_HAS_CINEMACHINE
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
#if MOSAIC_HAS_TIMELINE
using System.Linq;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mosaic.Bridge.Tools.Timeline;
#endif

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
            // Qualify — Unity.Cinemachine contains a `Camera` sub-namespace that
            // shadows the unqualified `Camera` identifier once we import its root.
            _mainCameraGo.AddComponent<UnityEngine.Camera>();
            _mainCameraGo.tag = "MainCamera";
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up all test objects
            var testObjects = new[]
            {
                "TestMainCamera", "TestVCam", "TestVCam2", "FollowTarget",
                "LookAtTarget", "TestDollyTrack", "DollyVCam", "TestCart"
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

        // L4: setting FieldOfView used to rebuild LensSettings from just 4 fields, silently
        // resetting Dutch, ModeOverride and the entire PhysicalProperties block to their
        // zero-values. This drives Dutch to a distinctive non-default value first, then sets
        // FOV, and asserts Dutch (and OrthographicSize, also not requested) survived untouched.
        [Test]
        public void SetProperties_FieldOfView_PreservesOtherLensFields()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });
            var vcam = GameObject.Find("TestVCam").GetComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.Dutch = 12.5f;
            lens.OrthographicSize = 7.25f;
            vcam.Lens = lens;

            var result = Tools.Cinemachine.CinemachineSetPropertiesTool.Execute(
                new Tools.Cinemachine.CinemachineSetPropertiesParams { VCamName = "TestVCam", FieldOfView = 45f });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(45f, vcam.Lens.FieldOfView, 0.001f);
            Assert.AreEqual(12.5f, vcam.Lens.Dutch, 0.001f, "Dutch must survive a FieldOfView-only update");
            Assert.AreEqual(7.25f, vcam.Lens.OrthographicSize, 0.001f, "OrthographicSize must survive too");
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

        // O4 §4.5 P2: bodies/aims/noise/lens — the "first vcam" lesson uses Follow, which was
        // missing; PanTilt for a security-cam pan/tilt; noise for handheld shake.

        [Test]
        public void CreateVCam_BodyFollow_SetsFollowOffset()
        {
            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam", BodyType = "Follow", FollowOffset = new float[] { 1, 2, 3 },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Follow", result.Data.BodyType);
            var follow = GameObject.Find("TestVCam").GetComponent<CinemachineFollow>();
            Assert.IsNotNull(follow);
            Assert.AreEqual(new Vector3(1, 2, 3), follow.FollowOffset);
        }

        [Test]
        public void CreateVCam_BodyHardLockToTarget_AddsComponent()
        {
            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam", BodyType = "HardLockToTarget",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(GameObject.Find("TestVCam").GetComponent<CinemachineHardLockToTarget>());
        }

        [Test]
        public void CreateVCam_AimPanTilt_SetsPanAndTiltAngle()
        {
            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam", AimType = "PanTilt", PanAngle = 45f, TiltAngle = -10f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("PanTilt", result.Data.AimType);
            var panTilt = GameObject.Find("TestVCam").GetComponent<CinemachinePanTilt>();
            Assert.IsNotNull(panTilt);
            Assert.AreEqual(45f, panTilt.PanAxis.Value, 0.0001f);
            Assert.AreEqual(-10f, panTilt.TiltAxis.Value, 0.0001f);
        }

        [Test]
        public void CreateVCam_AimRotateWithFollowTarget_AddsComponent()
        {
            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam", AimType = "RotateWithFollowTarget",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(GameObject.Find("TestVCam").GetComponent<CinemachineRotateWithFollowTarget>());
        }

        [Test]
        public void CreateVCam_NoiseBasicMultiChannelPerlin_SetsGains()
        {
            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam", NoiseType = "BasicMultiChannelPerlin",
                NoiseAmplitudeGain = 2f, NoiseFrequencyGain = 0.5f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("BasicMultiChannelPerlin", result.Data.NoiseType);
            var perlin = GameObject.Find("TestVCam").GetComponent<CinemachineBasicMultiChannelPerlin>();
            Assert.IsNotNull(perlin);
            Assert.AreEqual(2f, perlin.AmplitudeGain, 0.0001f);
            Assert.AreEqual(0.5f, perlin.FrequencyGain, 0.0001f);
        }

        [Test]
        public void CreateVCam_NoiseProfileNotFound_ReturnsFail()
        {
            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam", NoiseType = "BasicMultiChannelPerlin",
                NoiseProfilePresetName = "NoSuchNoiseProfileAnywhere",
            });

            Assert.IsFalse(result.Success);
            Assert.IsNull(GameObject.Find("TestVCam"), "a failed create must not leave a partial GameObject behind");
        }

        [Test]
        public void CreateVCam_LensBlock_SetsDutchAndOrthographicSizeAndModeOverride()
        {
            var result = Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam", Dutch = 5f, OrthographicSize = 8f, LensModeOverride = "Orthographic",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(5f, result.Data.Dutch, 0.0001f);
            Assert.AreEqual(8f, result.Data.OrthographicSize, 0.0001f);
            Assert.AreEqual("Orthographic", result.Data.LensModeOverride);
            var vcam = GameObject.Find("TestVCam").GetComponent<CinemachineCamera>();
            Assert.AreEqual(LensSettings.OverrideModes.Orthographic, vcam.Lens.ModeOverride);
        }

        [Test]
        public void Info_ReportsNewBodyAimNoiseAndLensFields()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams
            {
                Name = "TestVCam", BodyType = "Follow", AimType = "PanTilt",
                NoiseType = "BasicMultiChannelPerlin", Dutch = 3f,
            });

            var result = Tools.Cinemachine.CinemachineInfoTool.Execute(new Tools.Cinemachine.CinemachineInfoParams
            {
                VCamName = "TestVCam",
            });

            Assert.IsTrue(result.Success, result.Error);
            var info = result.Data.VirtualCameras[0];
            CollectionAssert.Contains(info.BodyComponents, "Follow");
            CollectionAssert.Contains(info.AimComponents, "PanTilt");
            CollectionAssert.Contains(info.NoiseComponents, "BasicMultiChannelPerlin");
            Assert.AreEqual(3f, info.Dutch, 0.0001f);
        }

        // O4 §4.5 P2: cinemachine/add-extension — 2D camera confined to level bounds is the
        // standard 2D camera lesson; handheld shake/collision avoidance/storyboard, etc.

        [Test]
        public void AddExtension_Confiner2D_SetsBoundingShapeAndBakes()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });
            var boundsGo = new GameObject("TestVCamBounds");
            var polygon = boundsGo.AddComponent<PolygonCollider2D>();
            polygon.points = new[] { new Vector2(-5, -5), new Vector2(5, -5), new Vector2(5, 5), new Vector2(-5, 5) };

            try
            {
                var result = Tools.Cinemachine.CinemachineAddExtensionTool.Execute(new Tools.Cinemachine.CinemachineAddExtensionParams
                {
                    VCamName = "TestVCam", ExtensionType = "Confiner2D",
                    ConfinerBoundingShapeName = "TestVCamBounds", ConfinerDamping = 0.5f,
                });

                Assert.IsTrue(result.Success, result.Error);
                var confiner = GameObject.Find("TestVCam").GetComponent<CinemachineConfiner2D>();
                Assert.IsNotNull(confiner);
                Assert.AreEqual(polygon, confiner.BoundingShape2D);
                Assert.AreEqual(0.5f, confiner.Damping, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(boundsGo);
            }
        }

        [Test]
        public void AddExtension_Confiner2D_MissingBoundingShape_ReturnsFail()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });

            var result = Tools.Cinemachine.CinemachineAddExtensionTool.Execute(new Tools.Cinemachine.CinemachineAddExtensionParams
            {
                VCamName = "TestVCam", ExtensionType = "Confiner2D",
                ConfinerBoundingShapeName = "NoSuchBoundsObject",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void AddExtension_ImpulseListener_SetsChannelAndGain()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });

            var result = Tools.Cinemachine.CinemachineAddExtensionTool.Execute(new Tools.Cinemachine.CinemachineAddExtensionParams
            {
                VCamName = "TestVCam", ExtensionType = "ImpulseListener", ImpulseChannelMask = 2, ImpulseGain = 1.5f,
            });

            Assert.IsTrue(result.Success, result.Error);
            var listener = GameObject.Find("TestVCam").GetComponent<CinemachineImpulseListener>();
            Assert.IsNotNull(listener);
            Assert.AreEqual(2, listener.ChannelMask);
            Assert.AreEqual(1.5f, listener.Gain, 0.0001f);
        }

        [Test]
        public void AddExtension_CameraOffset_SetsOffset()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });

            var result = Tools.Cinemachine.CinemachineAddExtensionTool.Execute(new Tools.Cinemachine.CinemachineAddExtensionParams
            {
                VCamName = "TestVCam", ExtensionType = "CameraOffset", CameraOffsetValue = new float[] { 1, 2, 3 },
            });

            Assert.IsTrue(result.Success, result.Error);
            var offset = GameObject.Find("TestVCam").GetComponent<CinemachineCameraOffset>();
            Assert.IsNotNull(offset);
            Assert.AreEqual(new Vector3(1, 2, 3), offset.Offset);
        }

        [Test]
        public void AddExtension_Recomposer_SetsFields()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });

            var result = Tools.Cinemachine.CinemachineAddExtensionTool.Execute(new Tools.Cinemachine.CinemachineAddExtensionParams
            {
                VCamName = "TestVCam", ExtensionType = "Recomposer",
                RecomposerZoomScale = 2f, RecomposerTilt = 5f, RecomposerPan = -3f, RecomposerDutch = 1f,
            });

            Assert.IsTrue(result.Success, result.Error);
            var recomposer = GameObject.Find("TestVCam").GetComponent<CinemachineRecomposer>();
            Assert.IsNotNull(recomposer);
            Assert.AreEqual(2f, recomposer.ZoomScale, 0.0001f);
            Assert.AreEqual(5f, recomposer.Tilt, 0.0001f);
            Assert.AreEqual(-3f, recomposer.Pan, 0.0001f);
            Assert.AreEqual(1f, recomposer.Dutch, 0.0001f);
        }

        [Test]
        public void AddExtension_PixelPerfect_AddsComponent()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });

            var result = Tools.Cinemachine.CinemachineAddExtensionTool.Execute(new Tools.Cinemachine.CinemachineAddExtensionParams
            {
                VCamName = "TestVCam", ExtensionType = "PixelPerfect",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(GameObject.Find("TestVCam").GetComponent<CinemachinePixelPerfect>());
        }

        [Test]
        public void AddExtension_UnknownType_ReturnsInvalidParam()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(
                new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });

            var result = Tools.Cinemachine.CinemachineAddExtensionTool.Execute(new Tools.Cinemachine.CinemachineAddExtensionParams
            {
                VCamName = "TestVCam", ExtensionType = "Bogus",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void AddExtension_VCamNotFound_ReturnsNotFound()
        {
            var result = Tools.Cinemachine.CinemachineAddExtensionTool.Execute(new Tools.Cinemachine.CinemachineAddExtensionParams
            {
                VCamName = "NoSuchVCam", ExtensionType = "PixelPerfect",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // O4 §4.5 P2: cinemachine/impulse — "camera shake on landing" is asked for in nearly
        // every intro course.

        [Test]
        public void Impulse_AddSource_SetsShapeDurationAndChannel()
        {
            var target = new GameObject("TestVCam");
            try
            {
                var result = Tools.Cinemachine.CinemachineImpulseTool.Execute(new Tools.Cinemachine.CinemachineImpulseParams
                {
                    Action = "add-source", TargetName = "TestVCam",
                    ImpulseShape = "Explosion", ImpulseDuration = 0.75f, ImpulseChannel = 3,
                    DefaultVelocity = new float[] { 0, -1, 0 },
                });

                Assert.IsTrue(result.Success, result.Error);
                Assert.AreEqual("Explosion", result.Data.ImpulseShape);
                Assert.AreEqual(0.75f, result.Data.ImpulseDuration, 0.0001f);
                Assert.AreEqual(3, result.Data.ImpulseChannel);

                var source = target.GetComponent<CinemachineImpulseSource>();
                Assert.IsNotNull(source);
                Assert.AreEqual(new Vector3(0, -1, 0), source.DefaultVelocity);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Impulse_AddCollisionSource_SetsScalingFlags()
        {
            var target = new GameObject("TestVCam");
            try
            {
                var result = Tools.Cinemachine.CinemachineImpulseTool.Execute(new Tools.Cinemachine.CinemachineImpulseParams
                {
                    Action = "add-collision-source", TargetName = "TestVCam",
                    ScaleImpactWithSpeed = true, ScaleImpactWithMass = true, UseImpactDirection = false,
                });

                Assert.IsTrue(result.Success, result.Error);
                var source = target.GetComponent<CinemachineCollisionImpulseSource>();
                Assert.IsNotNull(source);
                Assert.IsTrue(source.ScaleImpactWithSpeed);
                Assert.IsTrue(source.ScaleImpactWithMass);
                Assert.IsFalse(source.UseImpactDirection);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Impulse_UnknownAction_ReturnsInvalidParam()
        {
            var target = new GameObject("TestVCam");
            try
            {
                var result = Tools.Cinemachine.CinemachineImpulseTool.Execute(new Tools.Cinemachine.CinemachineImpulseParams
                {
                    Action = "bogus", TargetName = "TestVCam",
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Impulse_TargetNotFound_ReturnsNotFound()
        {
            var result = Tools.Cinemachine.CinemachineImpulseTool.Execute(new Tools.Cinemachine.CinemachineImpulseParams
            {
                Action = "add-source", TargetName = "NoSuchTarget",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // O4 §4.5 P2: cinemachine/target-group — a camera that follows the group frames every
        // member automatically (e.g. via GroupFraming aim).

        [Test]
        public void TargetGroup_Create_AddMember_Info_RoundTrips()
        {
            var member1 = new GameObject("TargetGroupMember1");
            var member2 = new GameObject("TargetGroupMember2");
            try
            {
                var create = Tools.Cinemachine.CinemachineTargetGroupTool.Execute(new Tools.Cinemachine.CinemachineTargetGroupParams
                {
                    Action = "create", Name = "TestTargetGroup", PositionMode = "GroupCenter", RotationMode = "Manual",
                });
                Assert.IsTrue(create.Success, create.Error);
                Assert.AreEqual("GroupCenter", create.Data.PositionMode);
                Assert.AreEqual("Manual", create.Data.RotationMode);

                var add1 = Tools.Cinemachine.CinemachineTargetGroupTool.Execute(new Tools.Cinemachine.CinemachineTargetGroupParams
                {
                    Action = "add-member", Name = "TestTargetGroup", MemberName = "TargetGroupMember1", Weight = 1f, Radius = 2f,
                });
                Assert.IsTrue(add1.Success, add1.Error);
                Assert.AreEqual(1, add1.Data.MemberCount);

                var add2 = Tools.Cinemachine.CinemachineTargetGroupTool.Execute(new Tools.Cinemachine.CinemachineTargetGroupParams
                {
                    Action = "add-member", Name = "TestTargetGroup", MemberName = "TargetGroupMember2", Weight = 2f,
                });
                Assert.IsTrue(add2.Success, add2.Error);
                Assert.AreEqual(2, add2.Data.MemberCount);

                var info = Tools.Cinemachine.CinemachineTargetGroupTool.Execute(new Tools.Cinemachine.CinemachineTargetGroupParams
                {
                    Action = "info", Name = "TestTargetGroup",
                });
                Assert.IsTrue(info.Success, info.Error);
                Assert.AreEqual(2, info.Data.Members.Length);
                Assert.AreEqual("TargetGroupMember1", info.Data.Members[0].Name);
                Assert.AreEqual(2f, info.Data.Members[0].Radius, 0.0001f);

                var remove = Tools.Cinemachine.CinemachineTargetGroupTool.Execute(new Tools.Cinemachine.CinemachineTargetGroupParams
                {
                    Action = "remove-member", Name = "TestTargetGroup", MemberName = "TargetGroupMember1",
                });
                Assert.IsTrue(remove.Success, remove.Error);
                Assert.AreEqual(1, remove.Data.MemberCount);
            }
            finally
            {
                Object.DestroyImmediate(member1);
                Object.DestroyImmediate(member2);
                var groupGo = GameObject.Find("TestTargetGroup");
                if (groupGo != null) Object.DestroyImmediate(groupGo);
            }
        }

        [Test]
        public void TargetGroup_AddMember_GroupNotFound_ReturnsNotFound()
        {
            var result = Tools.Cinemachine.CinemachineTargetGroupTool.Execute(new Tools.Cinemachine.CinemachineTargetGroupParams
            {
                Action = "add-member", Name = "NoSuchGroup", MemberName = "Whatever",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void TargetGroup_RemoveMember_NotAMember_ReturnsNotFound()
        {
            Tools.Cinemachine.CinemachineTargetGroupTool.Execute(
                new Tools.Cinemachine.CinemachineTargetGroupParams { Action = "create", Name = "TestTargetGroup" });
            var nonMember = new GameObject("TargetGroupMember1");
            try
            {
                var result = Tools.Cinemachine.CinemachineTargetGroupTool.Execute(new Tools.Cinemachine.CinemachineTargetGroupParams
                {
                    Action = "remove-member", Name = "TestTargetGroup", MemberName = "TargetGroupMember1",
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual("NOT_FOUND", result.ErrorCode);
            }
            finally
            {
                Object.DestroyImmediate(nonMember);
                var groupGo = GameObject.Find("TestTargetGroup");
                if (groupGo != null) Object.DestroyImmediate(groupGo);
            }
        }

        // O4 §4.5 P2: custom blends + brain settings — the blend-rules lesson.

        [Test]
        public void CreateBrain_UpdateMethodAndChannelMask_Apply()
        {
            var result = Tools.Cinemachine.CinemachineCreateBrainTool.Execute(new Tools.Cinemachine.CinemachineCreateBrainParams
            {
                UpdateMethod = "FixedUpdate", ChannelMask = (int)OutputChannels.Channel02,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("FixedUpdate", result.Data.UpdateMethod);
            Assert.AreEqual((int)OutputChannels.Channel02, result.Data.ChannelMask);
            var brain = _mainCameraGo.GetComponent<CinemachineBrain>();
            Assert.AreEqual(CinemachineBrain.UpdateMethods.FixedUpdate, brain.UpdateMethod);
        }

        [Test]
        public void CreateBrain_WorldUpOverride_SetsTransform()
        {
            var worldUpGo = new GameObject("WorldUpOverrideTarget");
            try
            {
                var result = Tools.Cinemachine.CinemachineCreateBrainTool.Execute(new Tools.Cinemachine.CinemachineCreateBrainParams
                {
                    WorldUpOverrideName = "WorldUpOverrideTarget",
                });

                Assert.IsTrue(result.Success, result.Error);
                Assert.AreEqual("WorldUpOverrideTarget", result.Data.WorldUpOverrideName);
                var brain = _mainCameraGo.GetComponent<CinemachineBrain>();
                Assert.AreEqual(worldUpGo.transform, brain.WorldUpOverride);
            }
            finally
            {
                Object.DestroyImmediate(worldUpGo);
            }
        }

        [Test]
        public void CreateBrain_CustomBlends_CreatesAssetAndAppendsBlend()
        {
            const string assetPath = "Assets/TestCustomBlends.asset";
            try
            {
                var result = Tools.Cinemachine.CinemachineCreateBrainTool.Execute(new Tools.Cinemachine.CinemachineCreateBrainParams
                {
                    CustomBlendsAssetPath = assetPath,
                    CustomBlends = new[]
                    {
                        new Tools.Cinemachine.CinemachineCustomBlendInput
                        {
                            From = "**ANY CAMERA**", To = "CamB", BlendType = "Cut", BlendTime = 0f,
                        },
                    },
                });

                Assert.IsTrue(result.Success, result.Error);
                Assert.AreEqual(assetPath, result.Data.CustomBlendsAssetPath);
                Assert.AreEqual(1, result.Data.CustomBlendCount);

                var asset = AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(assetPath);
                Assert.IsNotNull(asset);
                Assert.AreEqual(1, asset.CustomBlends.Length);
                Assert.AreEqual("**ANY CAMERA**", asset.CustomBlends[0].From);
                Assert.AreEqual("CamB", asset.CustomBlends[0].To);
                Assert.AreEqual(CinemachineBlendDefinition.Styles.Cut, asset.CustomBlends[0].Blend.Style);

                var brain = _mainCameraGo.GetComponent<CinemachineBrain>();
                Assert.AreEqual(asset, brain.CustomBlends);
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void CreateBrain_CustomBlends_MissingAssetPath_ReturnsInvalidParam()
        {
            var result = Tools.Cinemachine.CinemachineCreateBrainTool.Execute(new Tools.Cinemachine.CinemachineCreateBrainParams
            {
                CustomBlends = new[] { new Tools.Cinemachine.CinemachineCustomBlendInput { From = "A", To = "B" } },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // O4 §4.5 P2: manager cameras — "camera follows the animator state"; automatic best shot.

        [Test]
        public void CreateManager_StateDriven_ReparentsAndSetsInstructions()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam2" });
            var animatedGo = new GameObject("StateDrivenAnimated");
            animatedGo.AddComponent<Animator>();

            try
            {
                var result = Tools.Cinemachine.CinemachineCreateManagerTool.Execute(new Tools.Cinemachine.CinemachineCreateManagerParams
                {
                    ManagerType = "StateDriven", Name = "TestManager",
                    ChildVCamNames = new[] { "TestVCam", "TestVCam2" },
                    AnimatedTargetName = "StateDrivenAnimated", LayerIndex = 0,
                    StateDrivenInstructions = new[]
                    {
                        new Tools.Cinemachine.CinemachineStateDrivenInstructionInput
                        {
                            StateName = "Base Layer.Idle", CameraIndex = 0, MinDuration = 0.5f,
                        },
                        new Tools.Cinemachine.CinemachineStateDrivenInstructionInput
                        {
                            StateName = "Base Layer.Run", CameraIndex = 1,
                        },
                    },
                });

                Assert.IsTrue(result.Success, result.Error);
                Assert.AreEqual(2, result.Data.ChildCount);
                Assert.AreEqual(2, result.Data.InstructionCount);

                var managerGo = GameObject.Find("TestManager");
                var stateDriven = managerGo.GetComponent<CinemachineStateDrivenCamera>();
                Assert.AreEqual(2, stateDriven.Instructions.Length);
                Assert.AreEqual(Animator.StringToHash("Base Layer.Idle"), stateDriven.Instructions[0].FullHash);
                Assert.AreEqual(GameObject.Find("TestVCam").GetComponent<CinemachineCamera>(), stateDriven.Instructions[0].Camera);
                Assert.AreEqual(GameObject.Find("TestVCam").transform.parent, managerGo.transform);
                Assert.AreEqual(GameObject.Find("TestVCam2").transform.parent, managerGo.transform);
            }
            finally
            {
                Object.DestroyImmediate(animatedGo);
                var managerGo = GameObject.Find("TestManager");
                if (managerGo != null) Object.DestroyImmediate(managerGo);
            }
        }

        [Test]
        public void CreateManager_Sequencer_SetsLoopAndInstructions()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam2" });

            try
            {
                var result = Tools.Cinemachine.CinemachineCreateManagerTool.Execute(new Tools.Cinemachine.CinemachineCreateManagerParams
                {
                    ManagerType = "Sequencer", Name = "TestManager",
                    ChildVCamNames = new[] { "TestVCam", "TestVCam2" }, Loop = true,
                    SequencerInstructions = new[]
                    {
                        new Tools.Cinemachine.CinemachineSequencerInstructionInput { CameraIndex = 0, Hold = 2f },
                        new Tools.Cinemachine.CinemachineSequencerInstructionInput { CameraIndex = 1, Hold = 3f, BlendType = "Cut" },
                    },
                });

                Assert.IsTrue(result.Success, result.Error);
                var managerGo = GameObject.Find("TestManager");
                var sequencer = managerGo.GetComponent<CinemachineSequencerCamera>();
                Assert.IsTrue(sequencer.Loop);
                Assert.AreEqual(2, sequencer.Instructions.Count);
                Assert.AreEqual(2f, sequencer.Instructions[0].Hold, 0.0001f);
                Assert.AreEqual(CinemachineBlendDefinition.Styles.Cut, sequencer.Instructions[1].Blend.Style);
            }
            finally
            {
                var managerGo = GameObject.Find("TestManager");
                if (managerGo != null) Object.DestroyImmediate(managerGo);
            }
        }

        [Test]
        public void CreateManager_ClearShot_SetsFields()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });

            try
            {
                var result = Tools.Cinemachine.CinemachineCreateManagerTool.Execute(new Tools.Cinemachine.CinemachineCreateManagerParams
                {
                    ManagerType = "ClearShot", Name = "TestManager", ChildVCamNames = new[] { "TestVCam" },
                    ActivateAfter = 1f, MinDuration = 2f, RandomizeChoice = true,
                });

                Assert.IsTrue(result.Success, result.Error);
                var managerGo = GameObject.Find("TestManager");
                var clearShot = managerGo.GetComponent<CinemachineClearShot>();
                Assert.AreEqual(1f, clearShot.ActivateAfter, 0.0001f);
                Assert.AreEqual(2f, clearShot.MinDuration, 0.0001f);
                Assert.IsTrue(clearShot.RandomizeChoice);
                Assert.AreEqual(managerGo.transform, GameObject.Find("TestVCam").transform.parent);
            }
            finally
            {
                var managerGo = GameObject.Find("TestManager");
                if (managerGo != null) Object.DestroyImmediate(managerGo);
            }
        }

        [Test]
        public void CreateManager_Mixing_ReparentsChildren()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });

            try
            {
                var result = Tools.Cinemachine.CinemachineCreateManagerTool.Execute(new Tools.Cinemachine.CinemachineCreateManagerParams
                {
                    ManagerType = "Mixing", Name = "TestManager", ChildVCamNames = new[] { "TestVCam" },
                });

                Assert.IsTrue(result.Success, result.Error);
                var managerGo = GameObject.Find("TestManager");
                Assert.IsNotNull(managerGo.GetComponent<CinemachineMixingCamera>());
                Assert.AreEqual(managerGo.transform, GameObject.Find("TestVCam").transform.parent);
            }
            finally
            {
                var managerGo = GameObject.Find("TestManager");
                if (managerGo != null) Object.DestroyImmediate(managerGo);
            }
        }

        [Test]
        public void CreateManager_ChildVCamNotFound_ReturnsNotFound()
        {
            var result = Tools.Cinemachine.CinemachineCreateManagerTool.Execute(new Tools.Cinemachine.CinemachineCreateManagerParams
            {
                ManagerType = "Mixing", Name = "TestManager", ChildVCamNames = new[] { "NoSuchVCam" },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
            Assert.IsNull(GameObject.Find("TestManager"), "a failed create must not leave a partial manager behind");
        }

        [Test]
        public void CreateManager_UnknownType_ReturnsInvalidParam()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });

            var result = Tools.Cinemachine.CinemachineCreateManagerTool.Execute(new Tools.Cinemachine.CinemachineCreateManagerParams
            {
                ManagerType = "Bogus", Name = "TestManager", ChildVCamNames = new[] { "TestVCam" },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            Assert.IsNull(GameObject.Find("TestManager"));
        }

#if MOSAIC_HAS_TIMELINE
        // O4 §4.5 P2: cinemachine/timeline-shot — the cinematics course's core deliverable, a shot list.

        [Test]
        public void TimelineShot_AddsClipAndWiresVirtualCamera()
        {
            const string timelinePath = "Assets/TestCinemachineShotTimeline.playable";
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "TestVCam" });
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = timelinePath });
            TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = timelinePath, TrackType = "Cinemachine", Name = "CMTrack",
            });
            var directorGo = new GameObject("TestShotDirector");
            var director = directorGo.AddComponent<PlayableDirector>();
            director.playableAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);

            try
            {
                var result = Tools.Cinemachine.CinemachineTimelineShotTool.Execute(new Tools.Cinemachine.CinemachineTimelineShotParams
                {
                    TimelineAssetPath = timelinePath, TrackIndex = 0,
                    DirectorInstanceId = directorGo.GetInstanceID(), VCamName = "TestVCam",
                    Start = 0, Duration = 3,
                });

                Assert.IsTrue(result.Success, result.Error);
                Assert.AreEqual(3, result.Data.Duration, 0.0001);
                Assert.IsFalse(result.Data.BrainFoundInScene);

                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
                var track = (CinemachineTrack)timeline.GetOutputTracks().First();
                var clip = track.GetClips().Single();
                var shot = (CinemachineShot)clip.asset;
                var value = director.GetReferenceValue(shot.VirtualCamera.exposedName, out var idValid);
                Assert.IsTrue(idValid);
                Assert.AreEqual(GameObject.Find("TestVCam").GetComponent<CinemachineCamera>(), value);
            }
            finally
            {
                Object.DestroyImmediate(directorGo);
                if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath) != null)
                    AssetDatabase.DeleteAsset(timelinePath);
            }
        }

        [Test]
        public void TimelineShot_NonCinemachineTrack_ReturnsInvalidParam()
        {
            const string timelinePath = "Assets/TestCinemachineShotTimeline2.playable";
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = timelinePath });
            TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = timelinePath, TrackType = "Activation", Name = "NotCM",
            });
            var directorGo = new GameObject("TestShotDirector");
            var director = directorGo.AddComponent<PlayableDirector>();
            director.playableAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);

            try
            {
                var result = Tools.Cinemachine.CinemachineTimelineShotTool.Execute(new Tools.Cinemachine.CinemachineTimelineShotParams
                {
                    TimelineAssetPath = timelinePath, TrackIndex = 0,
                    DirectorInstanceId = directorGo.GetInstanceID(), VCamName = "TestVCam",
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            }
            finally
            {
                Object.DestroyImmediate(directorGo);
                if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath) != null)
                    AssetDatabase.DeleteAsset(timelinePath);
            }
        }
#endif

        // O4 §4.5 P2: spline dolly params + cart — fly-through/rail camera; CameraPosition is
        // what a Timeline track keys for a dolly move.

        [Test]
        public void CreateDolly_SplineDollyFields_Apply()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "DollyVCam" });

            var result = Tools.Cinemachine.CinemachineCreateDollyTool.Execute(new Tools.Cinemachine.CinemachineCreateDollyParams
            {
                Name = "TestDollyTrack", Waypoints = new float[] { 0, 0, 0, 10, 0, 10 },
                VCamName = "DollyVCam", CameraPosition = 5f, PositionUnits = "Distance",
                SplineOffset = new[] { 1f, 2f, 0f }, CameraRotation = "FollowTarget",
                DampingEnabled = true, DampingPosition = new[] { 0.5f, 0.5f, 0.5f }, DampingAngular = 2f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(5f, result.Data.CameraPosition, 0.0001f);
            Assert.AreEqual("Distance", result.Data.PositionUnits);
            Assert.AreEqual("FollowTarget", result.Data.CameraRotation);

            var dolly = GameObject.Find("DollyVCam").GetComponent<CinemachineSplineDolly>();
            Assert.AreEqual(new Vector3(1, 2, 0), dolly.SplineOffset);
            Assert.IsTrue(dolly.Damping.Enabled);
            Assert.AreEqual(2f, dolly.Damping.Angular, 0.0001f);
            Assert.AreEqual(CinemachineSplineDolly.RotationMode.FollowTarget, dolly.CameraRotation);
        }

        [Test]
        public void CreateDolly_AutoDollyFixedSpeed_SetsMethod()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "DollyVCam" });

            var result = Tools.Cinemachine.CinemachineCreateDollyTool.Execute(new Tools.Cinemachine.CinemachineCreateDollyParams
            {
                Name = "TestDollyTrack", Waypoints = new float[] { 0, 0, 0, 10, 0, 10 },
                VCamName = "DollyVCam", AutoDolly = true, AutoDollyMethod = "FixedSpeed", AutoDollySpeed = 3f,
            });

            Assert.IsTrue(result.Success, result.Error);
            var dolly = GameObject.Find("DollyVCam").GetComponent<CinemachineSplineDolly>();
            Assert.IsTrue(dolly.AutomaticDolly.Enabled);
            var method = dolly.AutomaticDolly.Method as SplineAutoDolly.FixedSpeed;
            Assert.IsNotNull(method);
            Assert.AreEqual(3f, method.Speed, 0.0001f);
        }

        [Test]
        public void CreateDolly_UnknownAutoDollyMethod_ReturnsInvalidParam()
        {
            Tools.Cinemachine.CinemachineCreateVCamTool.Execute(new Tools.Cinemachine.CinemachineCreateVCamParams { Name = "DollyVCam" });

            var result = Tools.Cinemachine.CinemachineCreateDollyTool.Execute(new Tools.Cinemachine.CinemachineCreateDollyParams
            {
                Name = "TestDollyTrack", Waypoints = new float[] { 0, 0, 0, 10, 0, 10 },
                VCamName = "DollyVCam", AutoDolly = true, AutoDollyMethod = "Bogus",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void CreateDolly_AddCart_CreatesCartOnSpline()
        {
            var result = Tools.Cinemachine.CinemachineCreateDollyTool.Execute(new Tools.Cinemachine.CinemachineCreateDollyParams
            {
                Name = "TestDollyTrack", Waypoints = new float[] { 0, 0, 0, 10, 0, 10 },
                CartName = "TestCart", CartSplinePosition = 0.5f, CartPositionUnits = "Normalized",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("TestCart", result.Data.CartName);

            var cart = GameObject.Find("TestCart").GetComponent<CinemachineSplineCart>();
            Assert.IsNotNull(cart);
            Assert.AreEqual(0.5f, cart.SplinePosition, 0.0001f);
            Assert.AreEqual(UnityEngine.Splines.PathIndexUnit.Normalized, cart.PositionUnits);
            Assert.AreEqual(GameObject.Find("TestDollyTrack").GetComponent<UnityEngine.Splines.SplineContainer>(), cart.Spline);
        }
    }
}
#endif
