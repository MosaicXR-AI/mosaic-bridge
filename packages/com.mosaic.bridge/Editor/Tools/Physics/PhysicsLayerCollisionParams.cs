using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsLayerCollisionParams
    {
        /// <summary>"get", "set", or "matrix" (the full 32x32 table, sparse — only non-default entries).</summary>
        [Required] public string Action { get; set; }

        /// <summary>Required for get/set.</summary>
        public string LayerA { get; set; }
        /// <summary>Required for get/set.</summary>
        public string LayerB { get; set; }

        /// <summary>Required for set. true = the layers CAN collide (Unity's default for every pair);
        /// false = ignored (Physics.IgnoreLayerCollision).</summary>
        public bool? CanCollide { get; set; }
    }
}
