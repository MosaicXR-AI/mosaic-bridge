#if MOSAIC_HAS_SPLINES
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Splines
{
    public static class SplineInfoTool
    {
        [MosaicTool("spline/info",
                    "Query spline data including knot count, length, closed status, and knot positions for SplineContainers in the scene",
                    isReadOnly: true,
                    category: "spline")]
        public static ToolResult<SplineInfoResult> Execute(SplineInfoParams p)
        {
            var allContainers = Object.FindObjectsByType<SplineContainer>(FindObjectsSortMode.None);
            var infos = new List<SplineContainerInfo>();

            foreach (var container in allContainers)
            {
                if (!string.IsNullOrEmpty(p.GameObjectName) && container.gameObject.name != p.GameObjectName)
                    continue;

                var spline = container.Spline;
                var knotInfos = new List<SplineKnotInfo>();

                for (int i = 0; i < spline.Count; i++)
                {
                    var knot = spline[i];
                    knotInfos.Add(new SplineKnotInfo
                    {
                        Index = i,
                        Position = new[] { knot.Position.x, knot.Position.y, knot.Position.z },
                        Rotation = new[] { knot.Rotation.value.x, knot.Rotation.value.y,
                                           knot.Rotation.value.z, knot.Rotation.value.w },
                        TangentIn = new[] { knot.TangentIn.x, knot.TangentIn.y, knot.TangentIn.z },
                        TangentOut = new[] { knot.TangentOut.x, knot.TangentOut.y, knot.TangentOut.z }
                    });
                }

                infos.Add(new SplineContainerInfo
                {
                    InstanceId = container.gameObject.GetInstanceID(),
                    Name = container.gameObject.name,
                    HierarchyPath = SplineToolHelpers.GetHierarchyPath(container.transform),
                    KnotCount = spline.Count,
                    Length = spline.GetLength(),
                    Closed = spline.Closed,
                    Knots = knotInfos.ToArray()
                });
            }

            return ToolResult<SplineInfoResult>.Ok(new SplineInfoResult
            {
                IsReadOnly = true,
                Splines = infos.ToArray()
            });
        }
    }
}
#endif
