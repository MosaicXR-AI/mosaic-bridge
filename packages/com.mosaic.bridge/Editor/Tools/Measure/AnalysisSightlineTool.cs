using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// analysis/sightline — sight-line, viewshed, and cone-of-vision analysis.
    /// Casts rays from an observer to targets (or hemisphere / cone) and reports
    /// visibility. Story 33-8.
    /// </summary>
    public static class AnalysisSightlineTool
    {
        // Small epsilon (meters) used when deciding whether a raycast hit coincides
        // with the intended target position.
        const float TargetHitEpsilon = 0.05f;

        [MosaicTool("analysis/sightline",
                    "Sight-line / viewshed / cone-of-vision analysis. Casts rays from an observer and reports visible vs blocked hits.",
                    isReadOnly: false, category: "analysis", Context = ToolContext.Both)]
        public static ToolResult<AnalysisSightlineResult> Execute(AnalysisSightlineParams p)
        {
            p ??= new AnalysisSightlineParams();

            // --- Resolve viewer position ---
            Vector3 viewer;
            if (p.ViewerPosition != null && p.ViewerPosition.Length == 3)
            {
                viewer = new Vector3(p.ViewerPosition[0], p.ViewerPosition[1], p.ViewerPosition[2]);
            }
            else if (!string.IsNullOrEmpty(p.ViewerGameObject))
            {
                var go = GameObject.Find(p.ViewerGameObject);
                if (go == null)
                    return ToolResult<AnalysisSightlineResult>.Fail(
                        $"ViewerGameObject '{p.ViewerGameObject}' not found", ErrorCodes.NOT_FOUND);
                viewer = go.transform.position;
            }
            else
            {
                return ToolResult<AnalysisSightlineResult>.Fail(
                    "ViewerPosition (float[3]) or ViewerGameObject is required",
                    ErrorCodes.INVALID_PARAM);
            }

            if (p.MaxDistance <= 0f)
                return ToolResult<AnalysisSightlineResult>.Fail(
                    "MaxDistance must be > 0", ErrorCodes.INVALID_PARAM);

            string mode = string.IsNullOrEmpty(p.Mode) ? "sightline" : p.Mode.ToLowerInvariant();
            var visibleColor = ResolveColor(p.VisibleColor, new Color(0f, 1f, 0f, 1f));
            var blockedColor = ResolveColor(p.BlockedColor, new Color(1f, 0f, 0f, 1f));

            var result = new AnalysisSightlineResult { Mode = mode };
            var rays = new List<(Vector3 from, Vector3 to, bool visible)>();

            switch (mode)
            {
                case "sightline":
                    RunSightline(viewer, p, result, rays);
                    break;

                case "viewshed":
                    RunViewshed(viewer, p, result, rays);
                    break;

                case "cone":
                    if (!RunCone(viewer, p, result, rays, out var coneErr))
                        return ToolResult<AnalysisSightlineResult>.Fail(coneErr.message, coneErr.code);
                    break;

                default:
                    return ToolResult<AnalysisSightlineResult>.Fail(
                        $"Unknown mode '{p.Mode}'. Use sightline, viewshed, or cone.",
                        ErrorCodes.INVALID_PARAM);
            }

            // --- Optional visual ---
            if (p.CreateVisual && rays.Count > 0)
            {
                var root = new GameObject($"AnalysisSightline_{System.DateTime.Now.Ticks}");
                Undo.RegisterCreatedObjectUndo(root, "Mosaic: Analysis Sightline");
                root.transform.position = viewer;

                for (int i = 0; i < rays.Count; i++)
                {
                    var child = new GameObject($"Ray_{i}");
                    child.transform.SetParent(root.transform, worldPositionStays: true);
                    var lr = child.AddComponent<LineRenderer>();
                    lr.useWorldSpace = true;
                    lr.positionCount = 2;
                    lr.SetPosition(0, rays[i].from);
                    lr.SetPosition(1, rays[i].to);
                    lr.startWidth = 0.01f;
                    lr.endWidth = 0.01f;
                    var c = rays[i].visible ? visibleColor : blockedColor;
                    var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/Internal-Colored"))
                    {
                        color = c,
                    };
                    lr.sharedMaterial = mat;
                    lr.startColor = c;
                    lr.endColor = c;
                }

                result.AnnotationId = root.GetInstanceID();
            }

            return ToolResult<AnalysisSightlineResult>.Ok(result);
        }

        // ------------------------------------------------------------------
        // Sightline mode
        // ------------------------------------------------------------------
        static void RunSightline(Vector3 viewer, AnalysisSightlineParams p,
                                  AnalysisSightlineResult result,
                                  List<(Vector3, Vector3, bool)> rays)
        {
            var targets = new List<(Vector3 pos, string label)>();

            if (p.Targets != null)
            {
                for (int i = 0; i < p.Targets.Length; i++)
                {
                    var t = p.Targets[i];
                    if (t == null || t.Length != 3) continue;
                    targets.Add((new Vector3(t[0], t[1], t[2]), null));
                }
            }

            if (p.TargetGameObjects != null)
            {
                for (int i = 0; i < p.TargetGameObjects.Length; i++)
                {
                    var name = p.TargetGameObjects[i];
                    if (string.IsNullOrEmpty(name)) continue;
                    var go = GameObject.Find(name);
                    if (go == null) continue;
                    targets.Add((go.transform.position, name));
                }
            }

            result.TotalTargets = targets.Count;
            foreach (var (pos, _) in targets)
            {
                var hit = TestVisibility(viewer, pos, p.MaxDistance, p.LayerMask,
                                          out var isVisible, out var distance, out var blockedBy);
                result.Results.Add(new SightlineHit
                {
                    TargetPosition = new[] { pos.x, pos.y, pos.z },
                    IsVisible = isVisible,
                    Distance = distance,
                    BlockedBy = blockedBy,
                });
                if (isVisible) result.VisibleCount++; else result.BlockedCount++;

                var endPoint = hit ? new Vector3() : pos;
                // ray visualization: from viewer to actual endpoint (hit point if blocked, target if visible)
                var end = isVisible ? pos : (hit ? PointAlong(viewer, pos, distance) : pos);
                rays.Add((viewer, end, isVisible));
            }
        }

        // ------------------------------------------------------------------
        // Viewshed mode (hemispherical Fibonacci distribution, upper hemisphere)
        // ------------------------------------------------------------------
        static void RunViewshed(Vector3 viewer, AnalysisSightlineParams p,
                                 AnalysisSightlineResult result,
                                 List<(Vector3, Vector3, bool)> rays)
        {
            int n = Mathf.Max(1, p.ViewshedResolution);
            int clear = 0;
            int total = 0;

            // Fibonacci sphere, filter y >= 0 (upper hemisphere).
            // Generate 2N samples so that after filtering we approximate N hemisphere rays.
            int samples = n * 2;
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < samples; i++)
            {
                float y = 1f - (i / (float)(samples - 1)) * 2f; // 1 .. -1
                if (y < 0f) continue; // upper hemisphere only
                float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float theta = goldenAngle * i;
                float x = Mathf.Cos(theta) * radius;
                float z = Mathf.Sin(theta) * radius;
                var dir = new Vector3(x, y, z).normalized;

                bool blocked = UnityEngine.Physics.Raycast(viewer, dir, out var hitInfo,
                                                           p.MaxDistance, p.LayerMask);
                bool visible = !blocked;
                total++;
                if (visible) clear++;

                var endPoint = blocked ? hitInfo.point : viewer + dir * p.MaxDistance;
                var tPos = endPoint;
                result.Results.Add(new SightlineHit
                {
                    TargetPosition = new[] { tPos.x, tPos.y, tPos.z },
                    IsVisible = visible,
                    Distance = blocked ? hitInfo.distance : p.MaxDistance,
                    BlockedBy = blocked && hitInfo.collider != null ? hitInfo.collider.gameObject.name : null,
                });
                rays.Add((viewer, endPoint, visible));
            }

            result.TotalTargets = total;
            result.VisibleCount = clear;
            result.BlockedCount = total - clear;
            result.ViewshedPercent = total > 0 ? (clear / (float)total) * 100f : 0f;
        }

        // ------------------------------------------------------------------
        // Cone mode — stratified sampling within a cone around LookDirection
        // ------------------------------------------------------------------
        static bool RunCone(Vector3 viewer, AnalysisSightlineParams p,
                             AnalysisSightlineResult result,
                             List<(Vector3, Vector3, bool)> rays,
                             out (string message, string code) err)
        {
            err = default;

            if (p.FieldOfView <= 0f || p.FieldOfView > 180f)
            {
                err = ($"FieldOfView must be in (0, 180]. Got {p.FieldOfView}", ErrorCodes.INVALID_PARAM);
                return false;
            }

            Vector3 look;
            if (p.LookDirection != null && p.LookDirection.Length == 3)
                look = new Vector3(p.LookDirection[0], p.LookDirection[1], p.LookDirection[2]);
            else
                look = new Vector3(0f, 0f, 1f);

            if (look.sqrMagnitude < 1e-10f)
            {
                err = ("LookDirection is zero-length.", ErrorCodes.INVALID_PARAM);
                return false;
            }
            look.Normalize();

            // If explicit targets are provided, test only those inside the cone.
            bool hasExplicitTargets = (p.Targets != null && p.Targets.Length > 0) ||
                                      (p.TargetGameObjects != null && p.TargetGameObjects.Length > 0);

            if (hasExplicitTargets)
            {
                var targets = new List<Vector3>();
                if (p.Targets != null)
                    foreach (var t in p.Targets)
                        if (t != null && t.Length == 3)
                            targets.Add(new Vector3(t[0], t[1], t[2]));
                if (p.TargetGameObjects != null)
                    foreach (var name in p.TargetGameObjects)
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        var go = GameObject.Find(name);
                        if (go != null) targets.Add(go.transform.position);
                    }

                float halfAngle = p.FieldOfView * 0.5f;
                foreach (var tp in targets)
                {
                    var toT = tp - viewer;
                    if (toT.sqrMagnitude < 1e-10f) continue;
                    float ang = Vector3.Angle(look, toT.normalized);
                    if (ang > halfAngle) continue; // outside cone → skip

                    TestVisibility(viewer, tp, p.MaxDistance, p.LayerMask,
                                    out var isVisible, out var distance, out var blockedBy);
                    result.Results.Add(new SightlineHit
                    {
                        TargetPosition = new[] { tp.x, tp.y, tp.z },
                        IsVisible = isVisible,
                        Distance = distance,
                        BlockedBy = blockedBy,
                    });
                    if (isVisible) result.VisibleCount++; else result.BlockedCount++;
                    result.TotalTargets++;

                    var end = isVisible ? tp : PointAlong(viewer, tp, distance);
                    rays.Add((viewer, end, isVisible));
                }
                return true;
            }

            // No explicit targets: stratified sampling across the cone.
            int n = Mathf.Max(1, p.ViewshedResolution);
            float halfAngleRad = p.FieldOfView * 0.5f * Mathf.Deg2Rad;
            float cosHalf = Mathf.Cos(halfAngleRad);
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (int i = 0; i < n; i++)
            {
                // Map i to a cap of the sphere around +Z.
                float z = Mathf.Lerp(1f, cosHalf, (i + 0.5f) / n);
                float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float theta = goldenAngle * i;
                float x = Mathf.Cos(theta) * radius;
                float y = Mathf.Sin(theta) * radius;
                var localDir = new Vector3(x, y, z).normalized;

                // Rotate local +Z frame onto look direction.
                var rot = Quaternion.FromToRotation(Vector3.forward, look);
                var dir = rot * localDir;

                bool blocked = UnityEngine.Physics.Raycast(viewer, dir, out var hitInfo,
                                                           p.MaxDistance, p.LayerMask);
                bool visible = !blocked;
                result.TotalTargets++;
                if (visible) result.VisibleCount++; else result.BlockedCount++;

                var endPoint = blocked ? hitInfo.point : viewer + dir * p.MaxDistance;
                result.Results.Add(new SightlineHit
                {
                    TargetPosition = new[] { endPoint.x, endPoint.y, endPoint.z },
                    IsVisible = visible,
                    Distance = blocked ? hitInfo.distance : p.MaxDistance,
                    BlockedBy = blocked && hitInfo.collider != null ? hitInfo.collider.gameObject.name : null,
                });
                rays.Add((viewer, endPoint, visible));
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Shared raycast toward a specific target.
        // ------------------------------------------------------------------
        static bool TestVisibility(Vector3 from, Vector3 target, float maxDistance, int layerMask,
                                    out bool isVisible, out float distance, out string blockedBy)
        {
            blockedBy = null;
            var delta = target - from;
            float targetDist = delta.magnitude;
            if (targetDist < 1e-6f)
            {
                isVisible = true;
                distance = 0f;
                return false;
            }

            var dir = delta / targetDist;
            float rayLen = Mathf.Min(maxDistance, targetDist);

            if (UnityEngine.Physics.Raycast(from, dir, out var hit, rayLen, layerMask))
            {
                // If the hit point is (approximately) the target, the target itself is what
                // we hit — call it visible.
                if (Vector3.Distance(hit.point, target) <= TargetHitEpsilon ||
                    hit.distance >= targetDist - TargetHitEpsilon)
                {
                    isVisible = true;
                    distance = hit.distance;
                    return true;
                }

                isVisible = false;
                distance = hit.distance;
                blockedBy = hit.collider != null ? hit.collider.gameObject.name : null;
                return true;
            }

            // No hit within the shorter of MaxDistance and target distance.
            if (targetDist > maxDistance)
            {
                // Target is beyond MaxDistance — treat as blocked by distance.
                isVisible = false;
                distance = maxDistance;
                blockedBy = null;
                return false;
            }

            isVisible = true;
            distance = targetDist;
            return false;
        }

        static Vector3 PointAlong(Vector3 from, Vector3 to, float distance)
        {
            var d = (to - from);
            var len = d.magnitude;
            if (len < 1e-6f) return from;
            return from + (d / len) * distance;
        }

        static Color ResolveColor(float[] c, Color fallback)
        {
            if (c != null && c.Length == 4)
                return new Color(c[0], c[1], c[2], c[3]);
            return fallback;
        }
    }
}
