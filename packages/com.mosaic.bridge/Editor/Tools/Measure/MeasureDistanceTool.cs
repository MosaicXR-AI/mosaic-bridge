using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Measure
{
    public static class MeasureDistanceTool
    {
        [MosaicTool("measure/distance",
                    "Measures the 3D distance between two points or GameObjects, with optional scene annotation",
                    isReadOnly: false, category: "measure", Context = ToolContext.Both)]
        public static ToolResult<MeasureDistanceResult> Execute(MeasureDistanceParams p)
        {
            if (p == null)
                return ToolResult<MeasureDistanceResult>.Fail(
                    "Params are required", ErrorCodes.INVALID_PARAM);

            // ---- Resolve Point A ----
            Vector3 a;
            GameObject goA = null;
            if (p.PointA != null && p.PointA.Length == 3)
            {
                a = new Vector3(p.PointA[0], p.PointA[1], p.PointA[2]);
            }
            else if (!string.IsNullOrEmpty(p.GameObjectA))
            {
                goA = GameObject.Find(p.GameObjectA);
                if (goA == null)
                    return ToolResult<MeasureDistanceResult>.Fail(
                        $"GameObjectA '{p.GameObjectA}' not found", ErrorCodes.NOT_FOUND);
                a = goA.transform.position;
            }
            else
            {
                return ToolResult<MeasureDistanceResult>.Fail(
                    "Either PointA or GameObjectA is required", ErrorCodes.INVALID_PARAM);
            }

            // ---- Resolve Point B ----
            Vector3 b;
            GameObject goB = null;
            if (p.PointB != null && p.PointB.Length == 3)
            {
                b = new Vector3(p.PointB[0], p.PointB[1], p.PointB[2]);
            }
            else if (!string.IsNullOrEmpty(p.GameObjectB))
            {
                goB = GameObject.Find(p.GameObjectB);
                if (goB == null)
                    return ToolResult<MeasureDistanceResult>.Fail(
                        $"GameObjectB '{p.GameObjectB}' not found", ErrorCodes.NOT_FOUND);
                b = goB.transform.position;
            }
            else
            {
                return ToolResult<MeasureDistanceResult>.Fail(
                    "Either PointB or GameObjectB is required", ErrorCodes.INVALID_PARAM);
            }

            // ---- Mode ----
            string mode = string.IsNullOrEmpty(p.Mode) ? "point_to_point" : p.Mode.ToLowerInvariant();
            Vector3 fromPoint = a;
            Vector3 toPoint = b;

            switch (mode)
            {
                case "point_to_point":
                    // Use points as-is.
                    break;

                case "min_distance":
                {
                    // If GOs have colliders, use Physics.ClosestPoint from each toward the other.
                    var colA = goA != null ? goA.GetComponent<Collider>() : null;
                    var colB = goB != null ? goB.GetComponent<Collider>() : null;
                    if (colA != null)
                        fromPoint = colA.ClosestPoint(b);
                    if (colB != null)
                        toPoint = colB.ClosestPoint(fromPoint);
                    // Refine A against new B
                    if (colA != null)
                        fromPoint = colA.ClosestPoint(toPoint);
                    break;
                }

                case "surface_to_surface":
                {
                    // Use bounds centers then find closest point on each bounds toward the other.
                    Bounds? bA = GetBounds(goA, a);
                    Bounds? bB = GetBounds(goB, b);
                    if (bA.HasValue && bB.HasValue)
                    {
                        fromPoint = bA.Value.ClosestPoint(bB.Value.center);
                        toPoint = bB.Value.ClosestPoint(fromPoint);
                        fromPoint = bA.Value.ClosestPoint(toPoint);
                    }
                    else if (bA.HasValue)
                    {
                        fromPoint = bA.Value.ClosestPoint(b);
                        toPoint = b;
                    }
                    else if (bB.HasValue)
                    {
                        fromPoint = a;
                        toPoint = bB.Value.ClosestPoint(a);
                    }
                    break;
                }

                default:
                    return ToolResult<MeasureDistanceResult>.Fail(
                        $"Unknown mode '{p.Mode}'. Use point_to_point, min_distance, or surface_to_surface.",
                        ErrorCodes.INVALID_PARAM);
            }

            float metersDistance = Vector3.Distance(fromPoint, toPoint);

            // ---- Unit conversion ----
            string unit = string.IsNullOrEmpty(p.Unit) ? "meters" : p.Unit.ToLowerInvariant();
            float converted;
            switch (unit)
            {
                case "meters":       converted = metersDistance * 1.0f; break;
                case "centimeters":  converted = metersDistance * 100f; break;
                case "millimeters":  converted = metersDistance * 1000f; break;
                case "inches":       converted = metersDistance * 39.3701f; break;
                case "feet":         converted = metersDistance * 3.28084f; break;
                default:
                    return ToolResult<MeasureDistanceResult>.Fail(
                        $"Unknown unit '{p.Unit}'. Use meters, feet, inches, millimeters, or centimeters.",
                        ErrorCodes.INVALID_PARAM);
            }

            var result = new MeasureDistanceResult
            {
                Distance  = converted,
                Unit      = unit,
                FromPoint = new[] { fromPoint.x, fromPoint.y, fromPoint.z },
                ToPoint   = new[] { toPoint.x, toPoint.y, toPoint.z }
            };

            // ---- Optional visual annotation ----
            if (p.CreateVisual)
            {
                Color color = new Color(1f, 1f, 0f, 1f);
                if (p.VisualColor != null && p.VisualColor.Length == 4)
                    color = new Color(p.VisualColor[0], p.VisualColor[1], p.VisualColor[2], p.VisualColor[3]);

                string annotationName = string.IsNullOrEmpty(p.Name)
                    ? $"MeasureDistance_{System.DateTime.Now.Ticks}"
                    : p.Name;

                var annotation = new GameObject(annotationName);
                Undo.RegisterCreatedObjectUndo(annotation, "Mosaic: Measure Distance");

                var lr = annotation.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.SetPosition(0, fromPoint);
                lr.SetPosition(1, toPoint);
                lr.startWidth = p.LineWidth;
                lr.endWidth = p.LineWidth;
                lr.useWorldSpace = true;
                var mat = new Material(Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default"));
                mat.color = color;
                lr.sharedMaterial = mat;
                lr.startColor = color;
                lr.endColor = color;

                // Label
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(annotation.transform, false);
                Vector3 mid = (fromPoint + toPoint) * 0.5f;
                labelGo.transform.position = mid;
                var tm = labelGo.AddComponent<TextMesh>();
                tm.text = $"{converted:F3} {unit}";
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.characterSize = 0.1f;
                tm.fontSize = 64;
                tm.color = color;

                result.AnnotationId = annotation.GetInstanceID();
                result.AnnotationName = annotation.name;
            }

            return ToolResult<MeasureDistanceResult>.Ok(result);
        }

        private static Bounds? GetBounds(GameObject go, Vector3 fallback)
        {
            if (go == null) return null;
            var r = go.GetComponent<Renderer>();
            if (r != null) return r.bounds;
            var c = go.GetComponent<Collider>();
            if (c != null) return c.bounds;
            return null;
        }
    }
}
