#if MOSAIC_HAS_CINEMACHINE && MOSAIC_HAS_SPLINES
using UnityEngine;
using UnityEngine.Splines;
using UnityEditor;
using Unity.Cinemachine;
using Unity.Mathematics;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineCreateDollyTool
    {
        [MosaicTool("cinemachine/create-dolly",
                    "Creates a dolly track (SplineContainer) with waypoints and optionally attaches a CinemachineSplineDolly to a virtual camera",
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

                if (p.AutoDolly)
                {
                    dolly.AutomaticDolly = new SplineAutoDolly
                    {
                        Enabled = true
                    };
                }

                attachedTo = vcamGo.name;
            }

            return ToolResult<CinemachineCreateDollyResult>.Ok(new CinemachineCreateDollyResult
            {
                TrackInstanceId = trackGo.GetInstanceID(),
                TrackName = trackGo.name,
                WaypointCount = waypointCount,
                AutoDollyEnabled = p.AutoDolly,
                AttachedToVCam = attachedTo
            });
        }
    }
}
#endif
