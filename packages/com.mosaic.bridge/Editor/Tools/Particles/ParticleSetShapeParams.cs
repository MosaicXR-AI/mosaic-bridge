namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleSetShapeParams
    {
        public int? InstanceId { get; set; }
        public string Name { get; set; }

        public string Shape { get; set; }   // Sphere, Hemisphere, Cone, Box, Mesh, Edge
        public float? Radius { get; set; }
        public float? Angle { get; set; }
        public float? Arc { get; set; }
    }
}
