using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// Computes bounding box (AABB/OBB/renderer/collider/mesh) of a GameObject,
    /// with unit conversion, volume, surface area, diagonal length, and optional wireframe visual.
    /// </summary>
    public static class MeasureBoundsTool
    {
        static readonly string[] ValidModes = { "aabb", "obb", "renderer", "collider", "mesh" };
        static readonly string[] ValidUnits = { "meters", "centimeters", "millimeters", "feet", "inches" };

        // CreateVisual mutates scene (creates a GameObject), so isReadOnly: false.
        [MosaicTool("measure/bounds",
                    "Computes bounding box (AABB/OBB/renderer/collider/mesh) of a GameObject with unit conversion, volume, surface area, diagonal, and optional wireframe visual",
                    isReadOnly: false, category: "measure", Context = ToolContext.Both)]
        public static ToolResult<MeasureBoundsResult> Execute(MeasureBoundsParams p)
        {
            if (p == null)
                return ToolResult<MeasureBoundsResult>.Fail("Params required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrWhiteSpace(p.GameObjectName))
                return ToolResult<MeasureBoundsResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            var mode = string.IsNullOrWhiteSpace(p.Mode) ? "aabb" : p.Mode.Trim().ToLowerInvariant();
            if (System.Array.IndexOf(ValidModes, mode) < 0)
                return ToolResult<MeasureBoundsResult>.Fail(
                    $"Invalid Mode '{p.Mode}'. Valid: {string.Join(", ", ValidModes)}",
                    ErrorCodes.INVALID_PARAM);

            var unit = string.IsNullOrWhiteSpace(p.Unit) ? "meters" : p.Unit.Trim().ToLowerInvariant();
            if (System.Array.IndexOf(ValidUnits, unit) < 0)
                return ToolResult<MeasureBoundsResult>.Fail(
                    $"Invalid Unit '{p.Unit}'. Valid: {string.Join(", ", ValidUnits)}",
                    ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<MeasureBoundsResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found",
                    ErrorCodes.NOT_FOUND);

            // Compute bounds (world-space AABB in meters)
            Bounds? boundsOpt;
            string boundsError;
            bool ok = TryComputeBounds(go, mode, p.IncludeChildren, out boundsOpt, out boundsError);
            if (!ok || !boundsOpt.HasValue)
                return ToolResult<MeasureBoundsResult>.Fail(
                    boundsError ?? "Could not compute bounds",
                    ErrorCodes.INTERNAL_ERROR);

            Bounds bounds = boundsOpt.Value;

            // Unit conversion scaling
            float scale = UnitScaleFromMeters(unit);

            Vector3 min = bounds.min * scale;
            Vector3 max = bounds.max * scale;
            Vector3 center = bounds.center * scale;
            Vector3 size = bounds.size * scale;
            Vector3 extents = size * 0.5f;

            float volume = size.x * size.y * size.z;
            float surfaceArea = 2f * (size.x * size.y + size.y * size.z + size.x * size.z);
            float diagonal = Mathf.Sqrt(size.x * size.x + size.y * size.y + size.z * size.z);

            int annotationId = -1;
            if (p.CreateVisual)
            {
                var color = ColorFromArray(p.VisualColor);
                var visual = CreateWireframeBox(bounds, color, p.GameObjectName, mode, go.transform, mode == "obb");
                if (visual != null)
                {
                    Undo.RegisterCreatedObjectUndo(visual, "Create Measure Bounds Visual");
                    annotationId = visual.GetInstanceID();
                }
            }

            return ToolResult<MeasureBoundsResult>.Ok(new MeasureBoundsResult
            {
                Min            = new[] { min.x, min.y, min.z },
                Max            = new[] { max.x, max.y, max.z },
                Center         = new[] { center.x, center.y, center.z },
                Size           = new[] { size.x, size.y, size.z },
                Extents        = new[] { extents.x, extents.y, extents.z },
                Volume         = volume,
                SurfaceArea    = surfaceArea,
                DiagonalLength = diagonal,
                Unit           = unit,
                Mode           = mode,
                AnnotationId   = annotationId
            });
        }

        // ---------------------------------------------------------------
        // Bounds computation
        // ---------------------------------------------------------------

        private static bool TryComputeBounds(
            GameObject go, string mode, bool includeChildren,
            out Bounds? bounds, out string error)
        {
            bounds = null;
            error = null;

            switch (mode)
            {
                case "aabb":
                    return TryAabb(go, includeChildren, out bounds, out error);
                case "renderer":
                    return TryRendererBounds(go, includeChildren, out bounds, out error);
                case "collider":
                    return TryColliderBounds(go, includeChildren, out bounds, out error);
                case "mesh":
                    return TryMeshBounds(go, includeChildren, out bounds, out error);
                case "obb":
                    // OBB world-space AABB representation: rotate local bounds by transform
                    return TryObbAsAabb(go, includeChildren, out bounds, out error);
                default:
                    error = $"Unsupported mode '{mode}'";
                    return false;
            }
        }

        private static bool TryAabb(GameObject go, bool includeChildren, out Bounds? result, out string error)
        {
            error = null;
            Bounds? combined = null;

            // Renderers
            var renderers = includeChildren
                ? go.GetComponentsInChildren<Renderer>(includeInactive: false)
                : go.GetComponents<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                Encapsulate(ref combined, r.bounds);
            }

            // Colliders
            var colliders = includeChildren
                ? go.GetComponentsInChildren<Collider>(includeInactive: false)
                : go.GetComponents<Collider>();
            foreach (var c in colliders)
            {
                if (c == null) continue;
                Encapsulate(ref combined, c.bounds);
            }

            if (!combined.HasValue)
            {
                error = "No Renderer or Collider bounds available on target";
                result = null;
                return false;
            }

            result = combined;
            return true;
        }

        private static bool TryRendererBounds(GameObject go, bool includeChildren, out Bounds? result, out string error)
        {
            error = null;
            Bounds? combined = null;
            var renderers = includeChildren
                ? go.GetComponentsInChildren<Renderer>(includeInactive: false)
                : go.GetComponents<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                Encapsulate(ref combined, r.bounds);
            }

            if (!combined.HasValue)
            {
                error = "No Renderer components found";
                result = null;
                return false;
            }
            result = combined;
            return true;
        }

        private static bool TryColliderBounds(GameObject go, bool includeChildren, out Bounds? result, out string error)
        {
            error = null;
            Bounds? combined = null;
            var colliders = includeChildren
                ? go.GetComponentsInChildren<Collider>(includeInactive: false)
                : go.GetComponents<Collider>();
            foreach (var c in colliders)
            {
                if (c == null) continue;
                Encapsulate(ref combined, c.bounds);
            }

            if (!combined.HasValue)
            {
                error = "No Collider components found";
                result = null;
                return false;
            }
            result = combined;
            return true;
        }

        private static bool TryMeshBounds(GameObject go, bool includeChildren, out Bounds? result, out string error)
        {
            error = null;
            Bounds? combined = null;

            var filters = includeChildren
                ? go.GetComponentsInChildren<MeshFilter>(includeInactive: false)
                : go.GetComponents<MeshFilter>();

            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var localBounds = mf.sharedMesh.bounds;
                var world = TransformBounds(mf.transform, localBounds);
                Encapsulate(ref combined, world);
            }

            if (!combined.HasValue)
            {
                error = "No MeshFilter.sharedMesh available on target";
                result = null;
                return false;
            }
            result = combined;
            return true;
        }

        /// <summary>
        /// OBB: take the local-space combined bounds (renderer + collider + mesh) on the target's
        /// coordinate frame, then rotate by transform. We return the resulting world-space AABB
        /// that contains the oriented box, preserving rotation info in the visual (when enabled).
        /// </summary>
        private static bool TryObbAsAabb(GameObject go, bool includeChildren, out Bounds? result, out string error)
        {
            error = null;

            // Build an AABB in the target's local space.
            var localToWorld = go.transform.localToWorldMatrix;
            var worldToLocal = go.transform.worldToLocalMatrix;

            Bounds? local = null;

            // Meshes
            var filters = includeChildren
                ? go.GetComponentsInChildren<MeshFilter>(includeInactive: false)
                : go.GetComponents<MeshFilter>();
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds; // local to mesh's transform
                var world = TransformBounds(mf.transform, b);
                var localB = TransformBoundsInverse(worldToLocal, world);
                Encapsulate(ref local, localB);
            }

            // Fallback to renderer/collider world bounds converted to target's local.
            if (!local.HasValue)
            {
                var renderers = includeChildren
                    ? go.GetComponentsInChildren<Renderer>(includeInactive: false)
                    : go.GetComponents<Renderer>();
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var localB = TransformBoundsInverse(worldToLocal, r.bounds);
                    Encapsulate(ref local, localB);
                }

                var colliders = includeChildren
                    ? go.GetComponentsInChildren<Collider>(includeInactive: false)
                    : go.GetComponents<Collider>();
                foreach (var c in colliders)
                {
                    if (c == null) continue;
                    var localB = TransformBoundsInverse(worldToLocal, c.bounds);
                    Encapsulate(ref local, localB);
                }
            }

            if (!local.HasValue)
            {
                error = "No bounds sources available on target";
                result = null;
                return false;
            }

            // Transform local AABB by the full localToWorld (rotated + translated) to get a world-space AABB
            // that encloses the oriented box.
            result = TransformBoundsMatrix(localToWorld, local.Value);
            return true;
        }

        private static void Encapsulate(ref Bounds? acc, Bounds b)
        {
            if (!acc.HasValue) { acc = b; return; }
            var cur = acc.Value;
            cur.Encapsulate(b);
            acc = cur;
        }

        /// <summary>Transform a local Bounds to world-space AABB via a Transform.</summary>
        private static Bounds TransformBounds(Transform t, Bounds localBounds)
        {
            return TransformBoundsMatrix(t.localToWorldMatrix, localBounds);
        }

        /// <summary>Transform a Bounds through a matrix, returning an axis-aligned bounds of the 8 corners.</summary>
        private static Bounds TransformBoundsMatrix(Matrix4x4 m, Bounds b)
        {
            var c = b.center;
            var e = b.extents;

            Vector3 p0 = m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z));
            Vector3 p1 = m.MultiplyPoint3x4(c + new Vector3( e.x, -e.y, -e.z));
            Vector3 p2 = m.MultiplyPoint3x4(c + new Vector3(-e.x,  e.y, -e.z));
            Vector3 p3 = m.MultiplyPoint3x4(c + new Vector3( e.x,  e.y, -e.z));
            Vector3 p4 = m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y,  e.z));
            Vector3 p5 = m.MultiplyPoint3x4(c + new Vector3( e.x, -e.y,  e.z));
            Vector3 p6 = m.MultiplyPoint3x4(c + new Vector3(-e.x,  e.y,  e.z));
            Vector3 p7 = m.MultiplyPoint3x4(c + new Vector3( e.x,  e.y,  e.z));

            var result = new Bounds(p0, Vector3.zero);
            result.Encapsulate(p1);
            result.Encapsulate(p2);
            result.Encapsulate(p3);
            result.Encapsulate(p4);
            result.Encapsulate(p5);
            result.Encapsulate(p6);
            result.Encapsulate(p7);
            return result;
        }

        private static Bounds TransformBoundsInverse(Matrix4x4 worldToLocal, Bounds worldBounds)
        {
            return TransformBoundsMatrix(worldToLocal, worldBounds);
        }

        // ---------------------------------------------------------------
        // Unit conversion
        // ---------------------------------------------------------------

        private static float UnitScaleFromMeters(string unit)
        {
            switch (unit)
            {
                case "meters":      return 1f;
                case "centimeters": return 100f;
                case "millimeters": return 1000f;
                case "feet":        return 3.28084f;
                case "inches":      return 39.3701f;
                default:            return 1f;
            }
        }

        // ---------------------------------------------------------------
        // Wireframe visual
        // ---------------------------------------------------------------

        private static Color ColorFromArray(float[] arr)
        {
            if (arr == null || arr.Length < 3) return Color.cyan;
            float r = arr[0], g = arr[1], b = arr[2];
            float a = arr.Length >= 4 ? arr[3] : 1f;
            return new Color(r, g, b, a);
        }

        /// <summary>
        /// Creates a wireframe box GameObject using 12 edges via a single LineRenderer.
        /// For AABB/renderer/collider/mesh: uses the world-space AABB.
        /// For OBB: uses the 8 corners of the oriented box in world space (target's local AABB rotated by its transform).
        /// </summary>
        private static GameObject CreateWireframeBox(Bounds worldAabb, Color color, string sourceName, string mode, Transform sourceTransform, bool orientedBox)
        {
            var go = new GameObject($"MeasureBounds_{sourceName}_{mode}");

            // Compute 8 corners
            Vector3[] corners = new Vector3[8];
            if (orientedBox && sourceTransform != null)
            {
                // Re-compute oriented corners: the incoming worldAabb is a containing AABB.
                // To draw an actual oriented box, we rebuild local AABB from world AABB relative to target's transform.
                var worldToLocal = sourceTransform.worldToLocalMatrix;
                var localToWorld = sourceTransform.localToWorldMatrix;
                var localAabb = TransformBoundsMatrix(worldToLocal, worldAabb);
                corners = CornersFromLocalBounds(localAabb, localToWorld);
            }
            else
            {
                var c = worldAabb.center;
                var e = worldAabb.extents;
                corners[0] = c + new Vector3(-e.x, -e.y, -e.z);
                corners[1] = c + new Vector3( e.x, -e.y, -e.z);
                corners[2] = c + new Vector3( e.x, -e.y,  e.z);
                corners[3] = c + new Vector3(-e.x, -e.y,  e.z);
                corners[4] = c + new Vector3(-e.x,  e.y, -e.z);
                corners[5] = c + new Vector3( e.x,  e.y, -e.z);
                corners[6] = c + new Vector3( e.x,  e.y,  e.z);
                corners[7] = c + new Vector3(-e.x,  e.y,  e.z);
            }

            // 12 edges traced with a single LineRenderer by revisiting corners (16 positions).
            // Bottom loop: 0-1-2-3-0, Up to 4, top loop: 4-5-6-7-4, connect 5-1, 2-6, 7-3.
            var positions = new List<Vector3>
            {
                corners[0], corners[1], corners[2], corners[3], corners[0],
                corners[4], corners[5], corners[1],
                corners[5], corners[6], corners[2],
                corners[6], corners[7], corners[3],
                corners[7], corners[4]
            };

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = positions.Count;
            lr.SetPositions(positions.ToArray());
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.02f;
            lr.loop = false;

            // Unlit color material
            var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.hideFlags = HideFlags.HideAndDontSave;
                mat.color = color;
                lr.sharedMaterial = mat;
            }
            lr.startColor = color;
            lr.endColor = color;

            return go;
        }

        private static Vector3[] CornersFromLocalBounds(Bounds localBounds, Matrix4x4 localToWorld)
        {
            var c = localBounds.center;
            var e = localBounds.extents;
            var result = new Vector3[8];
            result[0] = localToWorld.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z));
            result[1] = localToWorld.MultiplyPoint3x4(c + new Vector3( e.x, -e.y, -e.z));
            result[2] = localToWorld.MultiplyPoint3x4(c + new Vector3( e.x, -e.y,  e.z));
            result[3] = localToWorld.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y,  e.z));
            result[4] = localToWorld.MultiplyPoint3x4(c + new Vector3(-e.x,  e.y, -e.z));
            result[5] = localToWorld.MultiplyPoint3x4(c + new Vector3( e.x,  e.y, -e.z));
            result[6] = localToWorld.MultiplyPoint3x4(c + new Vector3( e.x,  e.y,  e.z));
            result[7] = localToWorld.MultiplyPoint3x4(c + new Vector3(-e.x,  e.y,  e.z));
            return result;
        }
    }
}
