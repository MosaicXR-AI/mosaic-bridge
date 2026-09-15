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
    }
}
#endif
