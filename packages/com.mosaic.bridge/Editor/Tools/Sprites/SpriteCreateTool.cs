using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;
using Mosaic.Bridge.Core.Assets;

namespace Mosaic.Bridge.Tools.Sprites
{
    public static class SpriteCreateTool
    {
        // O4 §4.1 (G1): "the 2D analogue of gameobject/create PrimitiveType" — needs §3.1's
        // sub-asset addressing to point at one sprite inside a sliced sheet.
        [MosaicTool("sprite/create",
                    "Creates a GameObject with a SpriteRenderer. SpritePath supports 'Assets/sheet.png#Run_03' " +
                    "sub-asset addressing. DrawMode=Sliced needs the sprite's own Border to be non-zero (set via " +
                    "texture/set-import-settings) — Image.type-style flags alone do nothing. AutoCollider adds a " +
                    "matching Collider2D sized/shaped to the sprite.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<SpriteCreateResult> Execute(SpriteCreateParams p)
        {
            if (string.IsNullOrEmpty(p.SpritePath))
                return ToolResult<SpriteCreateResult>.Fail("SpritePath is required", ErrorCodes.INVALID_PARAM);

            if (!ObjectReferenceResolver.TryResolveAsset(p.SpritePath, typeof(Sprite), out var resolved, out var error))
                return ToolResult<SpriteCreateResult>.Fail(error, ErrorCodes.NOT_FOUND);
            var sprite = (Sprite)resolved;

            if (!TryParseDrawMode(p.DrawMode, out var drawMode))
                return ToolResult<SpriteCreateResult>.Fail(
                    $"Unknown DrawMode '{p.DrawMode}'. Valid: Simple, Sliced, Tiled", ErrorCodes.INVALID_PARAM);
            if (!TryParseMaskInteraction(p.MaskInteraction, out var maskInteraction))
                return ToolResult<SpriteCreateResult>.Fail(
                    $"Unknown MaskInteraction '{p.MaskInteraction}'. Valid: None, VisibleInsideMask, VisibleOutsideMask",
                    ErrorCodes.INVALID_PARAM);
            if (!TryParseAutoCollider(p.AutoCollider, out var autoCollider))
                return ToolResult<SpriteCreateResult>.Fail(
                    $"Unknown AutoCollider '{p.AutoCollider}'. Valid: None, Box, Circle, Capsule, Polygon",
                    ErrorCodes.INVALID_PARAM);

            var go = new GameObject(string.IsNullOrEmpty(p.Name) ? sprite.name : p.Name);
            if (p.Position != null && p.Position.Length >= 2)
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position.Length >= 3 ? p.Position[2] : 0f);
            if (!string.IsNullOrEmpty(p.ParentName))
            {
                var parent = GameObject.Find(p.ParentName);
                if (parent != null) go.transform.SetParent(parent.transform, true);
            }

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.flipX = p.FlipX;
            renderer.flipY = p.FlipY;
            renderer.drawMode = drawMode;
            renderer.maskInteraction = maskInteraction;
            if (!string.IsNullOrEmpty(p.SortingLayerName)) renderer.sortingLayerName = p.SortingLayerName;
            renderer.sortingOrder = p.SortingOrder;
            if (p.Color != null)
            {
                if (p.Color.Length < 3)
                    return ToolResult<SpriteCreateResult>.Fail("Color requires a [r, g, b] or [r, g, b, a] float array", ErrorCodes.INVALID_PARAM);
                renderer.color = new Color(p.Color[0], p.Color[1], p.Color[2], p.Color.Length >= 4 ? p.Color[3] : 1f);
            }
            if (drawMode != SpriteDrawMode.Simple && p.Size != null && p.Size.Length == 2)
                renderer.size = new Vector2(p.Size[0], p.Size[1]);

            string colliderType = "None";
            switch (autoCollider)
            {
                case AutoColliderKind.Box:
                    go.AddComponent<BoxCollider2D>();
                    colliderType = "Box";
                    break;
                case AutoColliderKind.Circle:
                    go.AddComponent<CircleCollider2D>();
                    colliderType = "Circle";
                    break;
                case AutoColliderKind.Capsule:
                    go.AddComponent<CapsuleCollider2D>();
                    colliderType = "Capsule";
                    break;
                case AutoColliderKind.Polygon:
                    var polygon = go.AddComponent<PolygonCollider2D>();
                    // PolygonCollider2D auto-derives its shape from the sprite's own physics shape
                    // (Sprite Editor "Custom Physics Shape") the moment it is added to a GameObject
                    // whose SpriteRenderer.sprite is already set — no manual SetPath call needed.
                    colliderType = "Polygon";
                    break;
            }

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create Sprite");

            return ToolResult<SpriteCreateResult>.Ok(new SpriteCreateResult
            {
                InstanceId = UnityIds.Of(go),
                GameObjectName = go.name,
                SpriteName = sprite.name,
                DrawMode = drawMode.ToString(),
                ColliderType = colliderType,
            });
        }

        private enum AutoColliderKind { None, Box, Circle, Capsule, Polygon }

        private static bool TryParseDrawMode(string value, out SpriteDrawMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "simple": result = SpriteDrawMode.Simple; return true;
                case "sliced": result = SpriteDrawMode.Sliced; return true;
                case "tiled":  result = SpriteDrawMode.Tiled;  return true;
                default:       result = SpriteDrawMode.Simple; return false;
            }
        }

        private static bool TryParseMaskInteraction(string value, out SpriteMaskInteraction result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "none":                result = SpriteMaskInteraction.None;               return true;
                case "visibleinsidemask":   result = SpriteMaskInteraction.VisibleInsideMask;   return true;
                case "visibleoutsidemask":  result = SpriteMaskInteraction.VisibleOutsideMask;  return true;
                default:                    result = SpriteMaskInteraction.None;                return false;
            }
        }

        private static bool TryParseAutoCollider(string value, out AutoColliderKind result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "none":    result = AutoColliderKind.None;    return true;
                case "box":     result = AutoColliderKind.Box;     return true;
                case "circle":  result = AutoColliderKind.Circle;  return true;
                case "capsule": result = AutoColliderKind.Capsule; return true;
                case "polygon": result = AutoColliderKind.Polygon; return true;
                default:        result = AutoColliderKind.None;    return false;
            }
        }
    }
}
