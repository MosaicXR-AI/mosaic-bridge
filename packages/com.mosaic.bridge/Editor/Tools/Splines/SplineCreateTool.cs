#if MOSAIC_HAS_SPLINES
using UnityEngine;
using UnityEngine.Splines;
using UnityEditor;
using Unity.Mathematics;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Splines
{
    public static class SplineCreateTool
    {
        [MosaicTool("spline/create",
                    "Create a SplineContainer with initial knots on a new GameObject",
                    isReadOnly: false,
                    category: "spline")]
        public static ToolResult<SplineCreateResult> Execute(SplineCreateParams p)
        {
            var go = new GameObject(p.Name);
            var container = go.AddComponent<SplineContainer>();
            var spline = container.Spline;

            if (p.Knots != null)
            {
                foreach (var knotData in p.Knots)
                {
                    if (knotData.Position == null || knotData.Position.Length < 3)
                    {
                        Object.DestroyImmediate(go);
                        return ToolResult<SplineCreateResult>.Fail(
                            "Each knot must have a Position array with at least 3 elements [x, y, z].",
                            ErrorCodes.INVALID_PARAM);
                    }

                    var position = new float3(knotData.Position[0], knotData.Position[1], knotData.Position[2]);

                    var rotation = quaternion.identity;
                    if (knotData.Rotation != null && knotData.Rotation.Length >= 4)
                        rotation = new quaternion(knotData.Rotation[0], knotData.Rotation[1],
                                                  knotData.Rotation[2], knotData.Rotation[3]);

                    var knot = new BezierKnot(position, default, default, rotation);

                    if (knotData.TangentIn != null && knotData.TangentIn.Length >= 3)
                        knot.TangentIn = new float3(knotData.TangentIn[0], knotData.TangentIn[1], knotData.TangentIn[2]);

                    if (knotData.TangentOut != null && knotData.TangentOut.Length >= 3)
                        knot.TangentOut = new float3(knotData.TangentOut[0], knotData.TangentOut[1], knotData.TangentOut[2]);

                    spline.Add(knot);
                }
            }

            spline.Closed = p.Closed;

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Spline Create");

            return ToolResult<SplineCreateResult>.Ok(new SplineCreateResult
            {
                InstanceId = go.GetInstanceID(),
                Name = go.name,
                HierarchyPath = SplineToolHelpers.GetHierarchyPath(go.transform),
                KnotCount = spline.Count,
                Closed = spline.Closed,
                Length = spline.GetLength()
            });
        }
    }
}
#endif
