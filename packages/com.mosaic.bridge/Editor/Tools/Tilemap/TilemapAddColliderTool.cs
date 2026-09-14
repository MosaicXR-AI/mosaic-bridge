using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Tilemap
{
    public static class TilemapAddColliderTool
    {
        // O4 §4.1 (G1): a static Rigidbody2D MUST exist (and be Static) before TilemapCollider2D/
        // CompositeCollider2D are added, or the composite geometry comes out empty — the order
        // below is not incidental.
        [MosaicTool("tilemap/add-collider",
                    "Adds solid collision to a Tilemap: a static Rigidbody2D, a TilemapCollider2D, and (by " +
                    "default, Composite=true) a CompositeCollider2D merging per-tile colliders into one shape. " +
                    "UsedByEffector adds a PlatformEffector2D for one-way (jump-through) platforms — Unity " +
                    "silently ignores an effector unless usedByEffector is also set on the collider, which this " +
                    "does for you.",
                    isReadOnly: false)]
        public static ToolResult<TilemapAddColliderResult> Execute(TilemapAddColliderParams p)
        {
            if (p.TilemapInstanceId == null && string.IsNullOrEmpty(p.TilemapName))
                return ToolResult<TilemapAddColliderResult>.Fail(
                    "Either TilemapInstanceId or TilemapName is required", ErrorCodes.INVALID_PARAM);

            GameObject go = null;
#pragma warning disable CS0618
            if (p.TilemapInstanceId.HasValue) go = UnityIds.Resolve(p.TilemapInstanceId.Value) as GameObject;
#pragma warning restore CS0618
            if (go == null && !string.IsNullOrEmpty(p.TilemapName)) go = GameObject.Find(p.TilemapName);
            if (go == null)
                return ToolResult<TilemapAddColliderResult>.Fail(
                    $"GameObject not found (TilemapInstanceId={p.TilemapInstanceId}, TilemapName='{p.TilemapName}')",
                    ErrorCodes.NOT_FOUND);

            if (go.GetComponent<UnityEngine.Tilemaps.Tilemap>() == null)
                return ToolResult<TilemapAddColliderResult>.Fail(
                    $"GameObject '{go.name}' has no Tilemap component", ErrorCodes.NOT_FOUND);

            var result = new TilemapAddColliderResult { GameObjectName = go.name };

            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = Undo.AddComponent<Rigidbody2D>(go);
                result.RigidbodyAdded = true;
            }
            rb.bodyType = RigidbodyType2D.Static;

            var tilemapCollider = go.GetComponent<TilemapCollider2D>();
            if (tilemapCollider == null)
            {
                tilemapCollider = Undo.AddComponent<TilemapCollider2D>(go);
                result.TilemapColliderAdded = true;
            }

            Collider2D effectorTarget = tilemapCollider;
            if (p.Composite)
            {
                tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
                var composite = go.GetComponent<CompositeCollider2D>();
                if (composite == null)
                {
                    composite = Undo.AddComponent<CompositeCollider2D>(go);
                    result.CompositeColliderAdded = true;
                }
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                effectorTarget = composite;
            }

            if (p.UsedByEffector)
            {
                effectorTarget.usedByEffector = true;
                if (go.GetComponent<PlatformEffector2D>() == null)
                {
                    Undo.AddComponent<PlatformEffector2D>(go);
                    result.EffectorAdded = true;
                }
            }

            return ToolResult<TilemapAddColliderResult>.Ok(result);
        }
    }
}
