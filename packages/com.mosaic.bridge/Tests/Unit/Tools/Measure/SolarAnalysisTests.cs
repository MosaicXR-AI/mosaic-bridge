using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Measure;

namespace Mosaic.Bridge.Tests.Unit.Tools.Measure
{
    /// <summary>
    /// Unit tests for the analysis/solar tool (Story 33-7).
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("Measure")]
    public class SolarAnalysisTests
    {
        [Test]
        public void NoonAtEquator_OnEquinox_SunNearOverhead()
        {
            // March equinox (day-of-year ~80) at equator, local solar noon.
            // With the simplified NOAA model the sun should be very high overhead.
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude  = 0f,
                Longitude = 0f,
                Date      = "2026-03-21",
                TimeOfDay = 12f,
                TimeZone  = 0f,
            });

            Assert.IsTrue(result.Success, result.Error);
            // Simplified declination model ~ 0.5° on this date at equator noon.
            Assert.GreaterOrEqual(result.Data.SunElevation, 85f,
                $"Expected elevation near 90°; got {result.Data.SunElevation}");
            Assert.LessOrEqual(result.Data.SunElevation, 90.01f);
            Assert.IsTrue(result.Data.IsDaytime);
        }

        [Test]
        public void Sunrise_Sunset_ReturnsValidTimes_AtTemperateLatitude()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude  = 40f,
                Longitude = -74f,
                Date      = "2026-06-21",
                TimeOfDay = 12f,
                TimeZone  = -5f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(string.IsNullOrEmpty(result.Data.Sunrise));
            Assert.IsFalse(string.IsNullOrEmpty(result.Data.Sunset));
            StringAssert.IsMatch(@"^\d{2}:\d{2}$", result.Data.Sunrise);
            StringAssert.IsMatch(@"^\d{2}:\d{2}$", result.Data.Sunset);
        }

        [Test]
        public void SummerSolstice_At45N_DayLongerThan12Hours()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude  = 45f,
                Longitude = 0f,
                Date      = "2026-06-21",
                TimeOfDay = 12f,
                TimeZone  = 0f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.Greater(result.Data.DayLength, 14f,
                $"Expected summer-solstice day length > 14h at 45°N; got {result.Data.DayLength}");
        }

        [Test]
        public void WinterSolstice_At45N_DayShorterThan12Hours()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude  = 45f,
                Longitude = 0f,
                Date      = "2026-12-21",
                TimeOfDay = 12f,
                TimeZone  = 0f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.Less(result.Data.DayLength, 10f,
                $"Expected winter-solstice day length < 10h at 45°N; got {result.Data.DayLength}");
            Assert.Greater(result.Data.DayLength, 0f);
        }

        [Test]
        public void SummerLongerThanWinter_At45N()
        {
            var summer = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude = 45f, Longitude = 0f, TimeZone = 0f,
                Date = "2026-06-21", TimeOfDay = 12f,
            });
            var winter = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude = 45f, Longitude = 0f, TimeZone = 0f,
                Date = "2026-12-21", TimeOfDay = 12f,
            });
            Assert.IsTrue(summer.Success);
            Assert.IsTrue(winter.Success);
            Assert.Greater(summer.Data.DayLength, winter.Data.DayLength);
        }

        [Test]
        public void InvalidLatitude_ReturnsError()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude = 100f,
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void InvalidLongitude_ReturnsError()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Longitude = 500f,
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void InvalidDateFormat_ReturnsError()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Date = "06/21/2026",
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void InvalidAnalysisType_ReturnsError()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                AnalysisType = "bogus",
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Night_Midnight_IsDaytimeFalse()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude  = 40f,
                Longitude = -74f,
                Date      = "2026-03-21",
                TimeOfDay = 0f,
                TimeZone  = -5f,
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(result.Data.IsDaytime);
            Assert.Less(result.Data.SunElevation, 0f);
        }

        [Test]
        public void SunDirection_IsNormalized()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude  = 40f,
                Longitude = -74f,
                Date      = "2026-06-21",
                TimeOfDay = 12f,
                TimeZone  = -5f,
            });
            Assert.IsTrue(result.Success, result.Error);
            var d = result.Data.SunDirection;
            var mag = Mathf.Sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]);
            Assert.AreEqual(1f, mag, 1e-3f);
        }

        [Test]
        public void Noon_AtTemperateLatitude_AzimuthNearSouth()
        {
            // Northern hemisphere noon — sun should be roughly due south (~180°).
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude  = 40f,
                Longitude = 0f,
                Date      = "2026-06-21",
                TimeOfDay = 12f,
                TimeZone  = 0f,
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(180f, result.Data.SunAzimuth, 5f,
                $"Expected azimuth near 180° (south) at NH noon; got {result.Data.SunAzimuth}");
        }

        [Test]
        public void ShadowLength_ComputedForTargetGameObject()
        {
            var go = new GameObject("__SolarTest_Target");
            try
            {
                go.transform.position = new Vector3(0f, 5f, 0f);

                var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
                {
                    Latitude  = 40f,
                    Longitude = 0f,
                    Date      = "2026-06-21",
                    TimeOfDay = 8f,      // low sun → long shadow
                    TimeZone  = 0f,
                    TargetGameObject = go.name,
                });
                Assert.IsTrue(result.Success, result.Error);
                Assert.IsTrue(result.Data.IsDaytime);
                Assert.Greater(result.Data.ShadowLength, 0f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MissingTargetGameObject_ReturnsError()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                TargetGameObject = "__nonexistent_solar_target__",
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void PolarNight_ReturnsZeroDayLength()
        {
            // High Arctic mid-winter — sun never rises.
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude  = 80f,
                Longitude = 0f,
                Date      = "2026-12-21",
                TimeOfDay = 12f,
                TimeZone  = 0f,
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0f, result.Data.DayLength, 1e-3f);
            Assert.IsFalse(result.Data.IsDaytime);
        }

        [Test]
        public void PolarDay_Returns24HourDayLength()
        {
            // High Arctic mid-summer — sun never sets.
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                Latitude  = 80f,
                Longitude = 0f,
                Date      = "2026-06-21",
                TimeOfDay = 0f,
                TimeZone  = 0f,
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(24f, result.Data.DayLength, 1e-3f);
            Assert.IsTrue(result.Data.IsDaytime);
        }

        [Test]
        public void DefaultParams_SucceedAndReturnCurrentPosition()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams());
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(-1, result.Data.AnnotationId);
            Assert.IsNotNull(result.Data.SunDirection);
            Assert.AreEqual(3, result.Data.SunDirection.Length);
        }

        [Test]
        public void InvalidSceneNorth_ReturnsError()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                SceneNorth = new[] { 0f, 0f },
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ZeroSceneNorth_ReturnsError()
        {
            var result = AnalysisSolarTool.Execute(new AnalysisSolarParams
            {
                SceneNorth = new[] { 0f, 0f, 0f },
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
