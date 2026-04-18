using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsVerletCreateParams
    {
        [Required] public string Type              { get; set; }
        public int?              PointCount        { get; set; }
        public float?            SegmentLength     { get; set; }
        public float?            Stiffness         { get; set; }
        public float?            Damping           { get; set; }
        public float?            Gravity           { get; set; }
        public int[]             PinPoints         { get; set; }
        public int?              SolverIterations  { get; set; }
        public float?            CollisionRadius   { get; set; }
        public string            AttachTo          { get; set; }
        public string            Name              { get; set; }
        public float[]           Position          { get; set; }
        public string            SavePath          { get; set; }
    }
}
