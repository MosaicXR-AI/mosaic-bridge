#if MOSAIC_HAS_CINEMACHINE && MOSAIC_HAS_SPLINES
using UnityEngine;
using UnityEngine.Splines;
using UnityEditor;
using Unity.Cinemachine;
using Unity.Mathematics;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineCreateDollyTool
    {
        [MosaicTool("cinemachine/create-dolly",
                    "Creates a dolly track (SplineContainer) with waypoints. VCamName attaches a " +
                    "CinemachineSplineDolly: CameraPosition/PositionUnits, SplineOffset, " +
                    "CameraRotation, Damping*, and AutoDolly + AutoDollyMethod (FixedSpeed: " +
                    "AutoDollySpeed; NearestPointToTarget: AutoDollyPositionOffset, needs a Follow " +
                    "target). CartName additionally creates a CinemachineSplineCart riding the same " +
                    "spline — a non-camera rider (moving platform) distinct from the camera dolly.",
                    isReadOnly: false)]
        public static ToolResult<CinemachineCreateDollyResult> Execute(CinemachineCreateDollyParams p)
        {
            if (p.Waypoints == null || p.Waypoints.Length < 6 || p.Waypoints.Length % 3 != 0)
                return ToolResult<CinemachineCreateDollyResult>.Fail(
                    "Waypoints must be a flat array of floats with at least 2 waypoints (6 values): [x1,y1,z1, x2,y2,z2, ...]",
                    ErrorCodes.INVALID_PARAM);

            // Create the spline track GameObject
            var trackGo = new GameObject(p.Name);
            var splineContainer = trackGo.AddComponent<SplineContainer>();

            // Build the spline from waypoints
            var spline = splineContainer.Spline;
            spline.Clear();
            int waypointCount = p.Waypoints.Length / 3;
            for (int i = 0; i < waypointCount; i++)
            {
                var pos = new float3(
                    p.Waypoints[i * 3],
                    p.Waypoints[i * 3 + 1],
                    p.Waypoints[i * 3 + 2]);
                spline.Add(new BezierKnot(pos));
            }

            Undo.RegisterCreatedObjectUndo(trackGo, "Mosaic: Cinemachine Create Dolly Track");

            // Optionally attach to a virtual camera
            string attachedTo = null;
            if (!string.IsNullOrEmpty(p.VCamName))
            {
                var vcamGo = GameObject.Find(p.VCamName);
                if (vcamGo == null)
                {
                    // Don't destroy the track - it's still useful standalone
                    return ToolResult<CinemachineCreateDollyResult>.Fail(
                        $"VCam '{p.VCamName}' not found. Dolly track was created but not attached.",
                        ErrorCodes.NOT_FOUND);
                }

                var vcam = vcamGo.GetComponent<CinemachineCamera>();
                if (vcam == null)
                {
                    return ToolResult<CinemachineCreateDollyResult>.Fail(
                        $"GameObject '{p.VCamName}' does not have a CinemachineCamera component. Dolly track was created but not attached.",
                        ErrorCodes.INVALID_PARAM);
                }

                var dolly = vcamGo.GetComponent<CinemachineSplineDolly>();
                if (dolly == null)
                    dolly = Undo.AddComponent<CinemachineSplineDolly>(vcamGo);

                dolly.Spline = splineContainer;

                // PositionUnits must be set BEFORE CameraPosition — Cinemachine reinterprets the
                // existing CameraPosition value under the new units when PositionUnits changes, so
                // setting it after CameraPosition silently rescales the position the caller asked for.
                if (!string.IsNullOrEmpty(p.PositionUnits))
                {
                    if (!TryParsePathIndexUnit(p.PositionUnits, out var units))
                        return ToolResult<CinemachineCreateDollyResult>.Fail(
                            $"Unknown PositionUnits '{p.PositionUnits}'. Valid: Distance, Normalized, Knot", ErrorCodes.INVALID_PARAM);
                    dolly.PositionUnits = units;
                }

                if (p.CameraPosition.HasValue)
                    dolly.CameraPosition = p.CameraPosition.Value;

                if (p.SplineOffset != null)
                {
                    if (p.SplineOffset.Length != 3)
                        return ToolResult<CinemachineCreateDollyResult>.Fail(
                            "SplineOffset requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                    dolly.SplineOffset = new Vector3(p.SplineOffset[0], p.SplineOffset[1], p.SplineOffset[2]);
                }

                if (!string.IsNullOrEmpty(p.CameraRotation))
                {
                    if (!TryParseRotationMode(p.CameraRotation, out var rotationMode))
                        return ToolResult<CinemachineCreateDollyResult>.Fail(
                            $"Unknown CameraRotation '{p.CameraRotation}'. Valid: Default, FollowTarget, " +
                            "FollowTargetNoRoll, Spline, SplineNoRoll", ErrorCodes.INVALID_PARAM);
                    dolly.CameraRotation = rotationMode;
                }

                if (p.DampingEnabled.HasValue || p.DampingPosition != null || p.DampingAngular.HasValue)
                {
                    var damping = dolly.Damping;
                    if (p.DampingEnabled.HasValue) damping.Enabled = p.DampingEnabled.Value;
                    if (p.DampingPosition != null)
                    {
                        if (p.DampingPosition.Length != 3)
                            return ToolResult<CinemachineCreateDollyResult>.Fail(
                                "DampingPosition requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                        damping.Position = new Vector3(p.DampingPosition[0], p.DampingPosition[1], p.DampingPosition[2]);
                    }
                    if (p.DampingAngular.HasValue) damping.Angular = p.DampingAngular.Value;
                    dolly.Damping = damping;
                }

                if (p.AutoDolly)
                {
                    SplineAutoDolly.ISplineAutoDolly method = null;
                    if (!string.IsNullOrEmpty(p.AutoDollyMethod))
                    {
                        switch (p.AutoDollyMethod.ToLowerInvariant())
                        {
                            case "fixedspeed":
                                method = new SplineAutoDolly.FixedSpeed { Speed = p.AutoDollySpeed ?? 1f };
                                break;
                            case "nearestpointtotarget":
                                method = new SplineAutoDolly.NearestPointToTarget { PositionOffset = p.AutoDollyPositionOffset ?? 0f };
                                break;
                            default:
                                return ToolResult<CinemachineCreateDollyResult>.Fail(
                                    $"Unknown AutoDollyMethod '{p.AutoDollyMethod}'. Valid: FixedSpeed, NearestPointToTarget",
                                    ErrorCodes.INVALID_PARAM);
                        }
                    }
                    dolly.AutomaticDolly = new SplineAutoDolly { Enabled = true, Method = method };
                }

                attachedTo = vcamGo.name;
            }

            string cartName = null;
            int cartInstanceId = 0;
            if (!string.IsNullOrEmpty(p.CartName))
            {
                var cartGo = new GameObject(p.CartName);
                var cart = cartGo.AddComponent<CinemachineSplineCart>();
                cart.Spline = splineContainer;
                // Same ordering requirement as the dolly above: units before position.
                if (!string.IsNullOrEmpty(p.CartPositionUnits))
                {
                    if (!TryParsePathIndexUnit(p.CartPositionUnits, out var cartUnits))
                        return ToolResult<CinemachineCreateDollyResult>.Fail(
                            $"Unknown CartPositionUnits '{p.CartPositionUnits}'. Valid: Distance, Normalized, Knot",
                            ErrorCodes.INVALID_PARAM);
                    cart.PositionUnits = cartUnits;
                }
                if (p.CartSplinePosition.HasValue)
                    cart.SplinePosition = p.CartSplinePosition.Value;
                Undo.RegisterCreatedObjectUndo(cartGo, "Mosaic: Cinemachine Create Spline Cart");
                cartName = cartGo.name;
                cartInstanceId = UnityIds.Of(cartGo);
            }

            var vcamForResult = attachedTo != null ? GameObject.Find(attachedTo)?.GetComponent<CinemachineSplineDolly>() : null;

            return ToolResult<CinemachineCreateDollyResult>.Ok(new CinemachineCreateDollyResult
            {
                TrackInstanceId = UnityIds.Of(trackGo),
                TrackName = trackGo.name,
                WaypointCount = waypointCount,
                AutoDollyEnabled = p.AutoDolly,
                AttachedToVCam = attachedTo,
                CameraPosition = vcamForResult != null ? vcamForResult.CameraPosition : 0f,
                PositionUnits = vcamForResult != null ? vcamForResult.PositionUnits.ToString() : null,
                CameraRotation = vcamForResult != null ? vcamForResult.CameraRotation.ToString() : null,
                AutoDollyMethod = p.AutoDollyMethod,
                CartName = cartName,
                CartInstanceId = cartInstanceId,
            });
        }

        private static bool TryParsePathIndexUnit(string value, out PathIndexUnit result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "distance":   result = PathIndexUnit.Distance;   return true;
                case "normalized": result = PathIndexUnit.Normalized; return true;
                case "knot":       result = PathIndexUnit.Knot;       return true;
                default:           result = PathIndexUnit.Normalized; return false;
            }
        }

        private static bool TryParseRotationMode(string value, out CinemachineSplineDolly.RotationMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "default":            result = CinemachineSplineDolly.RotationMode.Default;            return true;
                case "followtarget":       result = CinemachineSplineDolly.RotationMode.FollowTarget;       return true;
                case "followtargetnoroll": result = CinemachineSplineDolly.RotationMode.FollowTargetNoRoll; return true;
                case "spline":             result = CinemachineSplineDolly.RotationMode.Spline;             return true;
                case "splinenoroll":       result = CinemachineSplineDolly.RotationMode.SplineNoRoll;       return true;
                default:                   result = CinemachineSplineDolly.RotationMode.Default;            return false;
            }
        }
    }
}
#endif
