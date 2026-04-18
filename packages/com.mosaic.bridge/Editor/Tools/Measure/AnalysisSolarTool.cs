using System;
using System.Globalization;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// analysis/solar — computes sun position (elevation, azimuth, direction), sunrise,
    /// sunset, day-length and optional shadow analysis / visualization using a simplified
    /// NOAA solar-position algorithm. Story 33-7.
    /// </summary>
    public static class AnalysisSolarTool
    {
        [MosaicTool("analysis/solar",
                    "Computes sun position, sunrise/sunset, day length and optional shadow analysis for a given lat/lon/date/time. Optionally visualizes sun path.",
                    isReadOnly: false, category: "analysis", Context = ToolContext.Both)]
        public static ToolResult<AnalysisSolarResult> Execute(AnalysisSolarParams p)
        {
            p ??= new AnalysisSolarParams();

            // --- Validate / defaults ------------------------------------------------
            float latitude  = p.Latitude  ?? 40f;
            float longitude = p.Longitude ?? -74f;
            float timeOfDay = p.TimeOfDay ?? 12f;
            float timeZone  = p.TimeZone  ?? -5f;
            int   sampleCount = p.SampleCount ?? 24;

            if (latitude < -90f || latitude > 90f)
                return ToolResult<AnalysisSolarResult>.Fail(
                    $"Latitude must be in [-90, 90]; got {latitude}.", ErrorCodes.INVALID_PARAM);
            if (longitude < -180f || longitude > 180f)
                return ToolResult<AnalysisSolarResult>.Fail(
                    $"Longitude must be in [-180, 180]; got {longitude}.", ErrorCodes.INVALID_PARAM);
            if (timeOfDay < 0f || timeOfDay > 24f)
                return ToolResult<AnalysisSolarResult>.Fail(
                    $"TimeOfDay must be in [0, 24]; got {timeOfDay}.", ErrorCodes.INVALID_PARAM);
            if (timeZone < -12f || timeZone > 14f)
                return ToolResult<AnalysisSolarResult>.Fail(
                    $"TimeZone must be in [-12, 14]; got {timeZone}.", ErrorCodes.INVALID_PARAM);
            if (sampleCount < 2)
                return ToolResult<AnalysisSolarResult>.Fail(
                    $"SampleCount must be >= 2; got {sampleCount}.", ErrorCodes.INVALID_PARAM);

            // Date parsing
            DateTime date;
            var dateStr = string.IsNullOrWhiteSpace(p.Date)
                ? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : p.Date.Trim();
            if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
                                         CultureInfo.InvariantCulture,
                                         DateTimeStyles.None, out date))
            {
                return ToolResult<AnalysisSolarResult>.Fail(
                    $"Invalid Date '{dateStr}'; expected yyyy-MM-dd.", ErrorCodes.INVALID_PARAM);
            }

            // SceneNorth
            Vector3 sceneNorth;
            if (p.SceneNorth == null || p.SceneNorth.Length == 0)
            {
                sceneNorth = Vector3.forward; // +Z
            }
            else
            {
                if (p.SceneNorth.Length != 3)
                    return ToolResult<AnalysisSolarResult>.Fail(
                        "SceneNorth must have exactly 3 components (x, y, z).", ErrorCodes.INVALID_PARAM);
                sceneNorth = new Vector3(p.SceneNorth[0], p.SceneNorth[1], p.SceneNorth[2]);
                if (sceneNorth.sqrMagnitude < 1e-10f)
                    return ToolResult<AnalysisSolarResult>.Fail(
                        "SceneNorth cannot be the zero vector.", ErrorCodes.INVALID_PARAM);
                sceneNorth.Normalize();
            }

            var analysisType = string.IsNullOrWhiteSpace(p.AnalysisType)
                ? "current_position" : p.AnalysisType.Trim().ToLowerInvariant();
            switch (analysisType)
            {
                case "current_position":
                case "full_day_arc":
                case "year_arc":
                case "shadow_at_time":
                    break;
                default:
                    return ToolResult<AnalysisSolarResult>.Fail(
                        $"Invalid AnalysisType '{p.AnalysisType}'. Valid: current_position, full_day_arc, year_arc, shadow_at_time",
                        ErrorCodes.INVALID_PARAM);
            }

            // --- Solar position -----------------------------------------------------
            int dayOfYear = date.DayOfYear;
            ComputeSunPosition(latitude, longitude, timeZone, dayOfYear, timeOfDay,
                               out float elevationDeg, out float azimuthDeg);

            // Sunrise / sunset via hour-angle at horizon
            ComputeSunriseSunset(latitude, longitude, timeZone, dayOfYear,
                                 out float sunriseHours, out float sunsetHours,
                                 out float dayLength, out bool polarDay, out bool polarNight);

            string sunriseStr = polarDay || polarNight ? string.Empty : HoursToHHmm(sunriseHours);
            string sunsetStr  = polarDay || polarNight ? string.Empty : HoursToHHmm(sunsetHours);

            bool isDaytime = elevationDeg > 0f;

            // Sun direction in scene space (pointing FROM origin TOWARD the sun)
            Vector3 sunDir = SphericalToScene(elevationDeg, azimuthDeg, sceneNorth);

            // --- Shadow length ------------------------------------------------------
            float shadowLength = 0f;
            GameObject targetGO = null;
            if (!string.IsNullOrEmpty(p.TargetGameObject))
            {
                targetGO = GameObject.Find(p.TargetGameObject);
                if (targetGO == null)
                    return ToolResult<AnalysisSolarResult>.Fail(
                        $"TargetGameObject '{p.TargetGameObject}' not found in scene.",
                        ErrorCodes.NOT_FOUND);

                if (isDaytime)
                {
                    float height = EstimateHeight(targetGO);
                    float tanEl  = Mathf.Tan(elevationDeg * Mathf.Deg2Rad);
                    shadowLength = tanEl > 1e-6f ? height / tanEl : 0f;
                }
            }

            // --- Optional visual ----------------------------------------------------
            int annotationId = -1;
            if (p.CreateVisual ?? false)
            {
                var root = CreateVisual(analysisType, latitude, longitude, timeZone,
                                         dayOfYear, timeOfDay, sceneNorth,
                                         sunDir, elevationDeg, isDaytime,
                                         sunriseHours, sunsetHours, polarDay, polarNight,
                                         sampleCount, targetGO, shadowLength, date);
                if (root != null)
                    annotationId = root.GetInstanceID();
            }

            return ToolResult<AnalysisSolarResult>.Ok(new AnalysisSolarResult
            {
                SunDirection = new[] { sunDir.x, sunDir.y, sunDir.z },
                SunElevation = elevationDeg,
                SunAzimuth   = azimuthDeg,
                IsDaytime    = isDaytime,
                Sunrise      = sunriseStr,
                Sunset       = sunsetStr,
                DayLength    = dayLength,
                AnnotationId = annotationId,
                ShadowLength = shadowLength,
            });
        }

        // ====================================================================
        // Solar math (simplified NOAA)
        // ====================================================================

        static void ComputeSunPosition(float latitudeDeg, float longitudeDeg,
                                        float timeZoneHours, int dayOfYear,
                                        float localTimeHours,
                                        out float elevationDeg, out float azimuthDeg)
        {
            // Declination (degrees)
            float decDeg = 23.45f * Mathf.Sin(Mathf.Deg2Rad * (360f * (284 + dayOfYear) / 365f));
            float dec = decDeg * Mathf.Deg2Rad;
            float lat = latitudeDeg * Mathf.Deg2Rad;

            // Hour angle accounting for longitude vs. time zone standard meridian.
            // Standard meridian for the time zone is 15° * TZ. Longitude correction
            // converts local clock time to local solar time.
            float standardMeridian = 15f * timeZoneHours;
            float longitudeCorrectionHours = (longitudeDeg - standardMeridian) / 15f;
            float solarTime = localTimeHours + longitudeCorrectionHours;
            float hourAngleDeg = (solarTime - 12f) * 15f;
            float H = hourAngleDeg * Mathf.Deg2Rad;

            // Elevation
            float sinAlt = Mathf.Sin(lat) * Mathf.Sin(dec)
                          + Mathf.Cos(lat) * Mathf.Cos(dec) * Mathf.Cos(H);
            sinAlt = Mathf.Clamp(sinAlt, -1f, 1f);
            float altitude = Mathf.Asin(sinAlt);
            elevationDeg = altitude * Mathf.Rad2Deg;

            // Azimuth (measured from North, clockwise)
            float cosAz;
            float cosAlt = Mathf.Cos(altitude);
            if (Mathf.Abs(cosAlt) < 1e-6f)
            {
                // Sun at zenith/nadir — azimuth undefined; return 180 as a stable default.
                azimuthDeg = 180f;
                return;
            }
            cosAz = (Mathf.Sin(dec) - sinAlt * Mathf.Sin(lat)) / (cosAlt * Mathf.Cos(lat));
            cosAz = Mathf.Clamp(cosAz, -1f, 1f);
            float az = Mathf.Acos(cosAz) * Mathf.Rad2Deg; // 0..180, measured from North

            // Disambiguate by hour-angle sign: H<0 => morning => azimuth in east half (0..180)
            // H>0 => afternoon => azimuth in west half (180..360)
            if (hourAngleDeg > 0f)
                azimuthDeg = 360f - az;
            else
                azimuthDeg = az;

            // Normalize 0..360
            azimuthDeg = (azimuthDeg % 360f + 360f) % 360f;
        }

        static void ComputeSunriseSunset(float latitudeDeg, float longitudeDeg,
                                          float timeZoneHours, int dayOfYear,
                                          out float sunriseHours, out float sunsetHours,
                                          out float dayLengthHours,
                                          out bool polarDay, out bool polarNight)
        {
            float decDeg = 23.45f * Mathf.Sin(Mathf.Deg2Rad * (360f * (284 + dayOfYear) / 365f));
            float dec = decDeg * Mathf.Deg2Rad;
            float lat = latitudeDeg * Mathf.Deg2Rad;

            float cosH0 = -Mathf.Tan(lat) * Mathf.Tan(dec);

            if (cosH0 < -1f)
            {
                // Sun never sets
                polarDay = true;
                polarNight = false;
                sunriseHours = 0f;
                sunsetHours = 24f;
                dayLengthHours = 24f;
                return;
            }
            if (cosH0 > 1f)
            {
                // Sun never rises
                polarDay = false;
                polarNight = true;
                sunriseHours = 0f;
                sunsetHours = 0f;
                dayLengthHours = 0f;
                return;
            }

            polarDay = false;
            polarNight = false;
            float H0Deg = Mathf.Acos(cosH0) * Mathf.Rad2Deg;
            float halfDayHours = H0Deg / 15f;

            // Longitude correction — clock time vs. solar time
            float standardMeridian = 15f * timeZoneHours;
            float longitudeCorrectionHours = (longitudeDeg - standardMeridian) / 15f;

            float solarNoon = 12f - longitudeCorrectionHours;
            sunriseHours = solarNoon - halfDayHours;
            sunsetHours  = solarNoon + halfDayHours;
            dayLengthHours = 2f * halfDayHours;
        }

        static string HoursToHHmm(float hours)
        {
            // Wrap into [0, 24)
            float h = hours;
            while (h < 0f) h += 24f;
            while (h >= 24f) h -= 24f;
            int hh = Mathf.FloorToInt(h);
            int mm = Mathf.FloorToInt((h - hh) * 60f);
            if (mm >= 60) { mm -= 60; hh = (hh + 1) % 24; }
            return $"{hh:D2}:{mm:D2}";
        }

        // ====================================================================
        // Scene mapping
        // ====================================================================

        /// <summary>
        /// Converts (elevation, azimuth) to a scene-space unit vector, using
        /// <paramref name="sceneNorth"/> as the +North direction. Up is world +Y.
        /// Azimuth is measured from North, clockwise as seen from above (+Y).
        /// </summary>
        static Vector3 SphericalToScene(float elevationDeg, float azimuthDeg, Vector3 sceneNorth)
        {
            float el = elevationDeg * Mathf.Deg2Rad;
            float az = azimuthDeg  * Mathf.Deg2Rad;

            // Build an orthonormal basis: North (horizontal), East, Up.
            Vector3 up = Vector3.up;
            Vector3 north = sceneNorth - Vector3.Dot(sceneNorth, up) * up;
            if (north.sqrMagnitude < 1e-8f)
            {
                // Degenerate: SceneNorth parallel to up. Pick +Z projected.
                north = Vector3.forward;
            }
            north.Normalize();
            // East = up x north gives a right-handed axis where azimuth 0=N, 90=E (CW from above).
            Vector3 east = Vector3.Cross(up, north).normalized;

            float horizontal = Mathf.Cos(el);
            Vector3 dir = north * (horizontal * Mathf.Cos(az))
                        + east  * (horizontal * Mathf.Sin(az))
                        + up    * Mathf.Sin(el);
            return dir.normalized;
        }

        static float EstimateHeight(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds? b = null;
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (!b.HasValue) b = r.bounds;
                    else { var c = b.Value; c.Encapsulate(r.bounds); b = c; }
                }
                if (b.HasValue) return Mathf.Max(0f, b.Value.size.y);
            }
            // Fallback: use y position (distance from origin plane)
            return Mathf.Max(0f, go.transform.position.y);
        }

        // ====================================================================
        // Visualization
        // ====================================================================

        static GameObject CreateVisual(string analysisType, float latitude, float longitude,
                                        float timeZone, int dayOfYear, float timeOfDay,
                                        Vector3 sceneNorth, Vector3 sunDir,
                                        float elevationDeg, bool isDaytime,
                                        float sunriseHours, float sunsetHours,
                                        bool polarDay, bool polarNight, int sampleCount,
                                        GameObject target, float shadowLength, DateTime date)
        {
            string baseName = $"SolarAnalysis_{analysisType}_{DateTime.Now:HHmmss}";
            var root = new GameObject(baseName);

            const float sunDistance = 50f;

            // Always show a sun marker at the current position (if above horizon).
            if (isDaytime)
            {
                var sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sun.name = "SunMarker";
                var col = sun.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
                sun.transform.SetParent(root.transform, worldPositionStays: true);
                sun.transform.position = sunDir * sunDistance;
                sun.transform.localScale = Vector3.one * 2f;
                TrySetUnlitColor(sun, new Color(1f, 0.95f, 0.4f, 1f));

                // Directional light pointing from sun toward origin
                var lightGO = new GameObject("SunLight");
                lightGO.transform.SetParent(root.transform, worldPositionStays: true);
                lightGO.transform.position = sunDir * sunDistance;
                lightGO.transform.rotation = Quaternion.LookRotation(-sunDir);
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.97f, 0.85f, 1f);
                light.intensity = 1f;
            }

            // Arc visualization
            switch (analysisType)
            {
                case "full_day_arc":
                    if (!polarNight)
                        DrawDayArc(root, latitude, longitude, timeZone, dayOfYear,
                                   sunriseHours, sunsetHours, polarDay, sceneNorth,
                                   sampleCount, sunDistance);
                    break;
                case "year_arc":
                    DrawYearArc(root, latitude, longitude, timeZone, sceneNorth,
                                sampleCount, sunDistance);
                    break;
                case "shadow_at_time":
                    if (target != null && isDaytime)
                        DrawShadow(root, target, sunDir, shadowLength);
                    break;
                case "current_position":
                default:
                    break;
            }

            return root;
        }

        static void DrawDayArc(GameObject root, float latitude, float longitude,
                                float timeZone, int dayOfYear,
                                float sunriseHours, float sunsetHours, bool polarDay,
                                Vector3 sceneNorth, int sampleCount, float sunDistance)
        {
            float t0 = polarDay ? 0f : sunriseHours;
            float t1 = polarDay ? 24f : sunsetHours;

            var arcGO = new GameObject("DayArc");
            arcGO.transform.SetParent(root.transform, worldPositionStays: true);
            var line = arcGO.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.2f;
            line.positionCount = sampleCount;
            TrySetLineMaterial(line, new Color(1f, 0.7f, 0.1f, 1f));

            for (int i = 0; i < sampleCount; i++)
            {
                float frac = (float)i / (sampleCount - 1);
                float t = Mathf.Lerp(t0, t1, frac);
                ComputeSunPosition(latitude, longitude, timeZone, dayOfYear, t,
                                   out float el, out float az);
                var dir = SphericalToScene(el, az, sceneNorth);
                line.SetPosition(i, dir * sunDistance);
            }
        }

        static void DrawYearArc(GameObject root, float latitude, float longitude,
                                 float timeZone, Vector3 sceneNorth,
                                 int sampleCount, float sunDistance)
        {
            // Analemma at solar noon: one sample per "month slice" across the year.
            var arcGO = new GameObject("YearArc");
            arcGO.transform.SetParent(root.transform, worldPositionStays: true);
            var line = arcGO.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.2f;
            line.loop = true;
            line.positionCount = sampleCount;
            TrySetLineMaterial(line, new Color(0.3f, 0.7f, 1f, 1f));

            for (int i = 0; i < sampleCount; i++)
            {
                int doy = Mathf.RoundToInt(Mathf.Lerp(1f, 365f, (float)i / sampleCount));
                ComputeSunPosition(latitude, longitude, timeZone, doy, 12f,
                                   out float el, out float az);
                // Clamp to horizon for visualization
                if (el < 0f) el = 0f;
                var dir = SphericalToScene(el, az, sceneNorth);
                line.SetPosition(i, dir * sunDistance);
            }
        }

        static void DrawShadow(GameObject root, GameObject target, Vector3 sunDir, float shadowLength)
        {
            if (shadowLength <= 0f) return;
            var shadowGO = new GameObject("Shadow");
            shadowGO.transform.SetParent(root.transform, worldPositionStays: true);
            var line = shadowGO.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.1f;
            line.positionCount = 2;
            TrySetLineMaterial(line, new Color(0.1f, 0.1f, 0.1f, 1f));

            Vector3 basePos = target.transform.position;
            basePos.y = 0f; // project to ground plane
            // Shadow projects opposite the sun's horizontal component
            Vector3 horizontal = new Vector3(sunDir.x, 0f, sunDir.z);
            if (horizontal.sqrMagnitude < 1e-8f) return;
            Vector3 shadowDir = -horizontal.normalized;
            line.SetPosition(0, basePos);
            line.SetPosition(1, basePos + shadowDir * shadowLength);
        }

        static void TrySetUnlitColor(GameObject go, Color c)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return;
            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.color = c;
            mr.sharedMaterial = mat;
        }

        static void TrySetLineMaterial(LineRenderer line, Color c)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return;
            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, color = c };
            line.material = mat;
            line.startColor = c;
            line.endColor = c;
        }
    }
}
