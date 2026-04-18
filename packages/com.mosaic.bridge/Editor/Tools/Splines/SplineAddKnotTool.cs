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
    public static class SplineAddKnotTool
    {
        [MosaicTool("spline/add_knot",
                    "Add, edit, or remove knots on an existing SplineContainer",
                    isReadOnly: false,
                    category: "spline")]
        public static ToolResult<SplineAddKnotResult> Execute(SplineAddKnotParams p)
        {
            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<SplineAddKnotResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found.",
                    ErrorCodes.NOT_FOUND);

            var container = go.GetComponent<SplineContainer>();
            if (container == null)
                return ToolResult<SplineAddKnotResult>.Fail(
                    $"GameObject '{p.GameObjectName}' does not have a SplineContainer component.",
                    ErrorCodes.NOT_FOUND);

            var spline = container.Spline;
            Undo.RecordObject(container, "Mosaic: Spline Add Knot");

            var action = p.Action?.ToLowerInvariant();

            switch (action)
            {
                case "add":
                    return AddKnot(spline, container, p, go.name);

                case "edit":
                    return EditKnot(spline, container, p, go.name);

                case "remove":
                    return RemoveKnot(spline, container, p, go.name);

                default:
                    return ToolResult<SplineAddKnotResult>.Fail(
                        $"Invalid action '{p.Action}'. Valid: add, edit, remove.",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<SplineAddKnotResult> AddKnot(
            Spline spline, SplineContainer container, SplineAddKnotParams p, string goName)
        {
            if (p.KnotData == null || p.KnotData.Position == null || p.KnotData.Position.Length < 3)
                return ToolResult<SplineAddKnotResult>.Fail(
                    "KnotData with Position [x,y,z] is required for 'add' action.",
                    ErrorCodes.INVALID_PARAM);

            var knot = BuildKnot(p.KnotData);

            int insertIndex;
            if (p.Index >= 0 && p.Index < spline.Count)
            {
                spline.Insert(p.Index, knot);
                insertIndex = p.Index;
            }
            else
            {
                spline.Add(knot);
                insertIndex = spline.Count - 1;
            }

            EditorUtility.SetDirty(container);

            return ToolResult<SplineAddKnotResult>.Ok(new SplineAddKnotResult
            {
                GameObjectName = goName,
                Action = "add",
                Index = insertIndex,
                KnotCount = spline.Count,
                Length = spline.GetLength(),
                Closed = spline.Closed
            });
        }

        private static ToolResult<SplineAddKnotResult> EditKnot(
            Spline spline, SplineContainer container, SplineAddKnotParams p, string goName)
        {
            if (p.Index < 0 || p.Index >= spline.Count)
                return ToolResult<SplineAddKnotResult>.Fail(
                    $"Index {p.Index} is out of range. Spline has {spline.Count} knots.",
                    ErrorCodes.OUT_OF_RANGE);

            if (p.KnotData == null || p.KnotData.Position == null || p.KnotData.Position.Length < 3)
                return ToolResult<SplineAddKnotResult>.Fail(
                    "KnotData with Position [x,y,z] is required for 'edit' action.",
                    ErrorCodes.INVALID_PARAM);

            spline[p.Index] = BuildKnot(p.KnotData);
            EditorUtility.SetDirty(container);

            return ToolResult<SplineAddKnotResult>.Ok(new SplineAddKnotResult
            {
                GameObjectName = goName,
                Action = "edit",
                Index = p.Index,
                KnotCount = spline.Count,
                Length = spline.GetLength(),
                Closed = spline.Closed
            });
        }

        private static ToolResult<SplineAddKnotResult> RemoveKnot(
            Spline spline, SplineContainer container, SplineAddKnotParams p, string goName)
        {
            if (p.Index < 0 || p.Index >= spline.Count)
                return ToolResult<SplineAddKnotResult>.Fail(
                    $"Index {p.Index} is out of range. Spline has {spline.Count} knots.",
                    ErrorCodes.OUT_OF_RANGE);

            spline.RemoveAt(p.Index);
            EditorUtility.SetDirty(container);

            return ToolResult<SplineAddKnotResult>.Ok(new SplineAddKnotResult
            {
                GameObjectName = goName,
                Action = "remove",
                Index = p.Index,
                KnotCount = spline.Count,
                Length = spline.GetLength(),
                Closed = spline.Closed
            });
        }

        private static BezierKnot BuildKnot(SplineKnotData data)
        {
            var position = new float3(data.Position[0], data.Position[1], data.Position[2]);

            var rotation = quaternion.identity;
            if (data.Rotation != null && data.Rotation.Length >= 4)
                rotation = new quaternion(data.Rotation[0], data.Rotation[1],
                                          data.Rotation[2], data.Rotation[3]);

            var knot = new BezierKnot(position, default, default, rotation);

            if (data.TangentIn != null && data.TangentIn.Length >= 3)
                knot.TangentIn = new float3(data.TangentIn[0], data.TangentIn[1], data.TangentIn[2]);

            if (data.TangentOut != null && data.TangentOut.Length >= 3)
                knot.TangentOut = new float3(data.TangentOut[0], data.TangentOut[1], data.TangentOut[2]);

            return knot;
        }
    }
}
#endif
