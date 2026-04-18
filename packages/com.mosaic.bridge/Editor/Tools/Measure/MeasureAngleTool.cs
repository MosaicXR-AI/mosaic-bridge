using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// measure/angle — computes the angle between two rays sharing a common vertex.
    /// Points can be supplied as explicit world coordinates or GameObject names.
    /// Optionally creates an arc + TextMesh label annotation visualising the angle.
    /// Story 33-2.
    /// </summary>
    public static class MeasureAngleTool
    {
        const int ArcSegments = 32;

        [MosaicTool("measure/angle",
                    "Computes the angle between two rays sharing a common vertex (apex). Optionally creates an arc + label annotation.",
                    isReadOnly: false, category: "measure", Context = ToolContext.Both)]
        public static ToolResult<MeasureAngleResult> Execute(MeasureAngleParams p)
        {
            p ??= new MeasureAngleParams();

            var unit = string.IsNullOrEmpty(p.Unit) ? "degrees" : p.Unit.ToLowerInvariant();
            if (unit != "degrees" && unit != "radians")
                return ToolResult<MeasureAngleResult>.Fail(
                    $"Invalid Unit '{p.Unit}'. Valid: degrees, radians", ErrorCodes.INVALID_PARAM);

            // --- Resolve the three points ---------------------------------------
            if (!TryResolvePoint(p.VertexPoint, p.VertexGameObject, "vertex",
                                  out var vertex, out var err))
                return ToolResult<MeasureAngleResult>.Fail(err.message, err.code);

            if (!TryResolvePoint(p.PointA, p.GameObjectA, "A", out var pointA, out err))
                return ToolResult<MeasureAngleResult>.Fail(err.message, err.code);

            if (!TryResolvePoint(p.PointB, p.GameObjectB, "B", out var pointB, out err))
                return ToolResult<MeasureAngleResult>.Fail(err.message, err.code);

            // --- Compute rays ---------------------------------------------------
            var vA = pointA - vertex;
            var vB = pointB - vertex;
            if (vA.sqrMagnitude < 1e-12f)
                return ToolResult<MeasureAngleResult>.Fail(
                    "Point A coincides with the vertex; cannot form a ray.", ErrorCodes.INVALID_PARAM);
            if (vB.sqrMagnitude < 1e-12f)
                return ToolResult<MeasureAngleResult>.Fail(
                    "Point B coincides with the vertex; cannot form a ray.", ErrorCodes.INVALID_PARAM);

            var rayA = vA.normalized;
            var rayB = vB.normalized;

            // --- Compute angle --------------------------------------------------
            var dot = Mathf.Clamp(Vector3.Dot(rayA, rayB), -1f, 1f);
            var angleRad = Mathf.Acos(dot);
            var angleOut = unit == "radians" ? angleRad : angleRad * Mathf.Rad2Deg;

            // --- Optional visual ------------------------------------------------
            int annotationId = -1;
            string annotationName = null;
            if (p.CreateVisual ?? false)
            {
                var color = p.VisualColor ?? new[] { 1f, 1f, 0f, 1f };
                if (color.Length != 4)
                    return ToolResult<MeasureAngleResult>.Fail(
                        "VisualColor must have exactly 4 elements (RGBA)", ErrorCodes.INVALID_PARAM);
                var arcRadius = p.ArcRadius ?? 0.5f;
                if (arcRadius <= 0f)
                    return ToolResult<MeasureAngleResult>.Fail(
                        "ArcRadius must be > 0", ErrorCodes.INVALID_PARAM);

                var baseName = string.IsNullOrEmpty(p.Name) ? "MeasureAngle" : p.Name;
                var go = CreateVisual(baseName, vertex, rayA, rayB, angleRad, arcRadius,
                                      new Color(color[0], color[1], color[2], color[3]),
                                      angleOut, unit);
                annotationId = go.GetInstanceID();
                annotationName = go.name;
            }

            return ToolResult<MeasureAngleResult>.Ok(new MeasureAngleResult
            {
                Angle          = angleOut,
                Unit           = unit,
                Vertex         = new[] { vertex.x, vertex.y, vertex.z },
                RayA           = new[] { rayA.x, rayA.y, rayA.z },
                RayB           = new[] { rayB.x, rayB.y, rayB.z },
                AnnotationId   = annotationId,
                AnnotationName = annotationName,
            });
        }

        // --------------------------------------------------------------------
        static bool TryResolvePoint(float[] coords, string goName, string label,
                                     out Vector3 point,
                                     out (string message, string code) error)
        {
            point = default;
            error = default;

            if (coords != null && coords.Length > 0)
            {
                if (coords.Length != 3)
                {
                    error = ($"Point '{label}' must have exactly 3 components (x, y, z)",
                             ErrorCodes.INVALID_PARAM);
                    return false;
                }
                point = new Vector3(coords[0], coords[1], coords[2]);
                return true;
            }

            if (!string.IsNullOrEmpty(goName))
            {
                var go = GameObject.Find(goName);
                if (go == null)
                {
                    error = ($"GameObject '{goName}' (point '{label}') not found in scene.",
                             ErrorCodes.NOT_FOUND);
                    return false;
                }
                point = go.transform.position;
                return true;
            }

            error = ($"Point '{label}' requires either explicit coordinates or a GameObject name.",
                     ErrorCodes.INVALID_PARAM);
            return false;
        }

        // --------------------------------------------------------------------
        static GameObject CreateVisual(string baseName, Vector3 vertex,
                                       Vector3 rayA, Vector3 rayB,
                                       float angleRad, float radius, Color color,
                                       float displayAngle, string unit)
        {
            var root = new GameObject(baseName);
            root.transform.position = vertex;

            // --- Arc LineRenderer -------------------------------------------------
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.01f;
            line.positionCount = ArcSegments + 1;
            var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"))
            {
                color = color,
            };
            line.material = mat;
            line.startColor = color;
            line.endColor = color;

            // Rotate from rayA toward rayB across the measured angle, sampling the plane
            // spanned by (rayA, rayB).
            var axis = Vector3.Cross(rayA, rayB);
            // Collinear rays: pick any perpendicular axis (angle is 0 or PI).
            if (axis.sqrMagnitude < 1e-10f)
                axis = Vector3.Cross(rayA, Mathf.Abs(Vector3.Dot(rayA, Vector3.up)) < 0.9f
                                            ? Vector3.up : Vector3.right);
            axis.Normalize();

            for (int i = 0; i <= ArcSegments; i++)
            {
                float t = (float)i / ArcSegments;
                var rot = Quaternion.AngleAxis(angleRad * Mathf.Rad2Deg * t, axis);
                var dir = rot * rayA;
                line.SetPosition(i, vertex + dir * radius);
            }

            // --- Label at arc midpoint -------------------------------------------
            var midRot = Quaternion.AngleAxis(angleRad * Mathf.Rad2Deg * 0.5f, axis);
            var midDir = midRot * rayA;
            var labelPos = vertex + midDir * (radius * 1.15f);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(root.transform, worldPositionStays: true);
            labelGO.transform.position = labelPos;
            var text = labelGO.AddComponent<TextMesh>();
            text.text = unit == "radians"
                ? displayAngle.ToString("0.####") + " rad"
                : displayAngle.ToString("0.##") + "°";
            text.characterSize = 0.1f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = color;

            return root;
        }
    }
}
