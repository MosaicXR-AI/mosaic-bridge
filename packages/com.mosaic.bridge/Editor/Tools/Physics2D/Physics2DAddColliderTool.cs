using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Physics2D
{
    public static class Physics2DAddColliderTool
    {
        // O4 §4.1 (G1): mirror of physics/add-collider for 2D. Polygon auto-derives from the
        // sprite's own physics shape the moment it's added to a GameObject with a SpriteRenderer
        // already set (same behavior sprite/create's AutoCollider=Polygon already relies on) —
        // Points overrides that only when explicitly given.
        [MosaicTool("physics2d/add-collider",
                    "Adds a 2D collider (Box, Circle, Capsule, Polygon, or Edge) to a GameObject. Box/Circle " +
                    "auto-fit to the GameObject's SpriteRenderer bounds when Size/Radius are omitted. Polygon " +
                    "uses the sprite's own physics shape automatically unless Points is given. Edge requires " +
                    "Points. Set AddRigidbody=true to also add a Rigidbody2D (skipped if one already exists).",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<Physics2DAddColliderResult> Execute(Physics2DAddColliderParams p)
        {
            var go = Physics2DToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<Physics2DAddColliderResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);
            if (string.IsNullOrEmpty(p.ColliderType))
                return ToolResult<Physics2DAddColliderResult>.Fail(
                    "ColliderType is required (Box, Circle, Capsule, Polygon, or Edge)", ErrorCodes.INVALID_PARAM);

            // Deliberately not `GetComponent<SpriteRenderer>()?.sprite` — a real failure on this
            // Unity version showed the null-conditional operator does not short-circuit through
            // Unity's own fake-null check for a missing SpriteRenderer, so `.sprite` still runs
            // and throws MissingComponentException. An explicit != null does the real check.
            var existingRenderer = go.GetComponent<SpriteRenderer>();
            var sprite = existingRenderer != null ? existingRenderer.sprite : null;
            Collider2D collider;
            var type = p.ColliderType.Trim();

            switch (type.ToLowerInvariant())
            {
                case "box":
                {
                    var box = Undo.AddComponent<BoxCollider2D>(go);
                    if (p.Size != null)
                    {
                        if (p.Size.Length != 2)
                            return ToolResult<Physics2DAddColliderResult>.Fail("Size requires exactly [w, h]", ErrorCodes.INVALID_PARAM);
                        box.size = new Vector2(p.Size[0], p.Size[1]);
                    }
                    else if (sprite != null)
                    {
                        box.size = sprite.bounds.size;
                    }
                    if (p.EdgeRadius.HasValue) box.edgeRadius = p.EdgeRadius.Value;
                    collider = box;
                    break;
                }
                case "circle":
                {
                    var circle = Undo.AddComponent<CircleCollider2D>(go);
                    if (p.Radius.HasValue)
                        circle.radius = p.Radius.Value;
                    else if (sprite != null)
                        circle.radius = Mathf.Max(sprite.bounds.extents.x, sprite.bounds.extents.y);
                    collider = circle;
                    break;
                }
                case "capsule":
                {
                    var capsule = Undo.AddComponent<CapsuleCollider2D>(go);
                    if (p.Size != null)
                    {
                        if (p.Size.Length != 2)
                            return ToolResult<Physics2DAddColliderResult>.Fail("Size requires exactly [w, h]", ErrorCodes.INVALID_PARAM);
                        capsule.size = new Vector2(p.Size[0], p.Size[1]);
                    }
                    else if (sprite != null)
                    {
                        capsule.size = sprite.bounds.size;
                    }
                    collider = capsule;
                    break;
                }
                case "polygon":
                {
                    var polygon = Undo.AddComponent<PolygonCollider2D>(go);
                    if (p.Points != null)
                    {
                        if (!TryToVector2Array(p.Points, out var pts, out var pointsError))
                            return ToolResult<Physics2DAddColliderResult>.Fail(pointsError, ErrorCodes.INVALID_PARAM);
                        polygon.SetPath(0, pts);
                    }
                    // else: Unity already derived the path from the sprite's own physics shape
                    // (Sprite Editor "Custom Physics Shape") when the component was added.
                    collider = polygon;
                    break;
                }
                case "edge":
                {
                    if (p.Points == null || p.Points.Length < 2)
                        return ToolResult<Physics2DAddColliderResult>.Fail(
                            "Edge requires Points with at least 2 [x, y] entries", ErrorCodes.INVALID_PARAM);
                    if (!TryToVector2Array(p.Points, out var edgePts, out var edgeError))
                        return ToolResult<Physics2DAddColliderResult>.Fail(edgeError, ErrorCodes.INVALID_PARAM);
                    var edge = Undo.AddComponent<EdgeCollider2D>(go);
                    edge.SetPoints(new System.Collections.Generic.List<Vector2>(edgePts));
                    collider = edge;
                    break;
                }
                default:
                    return ToolResult<Physics2DAddColliderResult>.Fail(
                        $"Unknown ColliderType '{p.ColliderType}'. Valid: Box, Circle, Capsule, Polygon, Edge",
                        ErrorCodes.INVALID_PARAM);
            }

            if (p.Offset != null)
            {
                if (p.Offset.Length != 2)
                    return ToolResult<Physics2DAddColliderResult>.Fail("Offset requires exactly [x, y]", ErrorCodes.INVALID_PARAM);
                collider.offset = new Vector2(p.Offset[0], p.Offset[1]);
            }
            if (p.IsTrigger.HasValue) collider.isTrigger = p.IsTrigger.Value;
            if (p.UsedByEffector.HasValue) collider.usedByEffector = p.UsedByEffector.Value;

            bool rigidbodyAdded = false;
            if (p.AddRigidbody == true && go.GetComponent<Rigidbody2D>() == null)
            {
                Undo.AddComponent<Rigidbody2D>(go);
                rigidbodyAdded = true;
            }

            return ToolResult<Physics2DAddColliderResult>.Ok(new Physics2DAddColliderResult
            {
                GameObjectName = go.name,
                InstanceId = UnityIds.Of(go),
                ColliderType = type,
                IsTrigger = collider.isTrigger,
                RigidbodyAdded = rigidbodyAdded,
            });
        }

        private static bool TryToVector2Array(float[][] points, out Vector2[] result, out string error)
        {
            result = new Vector2[points.Length];
            error = null;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null || points[i].Length != 2)
                {
                    error = $"Points[{i}] must be a [x, y] pair";
                    return false;
                }
                result[i] = new Vector2(points[i][0], points[i][1]);
            }
            return true;
        }
    }
}
