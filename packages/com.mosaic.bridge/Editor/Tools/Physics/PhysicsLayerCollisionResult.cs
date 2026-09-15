namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class LayerCollisionIgnoredPair
    {
        public string LayerA { get; set; }
        public string LayerB { get; set; }
    }

    public sealed class PhysicsLayerCollisionResult
    {
        public string Action     { get; set; }
        public string LayerA     { get; set; }
        public string LayerB     { get; set; }
        public bool   CanCollide { get; set; }

        /// <summary>"matrix" only: every layer pair currently set to ignore collision.</summary>
        public LayerCollisionIgnoredPair[] IgnoredPairs { get; set; }

        public string Message { get; set; }
    }
}
