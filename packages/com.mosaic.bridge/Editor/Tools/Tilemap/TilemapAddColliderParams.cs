namespace Mosaic.Bridge.Tools.Tilemap
{
    public sealed class TilemapAddColliderParams
    {
        public int? TilemapInstanceId { get; set; }
        public string TilemapName { get; set; }

        /// <summary>Merge per-tile colliders into one CompositeCollider2D (default true) — the
        /// normal choice for solid ground/walls. False leaves one collider per tile.</summary>
        public bool Composite { get; set; } = true;

        /// <summary>Adds a PlatformEffector2D and sets usedByEffector — for one-way (jump-through)
        /// platforms. A silent no-op if left false and an effector is added by hand later without
        /// also setting usedByEffector, per Unity's own classic footgun here.</summary>
        public bool UsedByEffector { get; set; }
    }
}
